using System.Text.Json;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Reporting;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Taf.Desktop.Core.Mocking;

/// <summary>A request the app sent to the <see cref="MockBackend"/>.</summary>
public sealed record RecordedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Query, string Body,
    IReadOnlyDictionary<string, string> Headers)
{
    public T? BodyAs<T>() => JsonSerializer.Deserialize<T>(Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public JsonElement Json => JsonDocument.Parse(Body).RootElement;
}

/// <summary>
/// An HTTP backend for the app under test, on WireMock.Net. Start it before the app and pass <see cref="Url"/> to the
/// app (published as <c>${Mock:Url}</c> for <c>App:Arguments</c> / <c>App:Environment</c>). Two modes:
/// <list type="bullet">
/// <item><see cref="Start"/> - only stubs answer, everything else is 404 (deterministic tests);</item>
/// <item><see cref="ReverseProxy"/> - everything passes through to the real backend except what a test stubs.</item>
/// </list>
/// </summary>
public sealed class MockBackend : IDisposable
{
    private const int PassthroughPriority = 1_000_000;

    private readonly string? upstream;

    private MockBackend(WireMockServer server, string? upstream)
    {
        Server = server;
        this.upstream = upstream;
        RuntimeOverrides.Set(UrlKey, Url);
        AddPassthrough();
    }

    /// <summary>The underlying WireMock server, for anything not covered here.</summary>
    public WireMockServer Server { get; }

    /// <summary>Base URL, e.g. <c>http://localhost:51234</c>.</summary>
    public string Url => Server.Url!;

    /// <summary>Stubs re-applied after every <see cref="Reset"/>: the default behaviour of the backend.</summary>
    public Action<MockBackend>? Defaults { get; set; }

    private static string UrlKey => TafConfig.Current.Get("Mock:UrlKey", "Mock:Url");

    /// <summary>A stub-only backend on a free port (or <paramref name="port"/>).</summary>
    public static MockBackend Start(int port = 0) => new(port == 0 ? WireMockServer.Start() : WireMockServer.Start(port), null);

    /// <summary>A backend that forwards unmatched requests to <paramref name="upstreamUrl"/>.</summary>
    public static MockBackend ReverseProxy(string upstreamUrl, int port = 0) =>
        new(port == 0 ? WireMockServer.Start() : WireMockServer.Start(port), upstreamUrl);

    /// <summary>Answers <paramref name="method"/> <paramref name="path"/> with JSON (stubs added later win over earlier ones of the same priority).</summary>
    public MockBackend StubJson(string method, string path, object body, int status = 200, TimeSpan? delay = null, int priority = 10)
    {
        var response = Response.Create().WithStatusCode(status).WithBodyAsJson(body);
        if (delay is { } d)
        {
            response = response.WithDelay(d);
        }
        return Stub(Request.Create().UsingMethod(method).WithPath(path), response, priority);
    }

    /// <summary>Any WireMock request/response pair: <c>Stub(Request.Create()..., Response.Create()...)</c>.</summary>
    public MockBackend Stub(IRequestBuilder request, IResponseBuilder response, int priority = 10)
    {
        Server.Given(request).AtPriority(priority).RespondWith(response);
        return this;
    }

    /// <summary>Requests received so far, optionally filtered by method and path.</summary>
    public IReadOnlyList<RecordedRequest> Requests(string? method = null, string? path = null) =>
        Server.LogEntries
            .Select(e => e.RequestMessage)
            .OfType<WireMock.IRequestMessage>()
            .Where(r => method == null || string.Equals(r.Method, method, StringComparison.OrdinalIgnoreCase))
            .Where(r => path == null || r.Path == path)
            .Select(r => new RecordedRequest(
                r.Method,
                r.Path,
                (r.Query ?? new Dictionary<string, WireMock.Types.WireMockList<string>>()).ToDictionary(q => q.Key, q => string.Join(",", q.Value)),
                r.Body ?? "",
                (r.Headers ?? new Dictionary<string, WireMock.Types.WireMockList<string>>()).ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)))
            .ToList();

    /// <summary>The single matching request; fails if there is none or more than one.</summary>
    public RecordedRequest SingleRequest(string method, string path)
    {
        Verify(method, path, 1);
        return Requests(method, path)[0];
    }

    /// <summary>Checks the app sent <paramref name="times"/> matching requests (a report step).</summary>
    public void Verify(string method, string path, int times) => Step.Run($"Verify the app sent {times}x {method} {path}", () =>
    {
        var count = Requests(method, path).Count;
        if (count != times)
        {
            var received = string.Join(", ", Requests().Select(r => $"{r.Method} {r.Path}"));
            throw new PotentialDefectException($"Expected {times} {method} {path} request(s), got {count}. Received: [{received}]");
        }
    });

    /// <summary>Removes all stubs and recorded requests, then applies <see cref="Defaults"/> again.</summary>
    public void Reset()
    {
        Server.ResetMappings();
        Server.ResetLogEntries();
        AddPassthrough();
        Defaults?.Invoke(this);
    }

    public void Dispose()
    {
        RuntimeOverrides.Remove(UrlKey);
        Server.Stop();
        Server.Dispose();
    }

    private void AddPassthrough()
    {
        if (upstream != null)
        {
            Server.Given(Request.Create().WithPath("/*")).AtPriority(PassthroughPriority).RespondWith(Response.Create().WithProxy(upstream));
        }
    }
}
