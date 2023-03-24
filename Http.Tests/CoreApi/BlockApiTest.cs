using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.Ipfs.Http.Tests.CoreApi;

[TestClass]
public class BlockApiTest
{
    private readonly IpfsClient _ipfs = TestFixture.Ipfs;
    private readonly string _id = "QmPv52ekjS75L4JmHpXVeuJ5uX2ecSfSZo88NSyxwA3rAQ";
    private readonly byte[] _blob = "blorb"u8.ToArray();

    [TestMethod]
    public async Task Put_Bytes()
    {
        var cid = await _ipfs.Block.PutAsync(_blob);
        Assert.AreEqual(_id, (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Bytes_ContentType()
    {
        var cid = await _ipfs.Block.PutAsync(_blob, contentType: "raw");
        Assert.AreEqual("bafkreiaxnnnb7qz2focittuqq3ya25q7rcv3bqynnczfzako47346wosmu", (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Bytes_Hash()
    {
        var cid = await _ipfs.Block.PutAsync(_blob, "raw", "sha2-512");
        Assert.AreEqual("bafkrgqelljziv4qfg5mefz36m2y3h6voaralnw6lwb4f53xcnrf4mlsykkn7vt6eno547tw5ygcz62kxrle45wnbmpbofo5tvu57jvuaf7k7e", (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Bytes_Pinned()
    {
        var data1 = new byte[] { 23, 24, 127 };
        var cid1 = await _ipfs.Block.PutAsync(data1, contentType: "raw", pin: true);
        var pins = await _ipfs.Pin.ListAsync();
        Assert.IsTrue(pins.Any(pin => pin == cid1));

        var data2 = new byte[] { 123, 124, 27 };
        var cid2 = await _ipfs.Block.PutAsync(data2, contentType: "raw", pin: false);
        pins = await _ipfs.Pin.ListAsync();
        Assert.IsFalse(pins.Any(pin => pin == cid2));
    }

    [TestMethod]
    public async Task Put_Stream()
    {
        var cid = await _ipfs.Block.PutAsync(new MemoryStream(_blob));
        Assert.AreEqual(_id, (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Stream_ContentType()
    {
        var cid = await _ipfs.Block.PutAsync(new MemoryStream(_blob), contentType: "raw");
        Assert.AreEqual("bafkreiaxnnnb7qz2focittuqq3ya25q7rcv3bqynnczfzako47346wosmu", (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Stream_Hash()
    {
        var cid = await _ipfs.Block.PutAsync(new MemoryStream(_blob), "raw", "sha2-512");
        Assert.AreEqual("bafkrgqelljziv4qfg5mefz36m2y3h6voaralnw6lwb4f53xcnrf4mlsykkn7vt6eno547tw5ygcz62kxrle45wnbmpbofo5tvu57jvuaf7k7e", (string)cid);

        var data = await _ipfs.Block.GetAsync(cid);
        Assert.AreEqual(_blob.Length, data.Size);
        CollectionAssert.AreEqual(_blob, data.DataBytes);
    }

    [TestMethod]
    public async Task Put_Stream_Pinned()
    {
        var data1 = new MemoryStream(new byte[] { 23, 24, 127 });
        var cid1 = await _ipfs.Block.PutAsync(data1, contentType: "raw", pin: true);
        var pins = await _ipfs.Pin.ListAsync();
        Assert.IsTrue(pins.Any(pin => pin == cid1));

        var data2 = new MemoryStream(new byte[] { 123, 124, 27 });
        var cid2 = await _ipfs.Block.PutAsync(data2, contentType: "raw", pin: false);
        pins = await _ipfs.Pin.ListAsync();
        Assert.IsFalse(pins.Any(pin => pin == cid2));
    }

    [TestMethod]
    public async Task Get()
    {
        await _ipfs.Block.PutAsync(_blob);
        var block = await _ipfs.Block.GetAsync(_id);
        Assert.AreEqual(_id, (string)block.Id);
        CollectionAssert.AreEqual(_blob, block.DataBytes);
        var blob1 = new byte[_blob.Length];
        block.DataStream.Read(blob1, 0, blob1.Length);
        CollectionAssert.AreEqual(_blob, blob1);
    }

    [TestMethod]
    public async Task Stat()
    {
        var _ = await _ipfs.Block.PutAsync(_blob);
        var info = await _ipfs.Block.StatAsync(_id);
        Assert.AreEqual(_id, (string)info.Id);
        Assert.AreEqual(5, info.Size);
    }

    [TestMethod]
    public async Task Remove()
    {
        await _ipfs.Block.PutAsync(_blob);
        var cid = await _ipfs.Block.RemoveAsync(_id);
        Assert.AreEqual(_id, (string)cid);
    }

    [TestMethod]
    public void Remove_Unknown()
    {
        ExceptionAssert.Throws<Exception>(() => _ipfs.Block.RemoveAsync("QmPv52ekjS75L4JmHpXVeuJ5uX2ecSfSZo88NSyxwA3rFF").GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task Remove_Unknown_OK()
    {
        var cid = await _ipfs.Block.RemoveAsync("QmPv52ekjS75L4JmHpXVeuJ5uX2ecSfSZo88NSyxwA3rFF", true);
    }

}