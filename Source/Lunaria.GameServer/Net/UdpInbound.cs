using System.Net;

namespace Lunaria.GameServer.Net;

public readonly record struct UdpInbound(byte[] Ciphertext, uint PlaintextLength, IPEndPoint Peer);
