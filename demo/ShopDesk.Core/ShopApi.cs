using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShopDesk;

/// <summary>Client of the ShopDesk REST backend. The examples replace the backend with a WireMock stub.</summary>
public sealed class ShopApi(Uri baseUrl) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient http = new() { BaseAddress = baseUrl, Timeout = TimeSpan.FromSeconds(15) };

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/login", new { username, password }, Json);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
            if (response.IsSuccessStatusCode)
            {
                var token = body.GetProperty("token").GetString();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                return new LoginResult(true, body.GetProperty("displayName").GetString(), token, null);
            }
            var error = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("error", out var e)
                ? e.GetString()
                : $"Sign in failed (HTTP {(int)response.StatusCode})";
            return new LoginResult(false, null, null, error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new LoginResult(false, null, null, "Cannot reach the ShopDesk server");
        }
    }

    public void SignOut() => http.DefaultRequestHeaders.Authorization = null;

    /// <exception cref="ShopApiException">The backend is unreachable or answered with an error.</exception>
    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? search)
    {
        var path = string.IsNullOrWhiteSpace(search) ? "api/products" : "api/products?search=" + Uri.EscapeDataString(search.Trim());
        try
        {
            using var response = await http.GetAsync(path);
            if (!response.IsSuccessStatusCode)
            {
                throw new ShopApiException($"Could not load products (HTTP {(int)response.StatusCode})");
            }
            return await response.Content.ReadFromJsonAsync<List<Product>>(Json) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new ShopApiException("Could not load products: the server is not reachable", ex);
        }
    }

    /// <exception cref="ShopApiException">The order was rejected or the backend is unreachable.</exception>
    public async Task<OrderResult> PlaceOrderAsync(Cart cart, Customer customer, Shipping shipping, bool giftWrap)
    {
        var order = new
        {
            customer,
            shipping = shipping.ToString(),
            giftWrap,
            items = cart.Lines.Select(l => new { productId = l.Product.Id, quantity = l.Quantity }),
        };
        try
        {
            using var response = await http.PostAsJsonAsync("api/orders", order, Json);
            if (response.StatusCode != HttpStatusCode.Created && response.StatusCode != HttpStatusCode.OK)
            {
                throw new ShopApiException($"The server rejected the order (HTTP {(int)response.StatusCode})");
            }
            return await response.Content.ReadFromJsonAsync<OrderResult>(Json)
                   ?? throw new ShopApiException("The server returned an empty response");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new ShopApiException("The server is not reachable", ex);
        }
    }

    public void Dispose() => http.Dispose();
}

public sealed class ShopApiException(string message, Exception? inner = null) : Exception(message, inner);
