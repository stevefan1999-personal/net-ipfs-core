﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using IpfsShipyard.Ipfs.Core;
using IpfsShipyard.Ipfs.Core.CoreApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace IpfsShipyard.Ipfs.Server.HttpApi.V0;

/// <summary>
///     A created file.
/// </summary>
public class FileSystemNodeDto
{
    /// <summary>
    ///     The CID of the file.
    /// </summary>
    public string Hash;

    /// <summary>
    ///     The file name.
    /// </summary>
    public string Name;

    /// <summary>
    ///     The file size.
    /// </summary>
    public string Size;
}

/// <summary>
///     A link to a file.
/// </summary>
public class FileSystemLinkDto
{
    /// <summary>
    ///     The CID of the file.
    /// </summary>
    public string Hash;

    /// <summary>
    ///     The file name.
    /// </summary>
    public string Name;

    /// <summary>
    ///     The file size.
    /// </summary>
    public long Size;
}

/// <summary>
///     Details on a files.
/// </summary>
public class FileSystemDetailDto
{
    /// <summary>
    ///     The CID of the file.
    /// </summary>
    public string Hash;

    /// <summary>
    ///     Links to other files.
    /// </summary>
    public FileSystemLinkDto[] Links;

    /// <summary>
    ///     The file size.
    /// </summary>
    public long Size;

    /// <summary>
    ///     "File" or "Directory"
    /// </summary>
    public string Type;
}

/// <summary>
///     A map of files.
/// </summary>
public class FileSystemDetailsDto
{
    /// <summary>
    ///     A path and its CID.
    /// </summary>
    public Dictionary<string, string> Arguments;

    /// <summary>
    ///     The pins.
    /// </summary>
    public Dictionary<string, FileSystemDetailDto> Objects;
}

/// <summary>
///     DNS mapping to IPFS.
/// </summary>
/// <remarks>
///     Multihashes are hard to remember, but domain names are usually easy to
///     remember. To create memorable aliases for multihashes, DNS TXT
///     records can point to other DNS links, IPFS objects, IPNS keys, etc.
/// </remarks>
public class FileSystemController : IpfsController
{
    /// <summary>
    ///     Creates a new controller.
    /// </summary>
    public FileSystemController(ICoreApi ipfs) : base(ipfs)
    {
    }

    /// <summary>
    ///     Get the contents of a file or directory.
    /// </summary>
    /// <param name="arg">
    ///     A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
    ///     or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
    /// </param>
    /// <param name="offset">
    ///     Offset into the file.
    /// </param>
    /// <param name="length">
    ///     Number of bytes to read.
    /// </param>
    [HttpGet]
    [HttpPost]
    [Route("cat")]
    [Produces("application/octet-stream")]
    public async Task<IActionResult> Cat(
        string arg,
        long offset = 0,
        long length = 0)
    {
        EntityTagHeaderValue etag = null;
        var path = await IpfsCore.Generic.ResolveAsync(arg, true, Cancel);
        var cid = Cid.Decode(path[6..]); // remove leading "/ipfs/"

        // Use an etag if the path is IPFS or CID.
        if (arg.StartsWith("/ipfs/") || arg[0] != '/')
        {
            etag = ETag(cid);
            Immutable();
        }

        // Use the last part of the path as the download filename
        var filename = arg.Split('/').Last();
        var stream = await IpfsCore.FileSystem.ReadFileAsync(cid, offset, length, Cancel);

        ArgumentNullException.ThrowIfNull(etag);
        return File(stream, "application/octet-stream", filename, null, etag);
    }

    /// <summary>
    ///     Get the object as a TAR file.
    /// </summary>
    /// <param name="arg">
    ///     A path to an existing file or directory.
    /// </param>
    /// <param name="compress">
    ///     If <b>true</b>, generate gzipped TAR.
    /// </param>
    [HttpGet]
    [HttpPost]
    [Route("get")]
    [Produces("application/tar")]
    public async Task Get(string arg, bool compress = false)
    {
        var tar = await IpfsCore.FileSystem.GetAsync(arg, compress, Cancel);
        Response.ContentType = "application/tar";
        Response.Headers.Add("X-Stream-Output", "1");
        Response.Headers.Add("X-Content-Length", "4");
        Response.StatusCode = 200;

        await tar.CopyToAsync(Response.Body);
        await Response.Body.FlushAsync();
    }

    /// <summary>
    ///     Get information on the file or directory.
    /// </summary>
    /// <param name="arg">
    ///     A path to an existing file, such as "QmXarR6rgkQ2fDSHjSY5nM2kuCXKYGViky5nohtwgF65Ec/about"
    ///     or "QmZTR5bcpQD7cFgTorqxZDYaew1Wqgfbd2ud9QqGPAkK2V"
    /// </param>
    [HttpGet]
    [HttpPost]
    [Route("file/ls")]
    public async Task<FileSystemDetailsDto> Stat(
        string arg)
    {
        var node = await IpfsCore.FileSystem.ListFileAsync(arg, Cancel);
        var dto = new FileSystemDetailsDto
        {
            Arguments = new(),
            Objects = new()
        };
        dto.Arguments[arg] = node.Id;
        dto.Objects[node.Id] = new()
        {
            Hash = node.Id,
            Size = node.Size,
            Type = node.IsDirectory ? "Directory" : "File",
            Links = node.Links
                .Select(link => new FileSystemLinkDto
                {
                    Hash = link.Id,
                    Name = link.Name,
                    Size = link.Size
                })
                .ToArray()
        };
        return dto;
    }

    /// <summary>
    ///     Add a file.
    /// </summary>
    [HttpGet]
    [HttpPost]
    [Route("add")]
    public async Task Add(
        IFormFile file,
        string hash = MultiHash.DefaultAlgorithmName,
        [ModelBinder(Name = "cid-base")] string cidBase = MultiBase.DefaultAlgorithmName,
        [ModelBinder(Name = "only-hash")] bool onlyHash = false,
        string chunker = null,
        bool pin = false,
        [ModelBinder(Name = "raw-leaves")] bool rawLeaves = false,
        bool trickle = false,
        [ModelBinder(Name = "wrap-with-directory")]
        bool wrap = false,
        string protect = null,
        bool progress = true
    )
    {
        if (file == null)
        {
            throw new ArgumentNullException(nameof(file));
        }

        var options = new AddFileOptions
        {
            Encoding = cidBase,
            Hash = hash,
            OnlyHash = onlyHash,
            Pin = pin,
            RawLeaves = rawLeaves,
            Trickle = trickle,
            Wrap = wrap,
            ProtectionKey = protect
        };
        if (chunker != null)
        {
            if (chunker.StartsWith("size-"))
            {
                options.ChunkSize = int.Parse(chunker[5..], CultureInfo.InvariantCulture);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(chunker));
            }
        }

        options.Progress = progress ? new Progress<TransferProgress>(StreamJson) : options.Progress;

        // TODO: Accept multiple files.
        await using var stream = file.OpenReadStream();
        // TODO: AddAsync returns a list of nodes containing every node added not just the top level.
        var node = await IpfsCore.FileSystem.AddAsync(stream, file.FileName, options, Cancel);
        StreamJson(new FileSystemNodeDto
        {
            Name = node.Id,
            Hash = node.Id,
            Size = node.Size.ToString(CultureInfo.InvariantCulture)
        });
    }
}