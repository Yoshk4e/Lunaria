using System.Threading.Channels;

namespace Lunaria.GameServer.Net;

public sealed class PlayerHandle
{
    private readonly Channel<PlayerNotification> _channel;
    private readonly TaskCompletionSource _takenOver =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PlayerHandle(Channel<PlayerNotification> channel)
    {
        _channel = channel;
    }

    public bool IsClosed => _channel.Reader.Completion.IsCompleted;

    public async Task<bool> RequestTakeoverAsync()
    {
        try
        {
            await _channel.Writer.WriteAsync(PlayerNotification.TakeOver).ConfigureAwait(false);
        }
        catch (ChannelClosedException)
        {
            return false;
        }

        await _takenOver.Task.ConfigureAwait(false);
        return true;
    }

    public void AcknowledgeTakeover() => _takenOver.TrySetResult();
}
