﻿using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509.Extension;

namespace IpfsShipyard.Ipfs.Engine.Tests.Cryptography;

[TestClass]
public class CertTest
{
    [TestMethod]
    public async Task Create_Rsa()
    {
        var ipfs = TestFixture.Ipfs;
        var keychain = await ipfs.KeyChainAsync();
        var key = await ipfs.Key.CreateAsync("alice", "rsa", 512);
        try
        {
            var cert = await keychain.CreateBcCertificateAsync(key.Name);
            Assert.AreEqual($"CN={key.Id},OU=keystore,O=ipfs", cert.SubjectDN.ToString());
            var ski = new SubjectKeyIdentifierStructure(cert.GetExtensionValue(X509Extensions.SubjectKeyIdentifier));
            Assert.AreEqual(key.Id.ToBase58(), ski.GetKeyIdentifier().ToBase58());
        }
        finally
        {
            await ipfs.Key.RemoveAsync("alice");
        }
    }

    [TestMethod]
    public async Task Create_Secp256k1()
    {
        var ipfs = TestFixture.Ipfs;
        var keychain = await ipfs.KeyChainAsync();
        var key = await ipfs.Key.CreateAsync("alice", "secp256k1", 0);
        try
        {
            var cert = await keychain.CreateBcCertificateAsync("alice");
            Assert.AreEqual($"CN={key.Id},OU=keystore,O=ipfs", cert.SubjectDN.ToString());
            var ski = new SubjectKeyIdentifierStructure(cert.GetExtensionValue(X509Extensions.SubjectKeyIdentifier));
            Assert.AreEqual(key.Id.ToBase58(), ski.GetKeyIdentifier().ToBase58());
        }
        finally
        {
            await ipfs.Key.RemoveAsync("alice");
        }
    }

    [TestMethod]
    public async Task Create_Ed25519()
    {
        var ipfs = TestFixture.Ipfs;
        var keychain = await ipfs.KeyChainAsync();
        var key = await ipfs.Key.CreateAsync("alice", "ed25519", 0);
        try
        {
            var cert = await keychain.CreateBcCertificateAsync("alice");
            Assert.AreEqual($"CN={key.Id},OU=keystore,O=ipfs", cert.SubjectDN.ToString());
            var ski = new SubjectKeyIdentifierStructure(cert.GetExtensionValue(X509Extensions.SubjectKeyIdentifier));
            Assert.AreEqual(key.Id.ToBase58(), ski.GetKeyIdentifier().ToBase58());
        }
        finally
        {
            await ipfs.Key.RemoveAsync("alice");
        }
    }
}