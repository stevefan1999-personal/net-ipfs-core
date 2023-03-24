﻿using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.Ipfs.Http.Tests.CoreApi;

[TestClass]
public class PinApiTest
{
    [TestMethod]
    public async Task List()
    {
        var ipfs = TestFixture.Ipfs;
        var pins = await ipfs.Pin.ListAsync();
        Assert.IsNotNull(pins);
        Assert.IsTrue(pins.Any());
    }

    [TestMethod]
    public async Task Add_Remove()
    {
        var ipfs = TestFixture.Ipfs;
        var result = await ipfs.FileSystem.AddTextAsync("I am pinned");
        var id = result.Id;

        var pins = await ipfs.Pin.AddAsync(id);
        Assert.IsTrue(pins.Any(pin => pin == id));
        var all = await ipfs.Pin.ListAsync();
        Assert.IsTrue(all.Any(pin => pin == id));

        pins = await ipfs.Pin.RemoveAsync(id);
        Assert.IsTrue(pins.Any(pin => pin == id));
        all = await ipfs.Pin.ListAsync();
        Assert.IsFalse(all.Any(pin => pin == id));
    }

}