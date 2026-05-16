using HttpServer;
using NextBotAdapter.Infrastructure;
using Rests;

namespace NextBotAdapter.Tests;

public sealed class RouteParametersTests
{
    private const string Key = RequestParameters.User;

    [Fact]
    public void ReadDecodedRouteParam_ShouldDecodePercentEncodedChineseFromVerbs()
    {
        var args = BuildArgs(verb: "%E5%8D%83%E4%BA%A6");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("千亦", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldReturnAsciiVerbUnchanged()
    {
        var args = BuildArgs(verb: "Steve");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("Steve", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldReturnAlreadyDecodedVerbUnchanged()
    {
        // The verb already contains decoded Chinese characters. Production TShock hands
        // us the raw percent-encoded form, but the helper must remain a no-op for
        // already-decoded values so a future framework change cannot corrupt the value.
        var args = BuildArgs(verb: "千亦");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("千亦", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldReturnRawVerbWhenEncodingIsInvalid()
    {
        // Uri.UnescapeDataString tolerates many malformed inputs, but inputs containing
        // an unpaired surrogate-shaped escape such as "%ED%A0%80" raise UriFormatException.
        // The helper must fall back to the raw value rather than throw, so upstream
        // username validation can reject it through the normal blank / invalid paths.
        const string raw = "%ED%A0%80";
        var args = BuildArgs(verb: raw);

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal(raw, user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldFallThroughToParametersWhenVerbIsNull()
    {
        var args = BuildArgs(verb: null, parameter: "alice");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("alice", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldFallThroughToParametersWhenVerbIsEmpty()
    {
        var args = BuildArgs(verb: string.Empty, parameter: "alice");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("alice", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldNotDecodeParameterSource()
    {
        // EscapedParameterCollection already decodes query parameters once. If a player
        // somehow registers a literal "%E5%8D%83%E4%BA%A6" account name (a valid TShock
        // identifier), the query parser hands us that literal string. The helper must
        // not run UnescapeDataString a second time, or the literal would be corrupted
        // back into "千亦".
        //
        // To simulate a parameter source that yields the literal "%E5%8D%83%E4%BA%A6",
        // we feed the underlying raw collection a double-encoded value so that
        // EscapedParameterCollection's own (single) decode produces the literal form.
        var args = BuildArgsWithRawParameter(rawParameter: "%25E5%258D%2583%25E4%25BA%25A6");

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Equal("%E5%8D%83%E4%BA%A6", user);
    }

    [Fact]
    public void ReadDecodedRouteParam_ShouldReturnNullWhenAllSourcesAreEmpty()
    {
        var args = BuildArgs(verb: null);

        var user = RouteParameters.ReadDecodedRouteParam(args, Key);

        Assert.Null(user);
    }

    private static RestRequestArgs BuildArgs(string? verb, string? parameter = null)
        => BuildArgsWithRawParameter(verb, parameter);

    private static RestRequestArgs BuildArgsWithRawParameter(string? verb = null, string? rawParameter = null)
    {
        var verbs = new RestVerbs();
        if (verb is not null)
        {
            verbs[Key] = verb;
        }

        // Always supply a real (possibly empty) IParameterCollection. The 4-arg ctor
        // wraps it in EscapedParameterCollection, which decodes values once on read.
        // That mirrors the production code path where TShock receives query parameters
        // already decoded by the HTTP server's parser before exposing them as
        // args.Parameters[...]. The wrapper indexer NREs when given a null backing
        // collection, which never happens in production.
        var pc = new ParameterCollection();
        if (rawParameter is not null)
        {
            pc.Add(Key, rawParameter);
        }

        return new RestRequestArgs(verbs, pc, null!, null!);
    }
}
