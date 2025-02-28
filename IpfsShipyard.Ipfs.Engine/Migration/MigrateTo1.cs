﻿using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;

namespace IpfsShipyard.Ipfs.Engine.Migration;

internal class MigrateTo1 : IMigration
{
    public int Version => 1;

    public bool CanUpgrade => true;

    public bool CanDowngrade => true;

    public async Task DowngradeAsync(IpfsEngine ipfs, CancellationToken cancel = default)
    {
        var path = Path.Combine(ipfs.Options.Repository.Folder, "pins");
        var folder = new DirectoryInfo(path);
        if (!folder.Exists)
        {
            return;
        }

        var store = new FileStore<Cid, Pin1>
        {
            Folder = path,
            NameToKey = cid => cid.Hash.ToBase32(),
            KeyToName = key => new MultiHash(key.FromBase32())
        };

        var files = folder.EnumerateFiles()
            .Where(fi => fi.Length != 0);
        foreach (var fi in files)
        {
            try
            {
                var name = store.KeyToName(fi.Name);
                var pin = await store.GetAsync(name, cancel).ConfigureAwait(false);
                File.Create(Path.Combine(store.Folder, pin.Id));
                File.Delete(store.GetPath(name));
            }
            catch
            {
                // ignored
            }
        }
    }

    public async Task UpgradeAsync(IpfsEngine ipfs, CancellationToken cancel = default)
    {
        var path = Path.Combine(ipfs.Options.Repository.Folder, "pins");
        var folder = new DirectoryInfo(path);
        if (!folder.Exists)
        {
            return;
        }

        var store = new FileStore<Cid, Pin1>
        {
            Folder = path,
            NameToKey = cid => cid.Hash.ToBase32(),
            KeyToName = key => new MultiHash(key.FromBase32())
        };

        var files = folder.EnumerateFiles()
            .Where(fi => fi.Length == 0);
        foreach (var fi in files)
        {
            try
            {
                var cid = Cid.Decode(fi.Name);
                await store.PutAsync(cid, new() { Id = cid }, cancel).ConfigureAwait(false);
                File.Delete(fi.FullName);
            }
            catch
            {
                // ignored
            }
        }
    }

    private class Pin1
    {
        public Cid Id;
    }
}