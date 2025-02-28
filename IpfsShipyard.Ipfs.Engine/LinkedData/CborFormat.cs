﻿using PeterO.Cbor;

namespace IpfsShipyard.Ipfs.Engine.LinkedData;

/// <summary>
///     Linked data as a CBOR message.
/// </summary>
/// <seealso href="https://tools.ietf.org/html/rfc7049">RFC 7049</seealso>
public class CborFormat : ILinkedDataFormat
{
    /// <inheritdoc />
    public CBORObject Deserialise(byte[] data)
    {
        return CBORObject.DecodeFromBytes(data);
    }

    /// <inheritdoc />
    public byte[] Serialize(CBORObject data)
    {
        return data.EncodeToBytes();
    }
}