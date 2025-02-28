﻿using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;
using IpfsShipyard.PeerTalk.Protocols;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.PeerTalk.Tests.Protocols
{
    [TestClass]
    public class Identitfy1Test
    {
        [TestMethod]
        public async Task RoundTrip()
        {
            var peerA = new Peer
            {
                Addresses = new MultiAddress[]
                {
                    "/ip4/127.0.0.1/tcp/4002/ipfs/QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb"
                },
                AgentVersion = "agent/1",
                Id = "QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb",
                ProtocolVersion = "protocol/1",
                PublicKey = "CAASpgIwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCfBYU9c0n28u02N/XCJY8yIsRqRVO5Zw+6kDHCremt2flHT4AaWnwGLAG9YyQJbRTvWN9nW2LK7Pv3uoIlvUSTnZEP0SXB5oZeqtxUdi6tuvcyqTIfsUSanLQucYITq8Qw3IMBzk+KpWNm98g9A/Xy30MkUS8mrBIO9pHmIZa55fvclDkTvLxjnGWA2avaBfJvHgMSTu0D2CQcmJrvwyKMhLCSIbQewZd2V7vc6gtxbRovKlrIwDTmDBXbfjbLljOuzg2yBLyYxXlozO9blpttbnOpU4kTspUVJXglmjsv7YSIJS3UKt3544l/srHbqlwC5CgOgjlwNfYPadO8kmBfAgMBAAE="
            };
            var peerB = new Peer();
            var ms = new MemoryStream();
            var connection = new PeerConnection
            {
                LocalPeer = peerA,
                RemotePeer = peerB,
                Stream = ms
            };

            // Generate identify msg.
            var identify = new Identify1();
            await identify.ProcessMessageAsync(connection, ms);

            // Process identify msg.
            ms.Position = 0;
            await identify.UpdateRemotePeerAsync(peerB, ms, CancellationToken.None);

            Assert.AreEqual(peerA.AgentVersion, peerB.AgentVersion);
            Assert.AreEqual(peerA.Id, peerB.Id);
            Assert.AreEqual(peerA.ProtocolVersion, peerB.ProtocolVersion);
            Assert.AreEqual(peerA.PublicKey, peerB.PublicKey);
            CollectionAssert.AreEqual(peerA.Addresses.ToArray(), peerB.Addresses.ToArray());
        }

        [TestMethod]
        public async Task InvalidPublicKey()
        {
            var peerA = new Peer
            {
                Addresses = new MultiAddress[]
                {
                    "/ip4/127.0.0.1/tcp/4002/ipfs/QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb"
                },
                AgentVersion = "agent/1",
                Id = "QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb",
                ProtocolVersion = "protocol/1",
                PublicKey = "BADSpgIwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQCfBYU9c0n28u02N/XCJY8yIsRqRVO5Zw+6kDHCremt2flHT4AaWnwGLAG9YyQJbRTvWN9nW2LK7Pv3uoIlvUSTnZEP0SXB5oZeqtxUdi6tuvcyqTIfsUSanLQucYITq8Qw3IMBzk+KpWNm98g9A/Xy30MkUS8mrBIO9pHmIZa55fvclDkTvLxjnGWA2avaBfJvHgMSTu0D2CQcmJrvwyKMhLCSIbQewZd2V7vc6gtxbRovKlrIwDTmDBXbfjbLljOuzg2yBLyYxXlozO9blpttbnOpU4kTspUVJXglmjsv7YSIJS3UKt3544l/srHbqlwC5CgOgjlwNfYPadO8kmBfAgMBAAE="
            };
            var peerB = new Peer
            {
                Id = peerA.Id
            };
            var ms = new MemoryStream();
            var connection = new PeerConnection
            {
                LocalPeer = peerA,
                RemotePeer = peerB,
                Stream = ms
            };

            // Generate identify msg.
            var identify = new Identify1();
            await identify.ProcessMessageAsync(connection, ms);

            // Process identify msg.
            ms.Position = 0;
            ExceptionAssert.Throws<InvalidDataException>(() =>
            {
                identify.UpdateRemotePeerAsync(peerB, ms, CancellationToken.None).Wait();
            });
        }

        [TestMethod]
        public async Task MustHavePublicKey()
        {
            var peerA = new Peer
            {
                Addresses = new MultiAddress[]
                {
                    "/ip4/127.0.0.1/tcp/4002/ipfs/QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb"
                },
                AgentVersion = "agent/1",
                Id = "QmXFX2P5ammdmXQgfqGkfswtEVFsZUJ5KeHRXQYCTdiTAb",
                ProtocolVersion = "protocol/1",
                PublicKey = ""
            };
            var peerB = new Peer
            {
                Id = peerA.Id
            };
            var ms = new MemoryStream();
            var connection = new PeerConnection
            {
                LocalPeer = peerA,
                RemotePeer = peerB,
                Stream = ms
            };

            // Generate identify msg.
            var identify = new Identify1();
            await identify.ProcessMessageAsync(connection, ms);

            // Process identify msg.
            ms.Position = 0;
            ExceptionAssert.Throws<InvalidDataException>(() =>
            {
                identify.UpdateRemotePeerAsync(peerB, ms, CancellationToken.None).Wait();
            });
        }
    }
}
