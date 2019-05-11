﻿using Ipfs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerTalk.PubSub
{
    
    [TestClass]
    public class NotificationServiceTest
    {
        Peer self = new Peer
        {
            AgentVersion = "self",
            Id = "QmXK9VBxaXFuuT29AaPUTgW3jBWZ9JgLVZYdMYTHC6LLAH",
            PublicKey = "CAASXjBcMA0GCSqGSIb3DQEBAQUAA0sAMEgCQQCC5r4nQBtnd9qgjnG8fBN5+gnqIeWEIcUFUdCG4su/vrbQ1py8XGKNUBuDjkyTv25Gd3hlrtNJV3eOKZVSL8ePAgMBAAE="
        };

        [TestMethod]
        public async Task MessageID_Increments()
        {
            var ns = new NotificationService { LocalPeer = self };
            await ns.StartAsync();
            try
            {
                var a = ns.CreateMessage("topic", new byte[0]);
                var b = ns.CreateMessage("topic", new byte[0]);
                Assert.IsTrue(b.MessageId.CompareTo(a.MessageId) > 0);
            }
            finally
            {
                await ns.StopAsync();
            }
        }

        [TestMethod]
        public async Task Publish()
        {
            var ns = new NotificationService { LocalPeer = self };
            await ns.StartAsync();
            try
            {
                await ns.PublishAsync("topic", "foo");
                await ns.PublishAsync("topic", new byte[] { 1, 2, 3 });
                await ns.PublishAsync("topic", new MemoryStream(new byte[] { 1, 2, 3 }));
                Assert.AreEqual(3ul, ns.MesssagesPublished);
                Assert.AreEqual(3ul, ns.MesssagesReceived);
            }
            finally
            {
                await ns.StopAsync();
            }
        }

        [TestMethod]
        public async Task Topics()
        {
            var ns = new NotificationService { LocalPeer = self };
            await ns.StartAsync();
            try
            {
                var topicA = Guid.NewGuid().ToString();
                var topicB = Guid.NewGuid().ToString();
                var csA = new CancellationTokenSource();
                var csB = new CancellationTokenSource();

                await ns.SubscribeAsync(topicA, msg => { }, csA.Token);
                await ns.SubscribeAsync(topicA, msg => { }, csA.Token);
                await ns.SubscribeAsync(topicB, msg => { }, csB.Token);

                var topics = (await ns.SubscribedTopicsAsync()).ToArray();
                Assert.AreEqual(2, topics.Count());
                CollectionAssert.Contains(topics, topicA);
                CollectionAssert.Contains(topics, topicB);

                csA.Cancel();
                topics = (await ns.SubscribedTopicsAsync()).ToArray();
                Assert.AreEqual(1, topics.Count());
                CollectionAssert.Contains(topics, topicB);

                csB.Cancel();
                topics = (await ns.SubscribedTopicsAsync()).ToArray();
                Assert.AreEqual(0, topics.Count());
            }
            finally
            {
                await ns.StopAsync();
            }
        }

        [TestMethod]
        public async Task Subscribe()
        {
            var ns = new NotificationService { LocalPeer = self };
            await ns.StartAsync();
            try
            {
                var topic = Guid.NewGuid().ToString();
                var cs = new CancellationTokenSource();
                int messageCount = 0;
                await ns.SubscribeAsync(topic, msg => { ++messageCount; }, cs.Token);
                await ns.SubscribeAsync(topic, msg => { ++messageCount; }, cs.Token);

                await ns.PublishAsync(topic, "");
                Assert.AreEqual(2, messageCount);
            }
            finally
            {
                await ns.StopAsync();
            }
        }

        [TestMethod]
        public async Task Subscribe_HandlerExceptionIsIgnored()
        {
            var ns = new NotificationService { LocalPeer = self };
            await ns.StartAsync();
            try
            {
                var topic = Guid.NewGuid().ToString();
                var cs = new CancellationTokenSource();
                int messageCount = 0;
                await ns.SubscribeAsync(topic, msg => { ++messageCount; throw new Exception(); }, cs.Token);

                await ns.PublishAsync(topic, "");
                Assert.AreEqual(1, messageCount);
            }
            finally
            {
                await ns.StopAsync();
            }
        }
    }
}
