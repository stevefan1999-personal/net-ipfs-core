﻿using System.IO;
using IpfsShipyard.PeerTalk.Multiplex;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.PeerTalk.Tests.Multiplex
{
    [TestClass]
    public class HeaderTest
    {
        [TestMethod]
        public void StreamIds()
        {
            Roundtrip(0, PacketType.NewStream);
            Roundtrip(1, PacketType.NewStream);
            Roundtrip(0x1234, PacketType.NewStream);
            Roundtrip(0x12345678, PacketType.NewStream);
            Roundtrip(Header.MinStreamId, PacketType.NewStream);
            Roundtrip(Header.MaxStreamId, PacketType.NewStream);
        }

        void Roundtrip(long id, PacketType type)
        {
            var header1 = new Header { StreamId = id, PacketType = type };
            var ms = new MemoryStream();
            header1.WriteAsync(ms).Wait();
            ms.Position = 0;
            var header2 = Header.ReadAsync(ms).Result;
            Assert.AreEqual(header1.StreamId, header2.StreamId);
            Assert.AreEqual(header1.PacketType, header2.PacketType);
        }
    }
}
