﻿using System.Collections.Generic;
using System.Linq;
using System.Net;
using IpfsShipyard.Ipfs.Core;
using Makaretu.Dns;

namespace IpfsShipyard.PeerTalk.Discovery
{
    /// <summary>
    ///   Discovers peers using Multicast DNS according to
    ///   js-ipfs v0.32.3
    /// </summary>
    public class MdnsJs : Mdns
    {
        /// <summary>
        ///   Creates a new instance of the class.  Sets the <see cref="Mdns.ServiceName"/>
        ///   to "ipfs".
        /// </summary>
        public MdnsJs()
        {
            ServiceName = "ipfs";
        }

        /// <inheritdoc />
        protected override void OnServiceDiscovery(ServiceDiscovery discovery)
        {
            discovery.AnswersContainsAdditionalRecords = true;
        }

        /// <inheritdoc />
        public override ServiceProfile BuildProfile()
        {
            // Only internet addresses.
            var addresses = LocalPeer.Addresses
                .Where(a => a.Protocols.FirstOrDefault().Name == "ip4" || a.Protocols.FirstOrDefault().Name == "ip6")
                .ToArray();
            if (addresses.Length == 0)
            {
                return null;
            }
            var ipAddresses = addresses
                .Select(a => IPAddress.Parse(a.Protocols.FirstOrDefault().Value));

            // Only one port is supported.
            var tcpPort = addresses.FirstOrDefault()
                .Protocols.Find(p => p.Name == "tcp")
                .Value;

            // Create the DNS records for this peer.  The TXT record
            // is singular and must contain the peer ID.
            var profile = new ServiceProfile(
                instanceName: LocalPeer.Id.ToBase58(),
                serviceName: ServiceName,
                port: ushort.Parse(tcpPort),
                addresses: ipAddresses
            );
            profile.Resources.RemoveAll(r => r.Type == DnsType.TXT);
            var txt = new TXTRecord { Name = profile.FullyQualifiedName };
            txt.Strings.Add(profile.InstanceName.ToString());
            profile.Resources.Add(txt);

            return profile;
        }

        /// <inheritdoc />
        public override IEnumerable<MultiAddress> GetAddresses(Message message)
        {
            var qsn = ServiceName + ".local";
            var peerNames = message.Answers
                .OfType<PTRRecord>()
                .Where(a => a.Name == qsn)
                .Select(a => a.DomainName);
            foreach (var name in peerNames)
            {
                var id = name.Labels[0];
                var srv = message.Answers
                    .OfType<SRVRecord>()
                    .FirstOrDefault(r => r.Name == name);
                var aRecords = message.Answers
                    .OfType<ARecord>()
                    .Where(a => a.Name == name || a.Name == srv.Target);
                foreach (var a in aRecords)
                {
                    yield return new MultiAddress($"/ip4/{a.Address}/tcp/{srv.Port}/ipfs/{id}");
                }
                var aaaaRecords = message.Answers
                    .OfType<AAAARecord>()
                    .Where(a => a.Name == name || a.Name == srv.Target);
                foreach (var a in aaaaRecords)
                {
                    yield return new MultiAddress($"/ip6/{a.Address}/tcp/{srv.Port}/ipfs/{id}");
                }
            }
        }
    }
}