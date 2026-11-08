using Microsoft.EntityFrameworkCore;
using PcAssembly.Api.Data;
using PcAssembly.Domain;
namespace PcAssembly.Api.Services;

public record ConfigurationInput(string Name, decimal BudgetRub, List<Selection> Items, int Version = 0);
public record Replacement(Guid ProductId, Guid ReplacementId, string Name, decimal EstimatedPriceRub, string Reason);
public record ConfigurationView(Guid Id, string Name, decimal BudgetRub, List<Selection> Items, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? SavedAt, decimal? SavedPriceRub, int Version, Guid? CartId, Evaluation? Evaluation, List<Product>? Products);
public sealed class ConfigurationService(AssemblyDb db, SessionService sessions, ShopClient shop, CompatibilityChecker checker, RecommendationService recommendations)
{
    public async Task<PCConfiguration> Owned(Guid id, CancellationToken ct)
    {
        var user = await sessions.RequireUser(ct);
        return await db.Configurations.SingleOrDefaultAsync(c => c.Id == id && c.UserId == user.Id, ct) ?? throw new ApiException(404, "CONFIGURATION_NOT_FOUND", "Конфигурация не найдена.");
    }
    public async Task<PCConfiguration> Create(ConfigurationInput input, CancellationToken ct)
    {
        ValidateInput(input);
        var user = await sessions.RequireUser(ct);
        var entity = new PCConfiguration { UserId = user.Id, Name = input.Name.Trim(), BudgetRub = input.BudgetRub, ItemsJson = JsonDefaults.Write(input.Items) };
        db.Configurations.Add(entity); await db.SaveChangesAsync(ct); return entity;
    }
    public async Task<PCConfiguration> Update(Guid id, ConfigurationInput input, CancellationToken ct)
    {
        ValidateInput(input); var entity = await Owned(id, ct); Mutable(entity);
        if (entity.Version != input.Version) throw new ApiException(409, "VERSION_CONFLICT", "Черновик изменился в другой вкладке. Откройте его заново.");
        entity.Name = input.Name.Trim(); entity.BudgetRub = input.BudgetRub; entity.ItemsJson = JsonDefaults.Write(input.Items); entity.EvaluationJson = null; entity.SavedProductsJson = null; entity.Status = "DRAFT";
        Touch(entity); await db.SaveChangesAsync(ct); return entity;
    }
    public async Task<Evaluation> Evaluate(Guid id, CancellationToken ct)
    {
        var entity = await Owned(id, ct); Mutable(entity);
        var items = JsonDefaults.Read<List<Selection>>(entity.ItemsJson);
        // Fetch first. A transport failure must not convert a configuration to INVALID.
        var products = items.Count == 0 ? [] : await shop.Products(items, ct);
        var technical = checker.Check(new(items, products));
        Quote? quote = null; string? externalError = null;
        if (technical.Compatible)
        {
            try { quote = await shop.Quote(items, ct); }
            catch (ApiException e) { externalError = e.Message; }
        }
        var result = new Evaluation(technical, quote, quote?.Available, quote is null ? null : quote.TotalRub <= entity.BudgetRub, externalError, DateTimeOffset.UtcNow);
        entity.Status = technical.Compatible ? "VALID" : "INVALID";
        entity.EvaluationJson = JsonDefaults.Write(result); entity.SavedProductsJson = JsonDefaults.Write(products);
        Touch(entity); await db.SaveChangesAsync(ct); return result;
    }
    public async Task<PCConfiguration> Save(Guid id, CancellationToken ct)
    {
        var entity = await Owned(id, ct);
        if (entity.SavedAt.HasValue) return entity; // Repeating save does not create a second statistic.
        var evaluation = entity.EvaluationJson is null ? null : JsonDefaults.Read<Evaluation>(entity.EvaluationJson);
        if (evaluation?.Technical.Compatible != true || evaluation.Quote is null) throw new ApiException(409, "CHECK_REQUIRED", "Перед сохранением выполните успешную полную проверку с расчётом цены.");
        if (evaluation.Quote.ExpiresAt <= DateTimeOffset.UtcNow) throw new ApiException(409, "QUOTE_EXPIRED", "Предложение истекло. Повторите проверку и подтвердите новую цену.");
        entity.SavedAt = DateTimeOffset.UtcNow; entity.SavedPriceRub = evaluation.Quote.TotalRub; entity.Status = "SAVED"; Touch(entity);
        await db.SaveChangesAsync(ct); return entity;
    }
    public async Task<List<Replacement>> Recommend(Guid id, CancellationToken ct)
    {
        var entity = await Owned(id, ct); var items = JsonDefaults.Read<List<Selection>>(entity.ItemsJson);
        return await recommendations.Find(items, entity.BudgetRub, ct);
    }
    public async Task<ShopCart> Cart(Guid id, string action, CartAction? input, CancellationToken ct)
    {
        var entity = await Owned(id, ct);
        if (!entity.SavedAt.HasValue) throw new ApiException(409, "SAVE_REQUIRED", "Сначала сохраните проверенную сборку.");
        if (action == "create")
        {
            if (entity.CartId.HasValue) return await shop.Send<ShopCart>(HttpMethod.Get, $"api/carts/{entity.CartId}", null, ct);
            var cart = await shop.Send<ShopCart>(HttpMethod.Post, "api/carts", new CartRequest(entity.Id, JsonDefaults.Read<List<Selection>>(entity.ItemsJson), entity.CartOperationKey), ct);
            entity.CartId = cart.Id; Touch(entity); await db.SaveChangesAsync(ct); return cart;
        }
        if (!entity.CartId.HasValue) throw new ApiException(409, "CART_REQUIRED", "Сначала передайте сборку в магазин.");
        if (action == "status") return await shop.Send<ShopCart>(HttpMethod.Get, $"api/carts/{entity.CartId}", null, ct);
        if (action is not ("reserve" or "confirm" or "cancel" or "quote")) throw new ApiException(400, "ACTION_INVALID", "Неизвестное действие.");
        if (input is null || string.IsNullOrWhiteSpace(input.OperationKey) || input.OperationKey.Length > 100) throw new ApiException(400, "OPERATION_KEY_REQUIRED", "Передайте ключ операции.");
        return await shop.Send<ShopCart>(HttpMethod.Post, $"api/carts/{entity.CartId}/{action}", input, ct);
    }
    public static ConfigurationView View(PCConfiguration e) => new(e.Id, e.Name, e.BudgetRub, JsonDefaults.Read<List<Selection>>(e.ItemsJson), e.Status, e.CreatedAt, e.UpdatedAt, e.SavedAt, e.SavedPriceRub, e.Version, e.CartId, e.EvaluationJson is null ? null : JsonDefaults.Read<Evaluation>(e.EvaluationJson), e.SavedProductsJson is null ? null : JsonDefaults.Read<List<Product>>(e.SavedProductsJson));
    private static void Mutable(PCConfiguration e) { if (e.SavedAt.HasValue) throw new ApiException(409, "CONFIGURATION_IMMUTABLE", "Сохранённая сборка неизменяема. Создайте копию."); }
    private static void Touch(PCConfiguration e) { e.Version++; e.UpdatedAt = DateTimeOffset.UtcNow; }
    private static void ValidateInput(ConfigurationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 120 || input.BudgetRub is <= 0 or > 100000000 || input.Items is null || input.Items.Count > 100 || input.Items.Any(i => i is null || i.Quantity is < 1 or > 100 || i.ProductId == Guid.Empty || !Enum.IsDefined(i.Category)) || input.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
            throw new ApiException(400, "INPUT_INVALID", "Проверьте название (до 120 символов), бюджет (0–100 млн ₽), уникальные позиции и количества (1–100).");
    }
}
