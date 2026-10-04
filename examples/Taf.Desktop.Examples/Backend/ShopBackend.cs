using System.Globalization;
using System.Text.Json;
using Taf.Desktop.Core.Mocking;
using Taf.Desktop.Core.Users;
using WireMock;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Types;
using WireMock.Util;

namespace Taf.Desktop.Examples.Backend;

/// <summary>
/// The ShopDesk REST backend, simulated with the framework's <see cref="MockBackend"/>: sign-in for the test users,
/// a product catalog with search, and orders with server-side totals. Tests override single endpoints to simulate
/// errors or slowness and check the requests the app sent.
/// </summary>
public static class ShopBackend
{
    public const string OrderId = "ORD-1001";

    public static readonly IReadOnlyList<Product> Catalog =
    [
        new(1, "Canvas Backpack", 29.99m, 5),
        new(2, "Bike Light", 9.99m, 0),
        new(3, "Cotton T-Shirt", 15.99m, 12),
        new(4, "Fleece Jacket", 49.99m, 3),
        new(5, "Baby Onesie", 7.99m, 8),
        new(6, "Travel Mug", 12.49m, 20),
    ];

    private static MockBackend? mock;

    public static MockBackend Mock => mock ?? throw new InvalidOperationException("The ShopDesk backend is not running (see RunSetup)");

    public static void Start()
    {
        mock = MockBackend.Start();
        mock.Defaults = ApplyDefaults;
        mock.Reset();
    }

    public static void Stop()
    {
        mock?.Dispose();
        mock = null;
    }

    private static void ApplyDefaults(MockBackend backend)
    {
        backend.StubJson("POST", "/api/login", new { error = "Wrong username or password" }, status: 401, priority: 100);
        SignInAnswer(backend, "standard", 200, new { token = "token-standard", displayName = "Standard User" });
        SignInAnswer(backend, "locked", 403, new { error = "This account is locked" });

        backend.Stub(Request.Create().UsingGet().WithPath("/api/products"), Response.Create().WithCallback(request =>
        {
            var search = request.Query?.TryGetValue("search", out var values) == true ? values.FirstOrDefault() : null;
            var found = Catalog.Where(p => search is null || p.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            return Json(200, found);
        }));

        backend.Stub(Request.Create().UsingPost().WithPath("/api/orders"), Response.Create().WithCallback(request =>
        {
            using var order = JsonDocument.Parse(request.Body ?? "{}");
            var root = order.RootElement;
            var total = root.GetProperty("items").EnumerateArray()
                .Sum(i => Catalog.Single(p => p.Id == i.GetProperty("productId").GetInt32()).Price * i.GetProperty("quantity").GetInt32());
            total += root.GetProperty("shipping").GetString() == "Express" ? 9.99m : 0;
            total += root.GetProperty("giftWrap").GetBoolean() ? 4.99m : 0;
            return Json(201, new { orderId = OrderId, total });
        }));
    }

    /// <summary>The answer for a test user's correct password (the password comes from the environment, like in the app).</summary>
    private static void SignInAnswer(MockBackend backend, string alias, int status, object body)
    {
        var user = TestUsers.Get(alias);
        backend.Stub(Request.Create().UsingPost().WithPath("/api/login")
                .WithBody(new JsonPartialMatcher(new { username = user.Username, password = user.Password })),
            Response.Create().WithStatusCode(status).WithBodyAsJson(body), priority: 10);
    }

    private static ResponseMessage Json(int status, object body) => new()
    {
        StatusCode = status,
        Headers = new Dictionary<string, WireMockList<string>> { ["Content-Type"] = new("application/json") },
        BodyData = new BodyData
        {
            DetectedBodyType = BodyType.String,
            BodyAsString = JsonSerializer.Serialize(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding = System.Text.Encoding.UTF8,
        },
    };

    public sealed record Product(int Id, string Name, decimal Price, int Stock)
    {
        public string PriceText => "$" + Price.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
