using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.Ipfs.Engine.Tests;

[TestClass]
public class RandomWalkTest
{
    [TestMethod]
    public async Task CanStartAndStop()
    {
        var walk = new RandomWalk();
        await walk.StartAsync();
        await walk.StopAsync();

        await walk.StartAsync();
        await walk.StopAsync();
    }

    [TestMethod]
    public async Task CannotStartTwice()
    {
        var walk = new RandomWalk();
        await walk.StartAsync();
        ExceptionAssert.Throws<Exception>(() => walk.StartAsync().GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task CanStopMultipletimes()
    {
        var walk = new RandomWalk();
        await walk.StartAsync();
        await walk.StopAsync();
        await walk.StopAsync();
        await walk.StartAsync();
        await walk.StopAsync();
    }
}