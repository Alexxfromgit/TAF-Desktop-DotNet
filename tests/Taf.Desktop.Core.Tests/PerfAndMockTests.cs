using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using Taf.Desktop.Core.Config;
using Taf.Desktop.Core.Failures;
using Taf.Desktop.Core.Mocking;
using Taf.Desktop.Core.Perf;

namespace Taf.Desktop.Core.Tests;

public class PerfTests
{
    [SetUp]
    public void Clean()
    {
        TestConfig.UseDefaults();
        PerfCollector.Clear();
    }

    [Test]
    public void StatsIncludeP95()
    {
        var stats = PerfStats.Of(PerfKind.WindowReady, "MainWindow ready", Enumerable.Range(1, 20).Select(i => (double)i * 100).ToArray());

        stats.Should().Be(new PerfStats(PerfKind.WindowReady, "MainWindow ready", 20, 100, 1050, 1900, 2000));
    }

    [Test]
    public void TimingsAreCollectedPerLabel()
    {
        PerfCollector.WindowReady("MainWindow", TimeSpan.FromMilliseconds(100));
        PerfCollector.WindowReady("MainWindow", TimeSpan.FromMilliseconds(300));
        PerfCollector.Transition("LoginWindow", "signIn", "MainWindow", TimeSpan.FromMilliseconds(800));

        PerfCollector.Snapshot().Select(s => (s.Label, s.Count, s.Max)).Should().Equal(
            ("MainWindow ready", 2, 300d), ("LoginWindow.signIn -> MainWindow", 1, 800d));
    }

    [Test]
    public void ThresholdsMarkBreaches()
    {
        PerfCollector.WindowReady("MainWindow", TimeSpan.FromMilliseconds(2500));
        PerfCollector.WindowReady("CartWindow", TimeSpan.FromMilliseconds(500));

        var rows = PerfReport.Rows(PerfCollector.Snapshot(), new Dictionary<string, string> { ["MainWindow ready"] = "2s", ["CartWindow ready"] = "1s" });

        rows.Where(r => r.Breached).Select(r => r.Stats.Label).Should().Equal("MainWindow ready");
        PerfReport.Html(rows).Should().Contain("<tr class=\"breach\"><td>WindowReady</td><td>MainWindow ready</td>");
    }
}

public class MockBackendTests
{
    private MockBackend mock = null!;
    private HttpClient http = null!;

    [SetUp]
    public void StartBackend()
    {
        TestConfig.UseDefaults();
        mock = MockBackend.Start();
        http = new HttpClient { BaseAddress = new Uri(mock.Url) };
    }

    [TearDown]
    public void StopBackend()
    {
        http.Dispose();
        mock.Dispose();
    }

    [Test]
    public void UrlIsPublishedForTheAppArguments() =>
        TafConfig.Current.Get("Mock:Url").Should().Be(mock.Url);

    [Test]
    public async Task StubsAnswerAndRequestsAreRecorded()
    {
        mock.StubJson("GET", "/api/products", new[] { new { id = 1, name = "Backpack" } });

        var products = await http.GetFromJsonAsync<List<Product>>("/api/products?search=back");

        products.Should().Equal(new Product(1, "Backpack"));
        var request = mock.SingleRequest("GET", "/api/products");
        request.Query["search"].Should().Be("back");
    }

    [Test]
    public async Task RecordedBodiesCanBeRead()
    {
        mock.StubJson("POST", "/api/orders", new { orderId = "ORD-1" }, status: 201);

        await http.PostAsJsonAsync("/api/orders", new { shipping = "Express", giftWrap = true });

        mock.SingleRequest("POST", "/api/orders").Json.GetProperty("shipping").GetString().Should().Be("Express");
    }

    [Test]
    public async Task VerifyExplainsWhatWasReceived()
    {
        await http.GetAsync("/api/health");

        mock.Invoking(m => m.Verify("POST", "/api/orders", 1)).Should().Throw<PotentialDefectException>()
            .WithMessage("Expected 1 POST /api/orders request(s), got 0. Received: [GET /api/health]");
    }

    [Test]
    public async Task ResetRestoresTheDefaults()
    {
        mock.Defaults = m => m.StubJson("GET", "/api/products", Array.Empty<object>());
        mock.StubJson("GET", "/api/products", new { error = "test-specific" }, status: 500);

        mock.Reset();

        (await http.GetAsync("/api/products")).IsSuccessStatusCode.Should().BeTrue();
        mock.Requests().Should().HaveCount(1);
    }

    [Test]
    public async Task ReverseProxyPassesUnstubbedRequestsThrough()
    {
        using var upstream = MockBackend.Start();
        upstream.StubJson("GET", "/api/products", new[] { new { id = 7, name = "From upstream" } });
        using var proxy = MockBackend.ReverseProxy(upstream.Url);
        proxy.StubJson("GET", "/api/profile", new { name = "stubbed" });
        using var client = new HttpClient { BaseAddress = new Uri(proxy.Url) };

        (await client.GetFromJsonAsync<List<Product>>("/api/products")).Should().Equal(new Product(7, "From upstream"));
        (await client.GetStringAsync("/api/profile")).Should().Contain("stubbed");
    }

    private sealed record Product(int Id, string Name);
}
