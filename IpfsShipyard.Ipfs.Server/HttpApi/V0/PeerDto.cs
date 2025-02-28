﻿using System.Collections.Generic;
using System.Linq;
using IpfsShipyard.Ipfs.Core;

namespace IpfsShipyard.Ipfs.Server.HttpApi.V0;

/// <summary>
///     Information on a peer.
/// </summary>
public class PeerInfoDto
{
    /// <summary>
    ///     The addresses that the peer is listening on.
    /// </summary>
    public IEnumerable<string> Addresses;

    /// <summary>
    ///     The version of the software.
    /// </summary>
    public string AgentVersion;

    /// <summary>
    ///     The unique ID of the peer.
    /// </summary>
    public string Id;

    /// <summary>
    ///     The version of the protocol.
    /// </summary>
    public string ProtocolVersion;

    /// <summary>
    ///     The public key of the peer.
    /// </summary>
    public string PublicKey;

    /// <summary>
    ///     Creates a new peer info.
    /// </summary>
    public PeerInfoDto(Peer peer)
    {
        Id = peer.Id.ToBase58();
        PublicKey = peer.PublicKey;
        Addresses = peer.Addresses.Select(a => a.ToString());
        AgentVersion = peer.AgentVersion;
        ProtocolVersion = peer.ProtocolVersion;
    }
}