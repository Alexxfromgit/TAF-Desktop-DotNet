# Backend mocking

Most desktop apps talk to a backend. Starting the app against a mock gives you deterministic data, and you can
simulate the failures the app must handle. `MockBackend` wraps [WireMock.Net](https://github.com/WireMock-Net/WireMock.Net).

## Start it before the app

```csharp
[SetUpFixture]
public sealed class RunSetup : TafRunSetup
{
    protected override void RunStarted() => ShopBackend.Start();     // MockBackend.Start() + default stubs
    protected override void RunFinished() => ShopBackend.Stop();
}
```

The mock publishes its URL as `${Mock:Url}`, so the app gets it through its arguments or environment:
`"Arguments": "--api ${Mock:Url}"`. This works best when your app can take the backend URL as a setting; ask for
that switch if it doesn't exist yet.

## Two modes

| | |
|---|---|
| `MockBackend.Start()` | Stub-only: unknown requests get 404. Deterministic, works offline and in CI. |
| `MockBackend.ReverseProxy("https://api.staging.example.com")` | Everything passes through to the real backend except what a test stubs |

## Default behaviour and per-test changes

```csharp
mock.Defaults = backend =>
{
    backend.StubJson("POST", "/api/login", new { error = "Wrong username or password" }, status: 401, priority: 100);
    backend.Stub(Request.Create().UsingGet().WithPath("/api/products"),
        Response.Create().WithCallback(request => /* filter the catalog by ?search= */));
};
```

`ShopDeskTest` calls `Backend.Reset()` before every test: all stubs and recorded requests are removed, and the
defaults are applied again. A test then overrides single endpoints with a higher priority (a lower number):

```csharp
Backend.StubJson("GET", "/api/products", new { error = "boom" }, status: 500, priority: 1);           // error
Backend.StubJson("GET", "/api/products", products, delay: TimeSpan.FromSeconds(2), priority: 1);      // slow
Backend.Stub(Request.Create().UsingPost().WithPath("/api/login"),
    Response.Create().WithFault(FaultType.EMPTY_RESPONSE), priority: 1);                              // outage
```

## Checking what the app sent

```csharp
Backend.Verify("POST", "/api/login", 1);                       // a report step; fails with the requests received
var order = Backend.SingleRequest("POST", "/api/orders").Json;
order.GetProperty("shipping").GetString().Should().Be("Express");
Backend.Requests("GET", "/api/products").Last().Query["search"].Should().Be("shirt");
```
