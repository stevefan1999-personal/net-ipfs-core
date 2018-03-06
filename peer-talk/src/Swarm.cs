﻿using Ipfs;
using PeerTalk.Transports;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using Common.Logging;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PeerTalk
{
    /// <summary>
    ///   Manages communication with other peers.
    /// </summary>
    public class Swarm : IService, IPolicy<MultiAddress>
    {
        static ILog log = LogManager.GetLogger(typeof(Swarm));

        /// <summary>
        ///  The local peer.
        /// </summary>
        public Peer LocalPeer { get; set; }

        /// <summary>
        ///   Other nodes. Key is the bae58 hash of the peer ID.
        /// </summary>
        ConcurrentDictionary<string, Peer> otherPeers = new ConcurrentDictionary<string, Peer>();

        /// <summary>
        ///   The connections to other peers. Key is the base58 hash of the peer ID.
        /// </summary>
        ConcurrentDictionary<string, PeerConnection> connections = new ConcurrentDictionary<string, PeerConnection>();

        /// <summary>
        ///   Cancellation tokens for the listeners.
        /// </summary>
        ConcurrentDictionary<MultiAddress, CancellationTokenSource> listeners = new ConcurrentDictionary<MultiAddress, CancellationTokenSource>();

        /// <summary>
        ///   Get the sequence of all known peer addresses.
        /// </summary>
        /// <value>
        ///   Contains any peer address that has been
        ///   <see cref="RegisterPeerAsync">discovered</see>.
        /// </value>
        /// <seealso cref="RegisterPeerAsync"/>
        public IEnumerable<MultiAddress> KnownPeerAddresses
        {
            get
            {
                return otherPeers
                    .Values
                    .SelectMany(p => p.Addresses);
            }
        }

        /// <summary>
        ///   Get the sequence of all known peers.
        /// </summary>
        /// <value>
        ///   Contains any peer that has been
        ///   <see cref="RegisterPeerAsync">discovered</see>.
        /// </value>
        /// <seealso cref="RegisterPeerAsync"/>
        public IEnumerable<Peer> KnownPeers
        {
            get
            {
                return otherPeers.Values;
            }
        }

        /// <summary>
        ///   Register that a peer's address has been discovered.
        /// </summary>
        /// <param name="address">
        ///   An address to the peer. 
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's result
        ///   is the <see cref="Peer"/> that is registered.
        /// </returns>
        /// <exception cref="Exception">
        ///   The <see cref="BlackList"/> or <see cref="WhiteList"/> policies forbid it.
        ///   Or the "ipfs" protocol name is missing.
        /// </exception>
        /// <remarks>
        ///   If the <paramref name="address"/> is not already known, then it is
        ///   added to the <see cref="KnownPeerAddresses"/>.
        /// </remarks>
        public async Task<Peer> RegisterPeerAsync(MultiAddress address, CancellationToken cancel = default(CancellationToken))
        {
            if (address.Protocols.Last().Name != "ipfs")
            {
                throw new Exception($"'{address}' missing ipfs protocol name.");
            }

            var peerId = address.Protocols
                .Last(p => p.Name == "ipfs")
                .Value;
            if (peerId == LocalPeer.Id)
            {
               throw new Exception("Cannot register to self.");
            }

            if (!await IsAllowedAsync(address, cancel))
            {
                throw new Exception($"Communication with '{address}' is not allowed.");
            }

            return otherPeers.AddOrUpdate(peerId,
                (id) => {
                    log.Debug("new peer " + peerId);
                    return new Peer
                    {
                        Id = id,
                        Addresses = new List<MultiAddress> { address }
                    };
                },
                (id, peer) =>
                {
                    var addrs = (List<MultiAddress>)peer.Addresses;
                    if (!addrs.Contains(address))
                    {
                        addrs.Add(address);
                    }
                    return peer;
                });
        }

        /// <summary>
        ///   The addresses that cannot be used.
        /// </summary>
        public BlackList<MultiAddress> BlackList { get; set;  } = new BlackList<MultiAddress>();

        /// <summary>
        ///   The addresses that can be used.
        /// </summary>
        public WhiteList<MultiAddress> WhiteList { get; set;  } = new WhiteList<MultiAddress>();

        /// <inheritdoc />
        public Task StartAsync()
        {
            log.Debug("Starting");

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAsync()
        {
            log.Debug("Stopping");

            // Stop the listeners
            foreach (var address in listeners.Keys)
            {
                await StopListeningAsync(address);
            }

            // Disconnect from remote peers
            foreach (var peer in otherPeers.Values.Where(p => p.ConnectedAddress != null))
            {
                await DisconnectAsync(peer.ConnectedAddress);
            }

            // Just in case.
            foreach (var connection in connections.Values)
            {
                connection.Dispose();
            }

            otherPeers.Clear();
            connections.Clear();
            listeners.Clear();
            BlackList = new BlackList<MultiAddress>();
            WhiteList = new WhiteList<MultiAddress>();
        }


        /// <summary>
        ///   Connect to a peer.
        /// </summary>
        /// <param name="address">
        ///   An ipfs <see cref="MultiAddress"/>, such as
        ///  <c>/ip4/104.131.131.82/tcp/4001/ipfs/QmaCpDMGvV2BGHeYERUEnRQAwe3N8SzbUtfsmvsqQLuvuJ</c>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation. The task's result
        ///   is the connected <see cref="Peer"/>.
        /// </returns>
        /// <remarks>
        ///   If already connected to the peer, on any address, then nothing is done.
        /// </remarks>
        public async Task<Peer> ConnectAsync(MultiAddress address, CancellationToken cancel = default(CancellationToken))
        {
            var peer = await RegisterPeerAsync(address, cancel);

            if (peer.ConnectedAddress != null)
            {
                return peer;
            }

            // Establish a stream.
            var addrs = await address.ResolveAsync(cancel);
            var connection = await Dial(peer, addrs, cancel);
            if (connection == null)
            {
                return null; // most likely a cancel
            }
            try
            {
                await connection.InitiateAsync(cancel);
            }
            catch (Exception)
            {
                connection.Dispose();
                throw;
            }

            connections[peer.Id.ToBase58()] = connection;
            peer.ConnectedAddress = address;
            return peer;
        }

        /// <summary>
        ///   Establish a duplex stream between the local and remote peer.
        /// </summary>
        /// <param name="remote"></param>
        /// <param name="addrs"></param>
        /// <param name="cancel"></param>
        /// <returns></returns>
        async Task<PeerConnection> Dial(Peer remote, List<MultiAddress> addrs, CancellationToken cancel)
        {
            var exceptions = new List<Exception>();
            foreach (var addr in addrs)
            {
                try
                {
                    foreach (var protocol in addr.Protocols)
                    {
                        if (TransportRegistry.Transports.TryGetValue(protocol.Name, out Func<IPeerTransport> transport))
                        {
                            var stream = await transport().ConnectAsync(addr, cancel);
                            if (stream != null)
                            {
                                remote.ConnectedAddress = addr;
                                var connection = new PeerConnection
                                {
                                    LocalPeer = LocalPeer,
                                    // TODO: LocalAddress
                                    RemotePeer = remote,
                                    RemoteAddress = addr,
                                    Stream = stream
                                };

                                return connection;
                            }
                        }
                    }
                    throw new Exception("Missing a transport protocol name.");
                }
                catch (Exception) when (cancel.IsCancellationRequested)
                {
                    return null;
                }
                catch (Exception e)
                {
                    exceptions.Add(new Exception($"Failed via '{addr}'.", e));
                }
            }

            if (addrs.Count == 0)
            {
                exceptions.Add(new Exception("No known address."));
            }
            throw new AggregateException($"Peer '{remote.Id}' is not reachable.", exceptions);
        }

        /// <summary>
        ///   Disconnect from a peer.
        /// </summary>
        /// <param name="address">
        ///   An ipfs <see cref="MultiAddress"/>, such as
        ///  <c>/ip4/104.131.131.82/tcp/4001/ipfs/QmaCpDMGvV2BGHeYERUEnRQAwe3N8SzbUtfsmvsqQLuvuJ</c>.
        /// </param>
        /// <param name="cancel">
        ///   Is used to stop the task.  When cancelled, the <see cref="TaskCanceledException"/> is raised.
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation.
        /// </returns>
        /// <remarks>
        ///   If the peer is not conected, then nothing happens.
        /// </remarks>
        public Task DisconnectAsync(MultiAddress address, CancellationToken cancel = default(CancellationToken))
        {
            var protocol = address.Protocols.LastOrDefault(p => p.Name == "ipfs");
            if (protocol == null)
            {
                return Task.CompletedTask;
            };

            var peerId = protocol.Value;
            if (otherPeers.TryGetValue(peerId, out Peer peer))
            {
                if (peer.ConnectedAddress != null)
                {
                    log.Debug($"disconnecting {peer.ConnectedAddress}");
                    if (connections.TryRemove(peerId, out PeerConnection connection))
                    {
                        connection.Dispose();
                    }
                    peer.ConnectedAddress = null;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///   Start listening on the specified <see cref="MultiAddress"/>.
        /// </summary>
        /// <param name="address">
        ///   Typically "/ip4/0.0.0.0/tcp/4001" or "/ip6/::/tcp/4001".
        /// </param>
        /// <returns>
        ///   A task that represents the asynchronous operation.  The task's result
        ///   is a <see cref="MultiAddress"/> than can be used by another peer
        ///   to connect to tis peer.
        /// </returns>
        /// <exception cref="Exception">
        ///   Already listening on <paramref name="address"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///   <paramref name="address"/> is missing a transport protocol (such as tcp or udp).
        /// </exception>
        /// <remarks>
        ///   Allows other peers to <see cref="ConnectAsync(MultiAddress, CancellationToken)">connect</see>
        ///   to the <paramref name="address"/>.
        ///   <para>
        ///   The <see cref="Peer.Addresses"/> of the <see cref="LocalPeer"/> are updated.  If the <paramref name="address"/> refers to
        ///   any IP address ("/ip4/0.0.0.0" or "/ip6/::") then all network interfaces addresses
        ///   are added.  If the port is zero (as in "/ip6/::/tcp/0"), then the peer addresses contains the actual port number
        ///   that was assigned.
        ///   </para>
        /// </remarks>
        public Task<MultiAddress> StartListeningAsync(MultiAddress address)
        {
            var cancel = new CancellationTokenSource();

            if (!listeners.TryAdd(address, cancel))
            {
                throw new Exception($"Already listening on '{address}'.");
            }

            // Start a listener for the transport
            var didSomething = false;
            foreach (var protocol in address.Protocols)
            {
                if (TransportRegistry.Transports.TryGetValue(protocol.Name, out Func<IPeerTransport> transport))
                {
                    address = transport().Listen(address, OnRemoteConnect, cancel.Token);
                    listeners.TryAdd(address, cancel);
                    didSomething = true;
                    break;
                }
            }
            if (!didSomething)
            {
                throw new ArgumentException($"Missing a transport protocol name '{address}'.", "address");
            }

            var result = new MultiAddress($"{address}/ipfs/{LocalPeer.Id}");

            // Get the actual IP address(es).
            IEnumerable<MultiAddress> addresses = new List<MultiAddress>();
            var ips = NetworkInterface.GetAllNetworkInterfaces()
                // It appears that the loopback adapter is not UP on Ubuntu 14.04.5 LTS
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up 
                    || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                .SelectMany(nic => nic.GetIPProperties().UnicastAddresses);
            if (result.ToString().StartsWith("/ip4/0.0.0.0/"))
            {
                addresses = ips
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(ip =>
                    {
                        return new MultiAddress(result.ToString().Replace("0.0.0.0", ip.Address.ToString()));
                    })
                    .ToArray();
            }
            else if (result.ToString().StartsWith("/ip6/::/"))
            {
                addresses = ips
                    .Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6)
                    .Select(ip =>
                    {
                        return new MultiAddress(result.ToString().Replace("::", ip.Address.ToString()));
                    })
                    .ToArray();
            }
            else
            {
                addresses = new MultiAddress[] { result };
            }
            if (addresses.Count() == 0)
            {
                var msg = "Cannot determine address(es) for " + result;
                foreach (var ip in ips)
                {
                    msg += "nic-ip: " + ip.Address.ToString();
                }
                cancel.Cancel();
                throw new Exception(msg);
            }

            // Add actual addresses to listeners and local peer addresses.
            foreach (var a in addresses)
            {
                listeners.TryAdd(a, cancel);
            }
            LocalPeer.Addresses = LocalPeer
                .Addresses
                .Union(addresses)
                .ToArray();

            return Task.FromResult(addresses.First());
        }

        /// <summary>
        ///   Called when a remote peer is connecting to the local peer.
        /// </summary>
        /// <param name="stream">
        ///   The stream to the remote peer.
        /// </param>
        /// <param name="local">
        ///   The local peer's address.
        /// </param>
        /// <param name="remote">
        ///   The remote peer's address.
        /// </param>
        /// <remarks>
        ///   Establishes the protocols of the connection.
        ///   <para>
        ///   If any error is encountered, then the connection is closed.
        ///   </para>
        /// </remarks>
        async void OnRemoteConnect(Stream stream, MultiAddress local, MultiAddress remote)
        {
            log.Debug("Got remote connection");
            log.Debug("local " + local);
            log.Debug("remote " + remote);

            // TODO: Check the policies

            var connection = new PeerConnection
            {
                LocalPeer = LocalPeer,
                LocalAddress = local,
                RemoteAddress = remote,
                Stream = stream
            };
            try
            {
                await connection.RespondAsync();

                // TODO: register the peer
            } 
            catch (Exception e)
            {
                log.Error("Failed to accept remote connection", e);
                connection.Dispose();
            }
        }

        /// <summary>
        ///   Stop listening on the specified <see cref="MultiAddress"/>.
        /// </summary>
        /// <param name="address"></param>
        /// <returns>
        ///   A task that represents the asynchronous operation.
        /// </returns>
        /// <remarks>
        ///   Allows other peers to <see cref="ConnectAsync(MultiAddress, CancellationToken)">connect</see>
        ///   to the <paramref name="address"/>.
        ///   <para>
        ///   The addresses of the <see cref="LocalPeer"/> are updated.
        ///   </para>
        /// </remarks>
        public Task StopListeningAsync(MultiAddress address)
        {
            if (listeners.TryRemove(address, out CancellationTokenSource listener))
            {
                listener.Cancel();

                // Remove any local peer address that depends on the cancellation token.
                var others = listeners
                    .Where(l => l.Value == listener)
                    .Select(l => l.Key);
                LocalPeer.Addresses = LocalPeer.Addresses
                    .Where(a => !others.Contains(a))
                    .ToArray();
            }
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> IsAllowedAsync(MultiAddress target, CancellationToken cancel = default(CancellationToken))
        {
            return await BlackList.IsAllowedAsync(target, cancel)
                && await WhiteList.IsAllowedAsync(target, cancel);
        }

        /// <inheritdoc />
        public async Task<bool> IsNotAllowedAsync(MultiAddress target, CancellationToken cancel = default(CancellationToken))
        {
            var q = await IsAllowedAsync(target, cancel);
            return !q;
        }
    }
}
