﻿using Common.Logging;
using Ipfs;
using PeerTalk;
using PeerTalk.Protocols;
using ProtoBuf;
using Semver;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace PeerTalk.Routing
{
    /// <summary>
    ///   DHT Protocol version 1.0
    /// </summary>
    public class Dht1 : IPeerProtocol, IService, IPeerRouting, IContentRouting
    {
        static ILog log = LogManager.GetLogger(typeof(Dht1));

        /// <inheritdoc />
        public string Name { get; } = "ipfs/kad";

        /// <inheritdoc />
        public SemVersion Version { get; } = new SemVersion(1, 0);

        /// <summary>
        ///   Provides access to other peers.
        /// </summary>
        public Swarm Swarm { get; set; }

        public RoutingTable RoutingTable = new RoutingTable();

        /// <inheritdoc />
        public override string ToString()
        {
            return $"/{Name}/{Version}";
        }

        /// <inheritdoc />
        public async Task ProcessMessageAsync(PeerConnection connection, Stream stream, CancellationToken cancel = default(CancellationToken))
        {
            var request = await ProtoBufHelper.ReadMessageAsync<DhtMessage>(stream, cancel);

            log.Debug($"got message from {connection.RemotePeer}");
            // TODO: process the request
        }

        /// <inheritdoc />
        public Task StartAsync()
        {
            log.Debug("Starting");

            RoutingTable = new RoutingTable();
            Swarm.AddProtocol(this);
            Swarm.PeerDiscovered += Swarm_PeerDiscovered;
            foreach (var peer in Swarm.KnownPeers)
            {
                RoutingTable.Peers.Add(peer);
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task StopAsync()
        {
            log.Debug("Stopping");

            Swarm.RemoveProtocol(this);
            Swarm.PeerDiscovered -= Swarm_PeerDiscovered;

            return Task.CompletedTask;
        }

        /// <summary>
        ///   The swarm has discovered a new peer, update the routing table.
        /// </summary>
        void Swarm_PeerDiscovered(object sender, Peer e)
        {
            RoutingTable.Peers.Add(e);
        }

        /// <inheritdoc />
        public async Task<Peer> FindPeerAsync(MultiHash id, CancellationToken cancel = default(CancellationToken))
        {
            // Can always find self.
            if (Swarm.LocalPeer.Id == id)
                return Swarm.LocalPeer;

            // Maybe the swarm knows about it.
            var found = Swarm.KnownPeers.FirstOrDefault(p => p.Id == id);
            if (found != null)
                return found;

            // Ask our peers for information of requested peer.
            var nearest = RoutingTable.NearestPeers(id);
            var query = new DhtMessage
            {
                Type = MessageType.FindNode,
                Key = id.ToArray()
            };
            log.Debug($"Query {query.Type}");
            foreach (var peer in nearest)
            {
                if (found != null)
                {
                    return found;
                }

                log.Debug($"Query peer {peer.Id} for {query.Type}");

                using (var stream = await Swarm.DialAsync(peer, this.ToString(), cancel))
                {
                    ProtoBuf.Serializer.SerializeWithLengthPrefix(stream, query, PrefixStyle.Base128);
                    await stream.FlushAsync(cancel);
                    var response = await ProtoBufHelper.ReadMessageAsync<DhtMessage>(stream, cancel);
                    if (response.CloserPeers == null)
                    {
                        continue;
                    }
                    foreach (var closer in response.CloserPeers)
                    {
                        if (closer.TryToPeer(out Peer p))
                        {
                            p = Swarm.RegisterPeer(p);
                            if (id == p.Id)
                            {
                                log.Debug("Found answer");
                                found = p;
                            }
                        }
                    }
                }
            }

            // Unknown peer ID.
            throw new KeyNotFoundException($"Cannot locate peer '{id}'.");
        }

        /// <inheritdoc />
        public Task ProvideAsync(Cid cid, bool advertise = true, CancellationToken cancel = default(CancellationToken))
        {
            throw new NotImplementedException("DHT ProvideAsync");
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Peer>> FindProvidersAsync(Cid id, int limit = 20, CancellationToken cancel = default(CancellationToken))
        {
            var providers = new List<Peer>();
            var visited = new List<Peer> { Swarm.LocalPeer };
            var peersToVisit = new ConcurrentQueue<Peer>();

            //var key = Encoding.ASCII.GetBytes(id.Encode());
            var key = id.Hash.ToArray();

            // Ask our peers for information of requested peer.
            foreach (var peer in RoutingTable.NearestPeers(id.Hash).Take(3))
            {
                peersToVisit.Enqueue(peer);
            }

            var query = new DhtMessage
            {
                Type = MessageType.GetProviders,
                Key = key
            };
            log.Debug($"Query {query.Type}");
            while (peersToVisit.TryDequeue(out Peer peer))
            {
                if (providers.Count >= limit)
                    break;
                if (visited.Contains(peer))
                {
                    continue;
                }
                log.Debug($"Query peer {peer.Id} for {query.Type}");
                visited.Add(peer);

                try
                {
                    using (var stream = await Swarm.DialAsync(peer, this.ToString(), cancel))
                    {
                        ProtoBuf.Serializer.SerializeWithLengthPrefix(stream, query, PrefixStyle.Base128);
                        await stream.FlushAsync(cancel);
                        var response = await ProtoBufHelper.ReadMessageAsync<DhtMessage>(stream, cancel);
                        if (response.CloserPeers != null)
                        {
                            foreach (var closer in response.CloserPeers)
                            {
                                if (closer.TryToPeer(out Peer p))
                                {
                                    p = Swarm.RegisterPeer(p);
                                    if (!visited.Contains(p))
                                    {
                                        Console.WriteLine($"Closer peer {p}");
                                        peersToVisit.Enqueue(p);
                                    }
                                }
                            }
                        }
                        if (response.ProviderPeers != null)
                        {
                            foreach (var provider in response.ProviderPeers)
                            {
                                if (provider.TryToPeer(out Peer p))
                                {
                                    Console.WriteLine($"FOUND peer {p}");
                                    providers.Add(Swarm.RegisterPeer(p));
                                }
                            }
                        }
                    }
                }
                
                catch (Exception e)
                {
                    log.Warn(e); //eat it
                }
            }

            // All peers queried or the limit has been reached.
            log.Debug($"Found {providers.Count} providers, visited {visited.Count} peers");
            return providers.Take(limit);
        }
    }
}
