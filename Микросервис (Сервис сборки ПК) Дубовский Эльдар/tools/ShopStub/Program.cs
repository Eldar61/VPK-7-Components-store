using PcAssembly.Domain;
using ShopStub;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
var app = builder.Build();
var key = app.Configuration["Integration:ServiceKey"] ?? "development-only-service-key";
var products = DemoCatalog.Products(); var quotes = new Dictionary<Guid, Quote>(); var carts = new Dictionary<Guid, ShopCart>();
var itemsByCart = new Dictionary<Guid, List<Selection>>(); var imports = new Dictionary<string, Guid>(); var operations = new HashSet<string>(); var gate = new object(); bool unavailable = false;
void Expire() { foreach (var cart in carts.Values.ToList()) if (cart.Status == "RESERVED" && cart.ExpiresAt <= DateTimeOffset.UtcNow) carts[cart.Id] = cart with { Status = "EXPIRED" }; }
int Available(Product p) => p.AvailableQuantity - carts.Values.Where(c => c.Status == "RESERVED").Sum(c => itemsByCart[c.Id].Where(i => i.ProductId == p.Id).Sum(i => i.Quantity));
Quote MakeQuote(List<Selection> items)
{
    Expire();
    if (items.Count == 0 || items.Count > 100 || items.Any(i => i.Quantity is < 1 or > 100) || items.GroupBy(i=>i.ProductId).Any(g=>g.Count()>1)) throw new ArgumentException("Invalid items");
    var lines = items.Select(i => { var p = products.Single(p => p.Id == i.ProductId && p.Active && p.Category == i.Category); var discount = i.Quantity >= 5 ? 7 : i.Quantity >= 2 ? 3 : 0; return new QuoteLine(p.Id, i.Quantity, p.PriceRub, discount, Math.Round(p.PriceRub * i.Quantity * (1-discount/100m), 2, MidpointRounding.AwayFromZero), Available(p)>=i.Quantity); }).ToList();
    var quote = new Quote(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(15), lines, lines.Sum(l=>l.TotalRub), lines.All(l=>l.Available)); quotes[quote.Id]=quote; return quote;
}
app.Use(async (ctx,next) => { if (ctx.Request.Path.StartsWithSegments("/api") && ctx.Request.Headers["X-Service-Key"] != key) { ctx.Response.StatusCode=401; return; } if (unavailable && ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode=503; return; } try { await next(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException) { ctx.Response.StatusCode=400; await ctx.Response.WriteAsJsonAsync(new { message="Некорректный состав корзины." }); } });
app.MapGet("/health", () => new { service="shop-stub", mode="test-only" });
app.MapPost("/testing/unavailable/{value:bool}", (bool value, HttpContext ctx) => { if(ctx.Request.Headers["X-Service-Key"]!=key)return Results.Unauthorized(); unavailable=value; return Results.Ok(); });
app.MapGet("/api/products", (string? ids) => { lock(gate) { Expire(); var selected = string.IsNullOrEmpty(ids) ? products : products.Where(p => ids.Split(',').Contains(p.Id.ToString())).ToList(); return selected.Select(p=>p with { AvailableQuantity=Available(p) }).ToList(); } });
app.MapPost("/api/quotes", (QuoteRequest input) => { lock(gate) return MakeQuote(input.Items); });
app.MapPost("/api/carts", (CartRequest input) => { lock(gate) { if(imports.TryGetValue(input.OperationKey,out var id)) return carts[id]; var quote=MakeQuote(input.Items); var cart=new ShopCart(Guid.NewGuid(),"DRAFT",quote,null,null); carts[cart.Id]=cart; itemsByCart[cart.Id]=input.Items; imports[input.OperationKey]=cart.Id; return cart; } });
app.MapGet("/api/carts/{id:guid}", (Guid id) => { lock(gate) { Expire(); return carts.TryGetValue(id,out var cart)?Results.Ok(cart):Results.NotFound(); } });
app.MapPost("/api/carts/{id:guid}/{action}", (Guid id,string action, CartAction input) =>
{
    lock(gate)
    {
        Expire(); if(!carts.TryGetValue(id,out var cart)) return Results.NotFound();
        var op=$"{id}/{action}/{input.OperationKey}"; if(operations.Contains(op)) return Results.Ok(cart);
        if(action=="quote" && cart.Status=="DRAFT") cart=cart with { Quote=MakeQuote(itemsByCart[id]) };
        else if(action=="reserve" && cart.Status=="DRAFT")
        {
            if(cart.Quote?.Id!=input.QuoteId || cart.Quote.ExpiresAt<=DateTimeOffset.UtcNow) return Results.Conflict();
            var fresh=MakeQuote(itemsByCart[id]); if(!fresh.Available || fresh.TotalRub!=cart.Quote.TotalRub) return Results.Conflict();
            cart=cart with { Status="RESERVED",ReservationId=Guid.NewGuid(),ExpiresAt=DateTimeOffset.UtcNow.AddMinutes(15) };
        }
        else if(action=="confirm" && cart.Status=="RESERVED")
        {
            foreach(var item in itemsByCart[id]) { var index=products.FindIndex(p=>p.Id==item.ProductId); products[index]=products[index] with { AvailableQuantity=products[index].AvailableQuantity-item.Quantity }; }
            cart=cart with { Status="CONFIRMED" };
        }
        else if(action=="cancel" && cart.Status is "DRAFT" or "RESERVED") cart=cart with { Status="CANCELLED" };
        else return Results.Conflict();
        carts[id]=cart; operations.Add(op); return Results.Ok(cart);
    }
});
app.Run();
