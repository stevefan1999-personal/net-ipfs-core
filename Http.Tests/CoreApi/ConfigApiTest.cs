﻿using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace IpfsShipyard.Ipfs.Http.Tests.CoreApi;

[TestClass]
public class ConfigApiTest
{
    private const string ApiAddress = "/ip4/127.0.0.1/tcp/";
    private const string GatewayAddress = "/ip4/127.0.0.1/tcp/";

    [TestMethod]
    public async Task Get_Entire_Config()
    {
        var ipfs = TestFixture.Ipfs;
        var config = await ipfs.Config.GetAsync();
        StringAssert.StartsWith(config["Addresses"]["API"].Value<string>(), ApiAddress);
    }

    [TestMethod]
    public async Task Get_Scalar_Key_Value()
    {
        var ipfs = TestFixture.Ipfs;
        var api = await ipfs.Config.GetAsync("Addresses.API");
        StringAssert.StartsWith(api.Value<string>(), ApiAddress);
    }

    [TestMethod]
    public async Task Get_Object_Key_Value()
    {
        var ipfs = TestFixture.Ipfs;
        var addresses = await ipfs.Config.GetAsync("Addresses");
        StringAssert.StartsWith(addresses["API"].Value<string>(), ApiAddress);
        StringAssert.StartsWith(addresses["Gateway"].Value<string>(), GatewayAddress);
    }

    [TestMethod]
    public async Task Keys_are_Case_Sensitive()
    {
        var ipfs = TestFixture.Ipfs;
        var api = await ipfs.Config.GetAsync("Addresses.API");
        StringAssert.StartsWith(api.Value<string>(), ApiAddress);

        ExceptionAssert.Throws<Exception>(() => { var x = ipfs.Config.GetAsync("Addresses.api").GetAwaiter().GetResult(); });
    }

    [TestMethod]
    public async Task Set_String_Value()
    {
        const string key = "foo";
        const string value = "foobar";
        var ipfs = TestFixture.Ipfs;
        await ipfs.Config.SetAsync(key, value);
        Assert.AreEqual(value, await ipfs.Config.GetAsync(key));
    }

    [TestMethod]
    public async Task Set_JSON_Value()
    {
        const string key = "API.HTTPHeaders.Access-Control-Allow-Origin";
        var value = JToken.Parse("['http://example.io']");
        var ipfs = TestFixture.Ipfs;
        await ipfs.Config.SetAsync(key, value);
        Assert.AreEqual("http://example.io", (await ipfs.Config.GetAsync(key))[0]);
    }

    [TestMethod]
    public async Task Replace_Entire_Config()
    {
        var ipfs = TestFixture.Ipfs;
        var original = await ipfs.Config.GetAsync();
        try
        {
            var a = JObject.Parse("{ \"foo-x-bar\": 1 }");
            await ipfs.Config.ReplaceAsync(a);
        }
        finally
        {
            await ipfs.Config.ReplaceAsync(original);
        }
    }

}