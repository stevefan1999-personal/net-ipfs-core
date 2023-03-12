﻿using IpfsShipyard.Ipfs.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.PeerTalk.Tests;

[TestClass]
public class MultiAddressWhiteListTest
{
    readonly MultiAddress _a = "/ipfs/QmSoLMeWqB7YGVLJN3pNLQpmmEk35v6wYtsMGLzSr5QBU3";
    readonly MultiAddress _a1 = "/ip4/127.0.0.1/ipfs/QmSoLMeWqB7YGVLJN3pNLQpmmEk35v6wYtsMGLzSr5QBU3";
    readonly MultiAddress _b = "/p2p/QmSoLMeWqB7YGVLJN3pNLQpmmEk35v6wYtsMGLzSr5QBU3";
    readonly MultiAddress _c = "/ipfs/QmSoLV4Bbm51jM9C4gDYZQ9Cy3U6aXMJDAbzgu2fzaDs64";
    readonly MultiAddress _d = "/p2p/QmSoLV4Bbm51jM9C4gDYZQ9Cy3U6aXMJDAbzgu2fzaDs64";

    [TestMethod]
    public void Allowed()
    {
        var policy = new MultiAddressWhiteList();
        policy.Add(_a);
        policy.Add(_b);
        Assert.IsTrue(policy.IsAllowed(_a));
        Assert.IsTrue(policy.IsAllowed(_a1));
        Assert.IsTrue(policy.IsAllowed(_b));
        Assert.IsFalse(policy.IsAllowed(_c));
        Assert.IsFalse(policy.IsAllowed(_d));
    }

    [TestMethod]
    public void Allowed_Alias()
    {
        var policy = new MultiAddressWhiteList();
        policy.Add(_a);
        Assert.IsTrue(policy.IsAllowed(_a));
        Assert.IsTrue(policy.IsAllowed(_a1));
        Assert.IsTrue(policy.IsAllowed(_b));
        Assert.IsFalse(policy.IsAllowed(_c));
        Assert.IsFalse(policy.IsAllowed(_d));
    }

    [TestMethod]
    public void Empty()
    {
        var policy = new MultiAddressWhiteList();
        Assert.IsTrue(policy.IsAllowed(_a));
    }

    [TestMethod]
    public void Collection()
    {
        MultiAddress a = "/ip4/127.0.0.1";
        MultiAddress b = "/ip4/127.0.0.2";

        var policy = new MultiAddressWhiteList();
        Assert.IsFalse(policy.IsReadOnly);
        Assert.AreEqual(0, policy.Count);
        Assert.IsFalse(policy.Contains(a));
        Assert.IsFalse(policy.Contains(b));

        policy.Add(a);
        Assert.AreEqual(1, policy.Count);
        Assert.IsTrue(policy.Contains(a));
        Assert.IsFalse(policy.Contains(b));

        policy.Add(a);
        Assert.AreEqual(1, policy.Count);
        Assert.IsTrue(policy.Contains(a));
        Assert.IsFalse(policy.Contains(b));

        policy.Add(b);
        Assert.AreEqual(2, policy.Count);
        Assert.IsTrue(policy.Contains(a));
        Assert.IsTrue(policy.Contains(b));

        policy.Remove(b);
        Assert.AreEqual(1, policy.Count);
        Assert.IsTrue(policy.Contains(a));
        Assert.IsFalse(policy.Contains(b));

        var array = new MultiAddress[1];
        policy.CopyTo(array, 0);
        Assert.AreSame(a, array[0]);

        foreach (var filter in policy)
        {
            Assert.AreSame(a, filter);
        }

        policy.Clear();
        Assert.AreEqual(0, policy.Count);
        Assert.IsFalse(policy.Contains(a));
        Assert.IsFalse(policy.Contains(b));
    }
}