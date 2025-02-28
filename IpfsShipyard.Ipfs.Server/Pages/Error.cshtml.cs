using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IpfsShipyard.Ipfs.Server.Pages;

/// <summary>
///     Information on an error.
/// </summary>
public class ErrorModel : PageModel
{
    /// <summary>
    ///     The ID of the failed request.
    /// </summary>
    public string RequestId { get; set; }

    /// <summary>
    ///     Is the request ID present.
    /// </summary>
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    /// <summary>
    ///     Build the model.
    /// </summary>
    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    }
}