﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;
using IpfsShipyard.PeerTalk.Transports;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.PeerTalk.Tests.Transports;

[TestClass]
public class UdpTest
{

    [TestMethod]
    public void Connect_Missing_UDP_Port()
    {
        var udp = new Udp();
        ExceptionAssert.Throws<Exception>(() =>
        {
            var _ = udp.ConnectAsync("/ip4/127.0.0.1/tcp/32700").Result;
        });
        ExceptionAssert.Throws<Exception>(() =>
        {
            var _ = udp.ConnectAsync("/ip4/127.0.0.1").Result;
        });
    }

    [TestMethod]
    public void Connect_Missing_IP_Address()
    {
        var udp = new Udp();
        ExceptionAssert.Throws<Exception>(() =>
        {
            var _ = udp.ConnectAsync("/udp/32700").Result;
        });
    }

    [TestMethod]
    public void Connect_Cancelled()
    {
        var udp = new Udp();
        var cs = new CancellationTokenSource();
        cs.Cancel();
        ExceptionAssert.Throws<OperationCanceledException>(() =>
        {
            var stream = udp.ConnectAsync("/ip4/127.0.10.10/udp/32700", cs.Token).Result;
        });
    }

    [TestMethod]
    [Ignore]
    public async Task Listen()
    {
        var udp = new Udp();
        var cs = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var connected = false;
        Action<Stream, MultiAddress, MultiAddress> handler = (stream, _, _) =>
        {
            Assert.IsNotNull(stream);
            connected = true;
        };
        try
        {
            var listenerAddress = udp.Listen("/ip4/127.0.0.1", handler, cs.Token);
            Assert.IsTrue(listenerAddress.Protocols.Any(p => p.Name == "udp"));
            await using var stream = await udp.ConnectAsync(listenerAddress, cs.Token);
            await Task.Delay(50, cs.Token);
            Assert.IsNotNull(stream);
            Assert.IsTrue(connected);
        }
        finally
        {
            cs.Cancel();
        }
    }

    [TestMethod]
    [Ignore("Sometimes fails")]
    public async Task NetworkTimeProtocol()
    {
        var cs = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var server = await new MultiAddress("/dns4/time.windows.com/udp/123").ResolveAsync(cs.Token);
        var ntpData = new byte[48];
        ntpData[0] = 0x1B;

        var udp = new Udp();
        await using var time = await udp.ConnectAsync(server[0], cs.Token);
        ntpData[0] = 0x1B;
        await time.WriteAsync(ntpData, 0, ntpData.Length, cs.Token);
        await time.FlushAsync(cs.Token);
        await time.ReadAsync(ntpData, 0, ntpData.Length, cs.Token);
        Assert.AreNotEqual(0x1B, ntpData[0]);

        Array.Clear(ntpData, 0, ntpData.Length);
        ntpData[0] = 0x1B;
        await time.WriteAsync(ntpData, 0, ntpData.Length, cs.Token);
        await time.FlushAsync(cs.Token);
        await time.ReadAsync(ntpData, 0, ntpData.Length, cs.Token);
        Assert.AreNotEqual(0x1B, ntpData[0]);
    }

    [TestMethod]
    [Ignore]
    public async Task SendReceive()
    {
        var cs = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var udp = new Udp();
        using var server = new HelloServer();
        await using var stream = await udp.ConnectAsync(server.Address, cs.Token);
        var bytes = new byte[5];
        await stream.ReadAsync(bytes, 0, bytes.Length, cs.Token);
        Assert.AreEqual("hello", Encoding.UTF8.GetString(bytes));
    }

    private class HelloServer : IDisposable
    {
        private readonly CancellationTokenSource _cs = new(TimeSpan.FromSeconds(30));

        public HelloServer()
        {
            var udp = new Udp();
            Address = udp.Listen("/ip4/127.0.0.1", Handler, _cs.Token);
        }

        public MultiAddress Address { get; }

        public void Dispose()
        {
            _cs.Cancel();
        }

        private void Handler(Stream stream, MultiAddress local, MultiAddress remote)
        {
            var msg = "hello"u8.ToArray();
            stream.Write(msg, 0, msg.Length);
            stream.Flush();
            stream.Dispose();
        }
    }
}