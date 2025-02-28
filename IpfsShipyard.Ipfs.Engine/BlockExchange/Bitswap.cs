﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using IpfsShipyard.Ipfs.Core;
using IpfsShipyard.Ipfs.Core.CoreApi;
using IpfsShipyard.PeerTalk;

namespace IpfsShipyard.Ipfs.Engine.BlockExchange;

/// <summary>
///     Exchange blocks with other peers.
/// </summary>
public class Bitswap : IService
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Bitswap));
    private readonly ConcurrentDictionary<Peer, BitswapLedger> _peerLedgers = new();

    private readonly ConcurrentDictionary<Cid, WantedBlock> _wants = new();

    /// <summary>
    ///     The number of blocks sent by other peers.
    /// </summary>
    private ulong _blocksReceived;

    /// <summary>
    ///     The number of blocks sent to other peers.
    /// </summary>
    private ulong _blocksSent;

    /// <summary>
    ///     The number of bytes sent by other peers.
    /// </summary>
    private ulong _dataReceived;

    /// <summary>
    ///     The number of bytes sent to other peers.
    /// </summary>
    private ulong _dataSent;

    /// <summary>
    ///     The number of duplicate blocks sent by other peers.
    /// </summary>
    /// <remarks>
    ///     A duplicate block is a block that is already stored in the
    ///     local repository.
    /// </remarks>
    private ulong _dupBlksReceived;

    /// <summary>
    ///     The number of duplicate bytes sent by other peers.
    /// </summary>
    /// <remarks>
    ///     A duplicate block is a block that is already stored in the
    ///     local repository.
    /// </remarks>
    private ulong _dupDataReceived;

    /// <summary>
    ///     The supported bitswap protocols.
    /// </summary>
    /// <value>
    ///     Defaults to <see cref="Bitswap11" /> and <see cref="Bitswap1" />.
    /// </value>
    public IBitswapProtocol[] Protocols;

    /// <summary>
    ///     Creates a new instance of the <see cref="Bitswap" /> class.
    /// </summary>
    public Bitswap()
    {
        Protocols = new IBitswapProtocol[]
        {
            new Bitswap11 { Bitswap = this },
            new Bitswap1 { Bitswap = this }
        };
    }

    /// <summary>
    ///     Provides access to other peers.
    /// </summary>
    public Swarm Swarm { get; set; }

    /// <summary>
    ///     Provides access to blocks of data.
    /// </summary>
    public IBlockApi BlockService { get; set; }

    /// <summary>
    ///     Statistics on the bitswap component.
    /// </summary>
    /// <seealso cref="IStatsApi" />
    public BitswapData Statistics
    {
        get
        {
            return new()
            {
                BlocksReceived = _blocksReceived,
                BlocksSent = _blocksSent,
                DataReceived = _dataReceived,
                DataSent = _dataSent,
                DupBlksReceived = _dupBlksReceived,
                DupDataReceived = _dupDataReceived,
                ProvideBufLen = 0, // TODO: Unknown meaning
                Peers = Swarm.KnownPeers.Select(p => p.Id),
                Wantlist = _wants.Keys
            };
        }
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        Log.Debug("Starting");

        foreach (var protocol in Protocols)
        {
            Swarm.AddProtocol(protocol);
        }

        Swarm.ConnectionEstablished += Swarm_ConnectionEstablished;

        // TODO: clear the stats.
        _peerLedgers.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        Log.Debug("Stopping");

        Swarm.ConnectionEstablished -= Swarm_ConnectionEstablished;
        foreach (var protocol in Protocols)
        {
            Swarm.RemoveProtocol(protocol);
        }

        foreach (var cid in _wants.Keys)
        {
            Unwant(cid);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Gets the bitswap ledger for the specified peer.
    /// </summary>
    /// <param name="peer">
    ///     The peer to get information on.  If the peer is unknown, then a ledger
    ///     with zeros is returned.
    /// </param>
    /// <returns>
    ///     Statistics on the bitswap blocks exchanged with the peer.
    /// </returns>
    /// <seealso cref="IBitswapApi.LedgerAsync(Peer, CancellationToken)" />
    public BitswapLedger PeerLedger(Peer peer)
    {
        if (_peerLedgers.TryGetValue(peer, out var ledger))
        {
            return ledger;
        }

        return new() { Peer = peer };
    }

    /// <summary>
    ///     Raised when a blocked is needed.
    /// </summary>
    /// <remarks>
    ///     Only raised when a block is first requested.
    /// </remarks>
    public event EventHandler<CidEventArgs> BlockNeeded;

    // When a connection is established
    // (1) Send the local peer's want list to the remote
#pragma warning disable VSTHRD100 // Avoid async void methods
    private async void Swarm_ConnectionEstablished(object sender, PeerConnection connection)
#pragma warning restore VSTHRD100 // Avoid async void methods
    {
        if (_wants.Count == 0)
        {
            return;
        }

        try
        {
            // There is a race condition between getting the remote identity and
            // the remote sending the first wantlist.
            var peer = await connection.IdentityEstablished.Task.ConfigureAwait(false);

            // Fire and forget.
            var _ = SendWantListAsync(peer, _wants.Values, true);
        }
        catch (Exception e)
        {
            Log.Warn("Sending want list", e);
        }
    }

    /// <summary>
    ///     The blocks needed by the peer.
    /// </summary>
    /// <param name="peer">
    ///     The unique ID of the peer.
    /// </param>
    /// <returns>
    ///     The sequence of CIDs need by the <paramref name="peer" />.
    /// </returns>
    public IEnumerable<Cid> PeerWants(MultiHash peer)
    {
        return _wants.Values
            .Where(w => w.Peers.Contains(peer))
            .Select(w => w.Id);
    }

    /// <summary>
    ///     Adds a block to the want list.
    /// </summary>
    /// <param name="id">
    ///     The CID of the block to add to the want list.
    /// </param>
    /// <param name="peer">
    ///     The unique ID of the peer that wants the block.  This is for
    ///     information purposes only.
    /// </param>
    /// <param name="cancel">
    ///     Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException" /> is raised.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task's result is
    ///     the contents of block.
    /// </returns>
    /// <remarks>
    ///     Other peers are informed that the block is needed by this peer. Hopefully,
    ///     someone will forward it to us.
    ///     <para>
    ///         Besides using <paramref name="cancel" /> for cancellation, the
    ///         <see cref="Unwant" /> method will also cancel the operation.
    ///     </para>
    /// </remarks>
    public Task<IDataBlock> WantAsync(Cid id, MultiHash peer, CancellationToken cancel)
    {
        if (Log.IsDebugEnabled)
        {
            Log.Debug($"{peer} wants {id}");
        }

        var tsc = new TaskCompletionSource<IDataBlock>();
        var want = _wants.AddOrUpdate(
            id,
            key => new()
            {
                Id = id,
                Consumers = new() { tsc },
                Peers = new() { peer }
            },
            (key, block) =>
            {
                block.Peers.Add(peer);
                block.Consumers.Add(tsc);
                return block;
            }
        );

        // If cancelled, then the block is unwanted.
        cancel.Register(() => Unwant(id));

        // If first time, tell other peers.
        if (want.Consumers.Count != 1)
        {
            return tsc.Task;
        }

        var _ = SendWantListToAllAsync(new[] { want }, false);
        BlockNeeded?.Invoke(this, new() { Id = want.Id });

        return tsc.Task;
    }

    /// <summary>
    ///     Removes the block from the want list.
    /// </summary>
    /// <param name="id">
    ///     The CID of the block to remove from the want list.
    /// </param>
    /// <remarks>
    ///     Any tasks waiting for the block are cancelled.
    ///     <para>
    ///         No exception is thrown if the <paramref name="id" /> is not
    ///         on the want list.
    ///     </para>
    /// </remarks>
    public void Unwant(Cid id)
    {
        if (Log.IsDebugEnabled)
        {
            Log.Debug($"Unwant {id}");
        }

        if (!_wants.TryRemove(id, out var block))
        {
            return;
        }

        foreach (var consumer in block.Consumers)
        {
            consumer.SetCanceled();
        }

        // TODO: Tell the swarm
    }

    /// <summary>
    ///     Indicate that a remote peer sent a block.
    /// </summary>
    /// <param name="remote">
    ///     The peer that sent the block.
    /// </param>
    /// <param name="block">
    ///     The data for the block.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Updates the statistics.
    ///     </para>
    ///     <para>
    ///         If the block is acceptable then the <paramref name="block" /> is added to local cache
    ///         via the <see cref="BlockService" />.
    ///     </para>
    /// </remarks>
    public Task OnBlockReceivedAsync(Peer remote, byte[] block)
    {
        return OnBlockReceivedAsync(remote, block, Cid.DefaultContentType, MultiHash.DefaultAlgorithmName);
    }

    /// <summary>
    ///     Indicate that a remote peer sent a block.
    /// </summary>
    /// <param name="remote">
    ///     The peer that sent the block.
    /// </param>
    /// <param name="block">
    ///     The data for the block.
    /// </param>
    /// <param name="contentType">
    ///     The <see cref="Cid.ContentType" /> of the block.
    /// </param>
    /// <param name="multiHash">
    ///     The multihash algorithm name of the block.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Updates the statistics.
    ///     </para>
    ///     <para>
    ///         If the block is acceptable then the <paramref name="block" /> is added to local cache
    ///         via the <see cref="BlockService" />.
    ///     </para>
    /// </remarks>
    public async Task OnBlockReceivedAsync(Peer remote, byte[] block, string contentType, string multiHash)
    {
        // Update statistics.
        ++_blocksReceived;
        _dataReceived += (ulong)block.LongLength;
        _peerLedgers.AddOrUpdate(remote,
            peer => new()
            {
                Peer = peer,
                BlocksExchanged = 1,
                DataReceived = (ulong)block.LongLength
            },
            (peer, ledger) =>
            {
                ++ledger.BlocksExchanged;
                _dataReceived += (ulong)block.LongLength;
                return ledger;
            });

        // TODO: Detect if duplicate and update stats
        var isDuplicate = false;
        if (isDuplicate)
        {
            ++_dupBlksReceived;
            _dupDataReceived += (ulong)block.Length;
        }

        // TODO: Determine if we should accept the block from the remote.
        var acceptble = true;
        if (acceptble)
        {
            await BlockService.PutAsync(
                    block,
                    contentType,
                    multiHash,
                    pin: false)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Indicate that the local peer sent a block to a remote peer.
    /// </summary>
    /// <param name="remote">
    ///     The peer that sent the block.
    /// </param>
    /// <param name="block">
    ///     The data for the block.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    public Task OnBlockSentAsync(Peer remote, IDataBlock block)
    {
        ++_blocksSent;
        _dataSent += (ulong)block.Size;
        _peerLedgers.AddOrUpdate(remote,
            peer => new()
            {
                Peer = peer,
                BlocksExchanged = 1,
                DataSent = (ulong)block.Size
            },
            (peer, ledger) =>
            {
                ++ledger.BlocksExchanged;
                _dataSent += (ulong)block.Size;
                return ledger;
            });

        return Task.CompletedTask;
    }

    /// <summary>
    ///     Indicate that a block is found.
    /// </summary>
    /// <param name="block">
    ///     The block that was found.
    /// </param>
    /// <returns>
    ///     The number of consumers waiting for the <paramref name="block" />.
    /// </returns>
    /// <remarks>
    ///     <b>Found</b> should be called whenever a new block is discovered.
    ///     It will continue any Task that is waiting for the block and
    ///     remove the block from the want list.
    /// </remarks>
    public int Found(IDataBlock block)
    {
        if (!_wants.TryRemove(block.Id, out var want))
        {
            return 0;
        }

        foreach (var consumer in want.Consumers)
        {
            consumer.SetResult(block);
        }

        return want.Consumers.Count;

    }

    /// <summary>
    ///     Send our want list to the connected peers.
    /// </summary>
    private async Task SendWantListToAllAsync(IEnumerable<WantedBlock> wants, bool full)
    {
        if (Swarm == null)
        {
            return;
        }

        try
        {
            var tasks = Swarm.KnownPeers
                .Where(p => p.ConnectedAddress != null)
                .Select(p => SendWantListAsync(p, wants, full))
                .ToArray();
            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Spamming {tasks.Length} connected peers");
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            if (Log.IsDebugEnabled)
            {
                Log.Debug($"Spam {tasks.Length} connected peers done");
            }
        }
        catch (Exception e)
        {
            Log.Debug("sending to all failed", e);
        }
    }

    private async Task SendWantListAsync(Peer peer, IEnumerable<WantedBlock> wants, bool full)
    {
        Log.Debug($"sending want list to {peer}");

        // Send the want list to the peer on any bitswap protocol
        // that it supports.
        var wantedBlocks = wants.ToList();
        foreach (var protocol in Protocols)
        {
            try
            {
                await using var stream = await Swarm.DialAsync(peer, protocol.ToString()).ConfigureAwait(false);
                await protocol.SendWantsAsync(stream, wantedBlocks, full).ConfigureAwait(false);
                return;
            }
            catch (Exception)
            {
                Log.Debug($"{peer} refused {protocol}");
            }
        }

        Log.Warn($"{peer} does not support any bitswap protocol");
    }
}