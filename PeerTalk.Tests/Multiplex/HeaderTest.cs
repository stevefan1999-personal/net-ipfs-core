using System.IO;
using System.Threading.Tasks;
using IpfsShipyard.PeerTalk.Multiplex;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.PeerTalk.Tests.Multiplex;

[TestClass]
public class HeaderTest
{
    [TestMethod]
    public async Task StreamIds()
    {
        await RoundtripAsync(0, PacketType.NewStream);
        await RoundtripAsync(1, PacketType.NewStream);
        await RoundtripAsync(0x1234, PacketType.NewStream);
        await RoundtripAsync(0x12345678, PacketType.NewStream);
        await RoundtripAsync(Header.MinStreamId, PacketType.NewStream);
        await RoundtripAsync(Header.MaxStreamId, PacketType.NewStream);
    }

    private async Task RoundtripAsync(long id, PacketType type)
    {
        var header1 = new Header { StreamId = id, PacketType = type };
        var ms = new MemoryStream();
        await header1.WriteAsync(ms);
        ms.Position = 0;
        var header2 = await Header.ReadAsync(ms);
        Assert.AreEqual(header1.StreamId, header2.StreamId);
        Assert.AreEqual(header1.PacketType, header2.PacketType);
    }
}