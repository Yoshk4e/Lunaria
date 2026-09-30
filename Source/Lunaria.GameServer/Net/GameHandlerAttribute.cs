using Msg;

namespace Lunaria.GameServer.Net;


[AttributeUsage(AttributeTargets.Method)]
public sealed class GameHandlerAttribute : Attribute
{
    public GameHandlerAttribute(uint cmdId)
    {
        CmdId = cmdId;
    }

    public GameHandlerAttribute(EClientServerCmds cmd)
    {
        CmdId = (uint)cmd;
    }

    public uint CmdId { get; }
}
