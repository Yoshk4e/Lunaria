using System.Collections.Concurrent;
using System.Reflection;
using Google.Protobuf;
using Lunaria.Game.Logging;
using Lunaria.GameServer.Handlers;
using Lunaria.Game.Player.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Msg;

namespace Lunaria.GameServer.Net;

public sealed class Router
{
    private readonly ILogger<Router> _logger;
    private readonly IServiceProvider _provider;
    private readonly ConcurrentDictionary<uint, HandlerEntry> _table;
    private readonly RoleStateStore _store;

    public Router(IServiceProvider provider, ILogger<Router> logger, RoleStateStore store)
    {
        _provider = provider;
        _logger = logger;
        _store = store;
        _table = BuildTable();
    }

    public IReadOnlyCollection<uint> RegisteredCommands => _table.Keys.ToArray();

    public bool IsRegistered(uint cmdId) => _table.ContainsKey(cmdId);

    public async Task DispatchAsync(NetContext ctx, byte[] plaintext)
    {
        using var logScope = _logger.BeginPlayerScope(ctx.Player.SessionId, () => ctx.Player.Roles.Active()?.Id);
        CSMsgPkg? pkg;

        try
        {
            pkg = CSMsgPkg.Parser.ParseFrom(plaintext);
        }
        catch (InvalidProtocolBufferException)
        {
            _logger.LogWarning("malformed CSMsgPkg ({Length} bytes)", plaintext.Length);
            throw new IOException("malformed CSMsgPkg");
        }

        var cmd = pkg.Head?.Cmd ?? 0;

        if (!_table.TryGetValue(cmd, out var entry))
        {
            _logger.LogWarning("unhandled command {Cmd}", cmd);
            return;
        }

        if (_logger.IsEnabled(LogLevel.Trace))
            _logger.LogTrace("dispatching cmd {Cmd} to {Handler}", cmd, entry.RequestType.Name);

        object request;

        try
        {
            request = entry.Parse(pkg.Body);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new IOException($"failed to decode {entry.RequestType.Name}: {ex.Message}", ex);
        }

        if (entry.Login is {} login && !login.IsSatisfied(ctx))
        {
            _logger.LogDebug("command {Cmd} requires {Requirement} login", cmd, login.Requirement);
            await entry.RejectLogin!(ctx, request).ConfigureAwait(false);
            return;
        }

        await ctx.RunCommittedAsync(async () => {
            await entry.Invoke(ctx, request).ConfigureAwait(false);
            if (ctx.Player.HasActiveRole && ctx.Player.TasksBootstrapped)
            {
                foreach (var outcome in ctx.Player.SettleServerTargets())
                    await ctx.NotifyAsync(outcome.AllNotifications).ConfigureAwait(false);
            }
        }, player => _store.SaveAsync(player)).ConfigureAwait(false);
    }

    private ConcurrentDictionary<uint, HandlerEntry> BuildTable()
    {
        var table = new ConcurrentDictionary<uint, HandlerEntry>();
        var any = false;

        foreach (var method in typeof(Router).Assembly.GetTypes()
                     .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                     .Where(m => m.GetCustomAttribute<GameHandlerAttribute>() is not null))
        {
            var attribute = method.GetCustomAttribute<GameHandlerAttribute>()!;
            var parameters = method.GetParameters();

            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(NetContext))
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name} must take (NetContext, TRequest)");

            var requestType = parameters[1].ParameterType;

            if (!typeof(IMessage).IsAssignableFrom(requestType))
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name}: request type {requestType.Name} is not a protobuf message");

            var expectsReply = method.ReturnType.IsGenericType
                               && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);

            if (expectsReply && !typeof(IMessage).IsAssignableFrom(method.ReturnType.GetGenericArguments()[0]))
                throw new InvalidOperationException(
                    $"{method.DeclaringType?.Name}.{method.Name}: reply type is not a protobuf message");

            var isStatic = method.IsStatic;
            var instance = isStatic ? null : ActivatorUtilities.CreateInstance(_provider, method.DeclaringType!);

            var invoke = CompileInvoke(method, instance, requestType);
            var parse = CompileParser(requestType);
            var login = method.GetCustomAttribute<RequireLoginAttribute>();
            if (login is not null && !Enum.IsDefined(login.Requirement))
                throw new InvalidOperationException($"Unknown login requirement on {method.DeclaringType?.Name}");
            var replyType = expectsReply ? method.ReturnType.GetGenericArguments()[0] : login?.Reply;
            if (expectsReply && login?.Reply is {} overrideType && overrideType != replyType)
                throw new InvalidOperationException($"Login rejection type does not match the reply on {method.DeclaringType?.Name}");
            var reject = login is null ? null : LoginRejection.Compile(replyType);
            var entry = new HandlerEntry(attribute.CmdId, requestType, parse, invoke, expectsReply, login, reject);

            if (!table.TryAdd(attribute.CmdId, entry))
                throw new InvalidOperationException(
                    $"duplicate handler registration for cmd {attribute.CmdId}");

            any = true;
        }

        if (!any)
            throw new InvalidOperationException("no [GameHandler] registrations found");

        _logger.LogInformation("router created with {Count} handlers", table.Count);
        return table;
    }

    private static Func<ByteString, object> CompileParser(Type requestType)
    {
        var wrapper = typeof(Router).GetMethod(nameof(WrapParser), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(requestType);
        return (Func<ByteString, object>)wrapper.Invoke(obj: null, [])!;
    }

    private static Func<ByteString, object> WrapParser<TRequest>()
        where TRequest : IMessage<TRequest>
    {
        var parser = (MessageParser<TRequest>)typeof(TRequest)
            .GetProperty("Parser", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null)!;
        return body => parser.ParseFrom(body);
    }

    private static Func<NetContext, object, ValueTask> CompileInvoke(MethodInfo method, object? instance, Type requestType)
    {
        if (method.ReturnType == typeof(Task))
        {
            var wrapper = typeof(Router).GetMethod(nameof(WrapVoidHandler), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(requestType);
            return (Func<NetContext, object, ValueTask>)wrapper.Invoke(obj: null, [method, instance])!;
        }

        var replyType = method.ReturnType.GetGenericArguments()[0];

        var replyWrapper = typeof(Router).GetMethod(nameof(WrapReplyHandler), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(requestType, replyType);
        return (Func<NetContext, object, ValueTask>)replyWrapper.Invoke(obj: null, [method, instance])!;
    }

    private static Func<NetContext, object, ValueTask> WrapVoidHandler<TRequest>(MethodInfo method, object? instance)
        where TRequest : IMessage<TRequest>
    {
        var handler = method.CreateDelegate<Func<NetContext, TRequest, Task>>(instance);
        return async (ctx, request) => await handler(ctx, (TRequest)request).ConfigureAwait(false);
    }

    private static Func<NetContext, object, ValueTask> WrapReplyHandler<TRequest, TReply>(MethodInfo method, object? instance)
        where TRequest : IMessage<TRequest>
        where TReply : IMessage<TReply>
    {
        var handler = method.CreateDelegate<Func<NetContext, TRequest, Task<TReply>>>(instance);

        return async (ctx, request) => {
            var reply = await handler(ctx, (TRequest)request).ConfigureAwait(false);

            if (reply is not null)
                await ctx.SendAsync(reply).ConfigureAwait(false);
        };
    }
}
