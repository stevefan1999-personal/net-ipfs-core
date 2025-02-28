﻿using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IpfsShipyard.Ipfs.Http.Tests.CoreApi
{
    [TestClass]
    public class ObjectApiTest
    {
        private IpfsClient ipfs = TestFixture.Ipfs;

        [TestMethod]
        public async Task New_Template_Null()
        {
            var node = await ipfs.Object.NewAsync();
            Assert.AreEqual("QmdfTbBqBPQ7VNxZEYEj14VmRuZBkqFbiwReogJgS1zR1n", (string)node.Id);
        }

        [TestMethod]
        public async Task New_Template_UnixfsDir()
        {
            var node = await ipfs.Object.NewAsync("unixfs-dir");
            Assert.AreEqual("QmUNLLsPACCz1vLxQVkXqqLX5R1X345qqfHbsf67hvA3Nn", (string)node.Id);

            node = await ipfs.Object.NewDirectoryAsync();
            Assert.AreEqual("QmUNLLsPACCz1vLxQVkXqqLX5R1X345qqfHbsf67hvA3Nn", (string)node.Id);

        }

        [TestMethod]
        public async Task Put_Get_Dag()
        {
            var adata = Encoding.UTF8.GetBytes("alpha");
            var bdata = Encoding.UTF8.GetBytes("beta");
            var alpha = new DagNode(adata);
            var beta = new DagNode(bdata, new[] { alpha.ToLink() });
            var x = await ipfs.Object.PutAsync(beta);
            var node = await ipfs.Object.GetAsync(x.Id);
            CollectionAssert.AreEqual(beta.DataBytes, node.DataBytes);
            Assert.AreEqual(beta.Links.Count(), Enumerable.Count<IMerkleLink>(node.Links));
            Assert.AreEqual(beta.Links.First().Id, Enumerable.First<IMerkleLink>(node.Links).Id);
            Assert.AreEqual(beta.Links.First().Name, Enumerable.First<IMerkleLink>(node.Links).Name);
            Assert.AreEqual(beta.Links.First().Size, Enumerable.First<IMerkleLink>(node.Links).Size);
        }

        [TestMethod]
        public async Task Put_Get_Data()
        {
            var adata = Encoding.UTF8.GetBytes("alpha");
            var bdata = Encoding.UTF8.GetBytes("beta");
            var alpha = new DagNode(adata);
            var beta = await ipfs.Object.PutAsync(bdata, new[] { alpha.ToLink() });
            var node = await ipfs.Object.GetAsync(beta.Id);
            CollectionAssert.AreEqual(beta.DataBytes, node.DataBytes);
            Assert.AreEqual(Enumerable.Count<IMerkleLink>(beta.Links), Enumerable.Count<IMerkleLink>(node.Links));
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Id, Enumerable.First<IMerkleLink>(node.Links).Id);
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Name, Enumerable.First<IMerkleLink>(node.Links).Name);
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Size, Enumerable.First<IMerkleLink>(node.Links).Size);
        }

        [TestMethod]
        public async Task Data()
        {
            var adata = Encoding.UTF8.GetBytes("alpha");
            var node = await ipfs.Object.PutAsync(adata);
            using (var stream = await ipfs.Object.DataAsync(node.Id))
            {
                var bdata = new byte[adata.Length];
                stream.Read(bdata, 0, bdata.Length);
                CollectionAssert.AreEqual(adata, bdata);
            }
        }

        [TestMethod]
        public async Task Links()
        {
            var adata = Encoding.UTF8.GetBytes("alpha");
            var bdata = Encoding.UTF8.GetBytes("beta");
            var alpha = new DagNode(adata);
            var beta = await ipfs.Object.PutAsync(bdata, new[] { alpha.ToLink() });
            var links = await ipfs.Object.LinksAsync(beta.Id);
            Assert.AreEqual(Enumerable.Count<IMerkleLink>(beta.Links), Enumerable.Count<IMerkleLink>(links));
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Id, Enumerable.First<IMerkleLink>(links).Id);
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Name, Enumerable.First<IMerkleLink>(links).Name);
            Assert.AreEqual(Enumerable.First<IMerkleLink>(beta.Links).Size, Enumerable.First<IMerkleLink>(links).Size);
        }

        [TestMethod]
        public async Task Stat()
        {
            var data1 = Encoding.UTF8.GetBytes("Some data 1");
            var data2 = Encoding.UTF8.GetBytes("Some data 2");
            var node2 = new DagNode(data2);
            var node1 = await ipfs.Object.PutAsync(data1,
                new[] { node2.ToLink("some-link") });
            var info = await ipfs.Object.StatAsync(node1.Id);
            Assert.AreEqual<int>(1, info.LinkCount);
            Assert.AreEqual<long>(64, info.BlockSize);
            Assert.AreEqual<long>(53, info.LinkSize);
            Assert.AreEqual<long>(11, info.DataSize);
            Assert.AreEqual<long>(77, info.CumulativeSize);
        }

        [TestMethod]
        public async Task Get_Nonexistent()
        {
            var data = Encoding.UTF8.GetBytes("Some data for net-ipfs-http-client-test that cannot be found");
            var node = new DagNode(data);
            var id = node.Id;
            var cs = new CancellationTokenSource(500);
            try
            {
                var _ = await ipfs.Object.GetAsync(id, cs.Token);
                Assert.Fail("Did not throw TaskCanceledException");
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

    }
}
