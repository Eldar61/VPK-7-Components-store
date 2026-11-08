using PcAssembly.Domain;
namespace PcAssembly.Api.Services;

public sealed class RecommendationService(ShopClient shop, CompatibilityChecker checker)
{
    public async Task<List<Replacement>> Find(List<Selection> items, decimal budgetRub, CancellationToken ct)
    {
        var catalog = await shop.Catalog(ct);
        var result = new List<Replacement>();
        foreach (var original in items)
        {
            var oldPrice = catalog.FirstOrDefault(p => p.Id == original.ProductId)?.PriceRub ?? 0;
            var candidates = catalog.Where(p => p.Active && p.Id != original.ProductId && p.Category == original.Category && p.AvailableQuantity >= original.Quantity)
                .OrderBy(p => Math.Abs(p.PriceRub - oldPrice)).ThenBy(p => p.Id).Take(30);
            foreach (var product in candidates)
            {
                var changed = items.Select(i => i.ProductId == original.ProductId ? i with { ProductId = product.Id } : i).ToList();
                if (changed.GroupBy(i => i.ProductId).Any(g => g.Count() > 1)) continue;
                var selectedProducts = catalog.Where(p => changed.Any(i => i.ProductId == p.Id)).ToList();
                if (!checker.Check(new(changed, selectedProducts)).Compatible) continue;
                var quote = await shop.Quote(changed, ct);
                if (!quote.Available || quote.TotalRub > budgetRub) continue;
                result.Add(new(original.ProductId, product.Id, product.Name, quote.TotalRub, "Вся сборка прошла проверку, доступна и укладывается в бюджет."));
                if (result.Count == 3) return result;
            }
        }
        return result;
    }
}
