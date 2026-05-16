using Rests;

namespace NextBotAdapter.Infrastructure;

/// <summary>
/// Shared helpers for reading route parameters from <see cref="RestRequestArgs"/>.
///
/// TShock's REST framework does not URL-decode path-segment captures (<c>args.Verbs[...]</c>),
/// so non-ASCII identifiers such as "千亦" arrive at the business layer as the raw
/// percent-encoded form "%E5%8D%83%E4%BA%A6". This helper centralizes the verb-decode +
/// fallback chain so all endpoints behave consistently.
///
/// Query / form parameter sources (<c>args.Parameters</c>, <c>args.Request.Parameters</c>)
/// are already decoded by the server-side parser; decoding them again would corrupt
/// legitimate literal "%25" values back into "%".
/// </summary>
public static class RouteParameters
{
    /// <summary>
    /// Reads a route parameter and URL-decodes the verb-source value.
    /// </summary>
    /// <param name="args">The current REST request.</param>
    /// <param name="key">The parameter key (see <see cref="RequestParameters"/>).</param>
    /// <returns>
    /// The decoded verb value when present; otherwise the raw value from
    /// <c>args.Parameters[key]</c> or <c>args.Request.Parameters[key]</c>. Returns
    /// <c>null</c> when all sources are empty.
    /// </returns>
    /// <remarks>
    /// When the verb-source value contains an invalid percent-encoding the original raw
    /// string is returned (no exception is thrown). Upstream validation will reject it
    /// through the usual blank / invalid-username paths.
    /// </remarks>
    public static string? ReadDecodedRouteParam(RestRequestArgs args, string key)
    {
        var fromVerb = args.Verbs?[key];
        if (!string.IsNullOrEmpty(fromVerb))
        {
            try
            {
                return Uri.UnescapeDataString(fromVerb);
            }
            catch (UriFormatException)
            {
                return fromVerb;
            }
        }

        return args.Parameters?[key] ?? args.Request?.Parameters?[key];
    }
}
