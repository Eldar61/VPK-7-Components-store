using System.Net.Http.Json;
using System.Text.Json;
using PcAssembly.Domain;
namespace PcAssembly.Api.Services;

public sealed class ShopClient(HttpClient http)
{
    public async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body, options: JsonDefaults.Options);
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500) throw new ApiException(503, "SHOP_UNAVAILABLE", "Магазин временно недоступен. Повторите запрос.");
                var status = response.StatusCode == System.Net.HttpStatusCode.Conflict ? 409 : 502;
                throw new ApiException(status, status == 409 ? "SHOP_CONFLICT" : "SHOP_REJECTED", status == 409 ? "Магазин отклонил действие: цена, остатки или срок предложения изменились. Обновите корзину." : "Магазин отклонил запрос. Проверьте контракт интеграции.");
            }
            return await response.Content.ReadFromJsonAsync<T>(JsonDefaults.Options, ct) ?? throw new JsonException("Empty shop response");
        }
        catch (HttpRequestException) { throw new ApiException(503, "SHOP_UNAVAILABLE", "Нет соединения с магазином. Черновик сохранён."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(503, "SHOP_TIMEOUT", "Магазин не ответил вовремя. Повторите запрос."); }
        catch (JsonException) { throw new ApiException(502, "SHOP_RESPONSE_INVALID", "Ответ магазина не соответствует JSON-контракту."); }
    }
    public Task<List<Product>> Catalog(CancellationToken ct) => Send<List<Product>>(HttpMethod.Get, "api/products", null, ct);
    public Task<List<Product>> Products(List<Selection> items, CancellationToken ct) => Send<List<Product>>(HttpMethod.Get, "api/products?ids=" + string.Join(',', items.Select(i => i.ProductId)), null, ct);
    public async Task<Quote> Quote(List<Selection> items, CancellationToken ct)
    {
        var quote = await Send<Quote>(HttpMethod.Post, "api/quotes", new QuoteRequest(items), ct);
        var expected = items.OrderBy(i => i.ProductId).Select(i => (i.ProductId, i.Quantity));
        if (quote.Id == Guid.Empty || quote.Lines is null || quote.Lines.Any(l => l is null || l.UnitPriceRub is < 0 or > 100000000 || l.TotalRub is < 0 or > 100000000 || l.DiscountPercent is < 0 or > 100 || l.TotalRub != Math.Round(l.UnitPriceRub * l.Quantity * (1-l.DiscountPercent/100m), 2, MidpointRounding.AwayFromZero)) || !expected.SequenceEqual(quote.Lines.OrderBy(i => i.ProductId).Select(i => (i.ProductId, i.Quantity))) || quote.TotalRub is < 0 or > 100000000 || quote.TotalRub != quote.Lines.Sum(l => l.TotalRub) || quote.Available != quote.Lines.All(l => l.Available) || quote.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ApiException(502, "SHOP_QUOTE_INVALID", "Магазин вернул некорректное предложение.");
        return quote;
    }
}
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string value) => JsonSerializer.Deserialize<T>(value, Options)!;
}
