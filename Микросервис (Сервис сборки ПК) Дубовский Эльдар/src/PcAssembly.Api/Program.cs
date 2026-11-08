using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PcAssembly.Api.Data;
using PcAssembly.Api.Services;
using PcAssembly.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 256 * 1024);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true).AddEnvironmentVariables();
builder.Services.AddDbContext<AssemblyDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("AssemblyDb") ?? throw new InvalidOperationException("ConnectionStrings:AssemblyDb is required.")));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<SessionService>(); builder.Services.AddScoped<ConfigurationService>(); builder.Services.AddSingleton<CompatibilityChecker>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddHttpClient<ShopClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Shop:BaseUrl"] ?? "http://localhost:5081/");
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.Add("X-Service-Key", builder.Configuration["Integration:ServiceKey"]);
});
builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions { PermitLimit = 15, Window = TimeSpan.FromMinutes(1) }));
});
var app = builder.Build();
var serviceKey = app.Configuration["Integration:ServiceKey"];
if (string.IsNullOrWhiteSpace(serviceKey) || serviceKey.Length < 20 || (!app.Environment.IsDevelopment() && serviceKey.StartsWith("development"))) throw new InvalidOperationException("Set a random Integration__ServiceKey of at least 20 characters.");
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (ApiException e) { ctx.Response.StatusCode = e.Status; await ctx.Response.WriteAsJsonAsync(new { code = e.Code, message = e.Message }); }
    catch (DbUpdateConcurrencyException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { code = "VERSION_CONFLICT", message = "Данные изменились. Откройте конфигурацию заново." }); }
    catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { code = "DUPLICATE_RECORD", message = "Запись с такими данными уже существует." }); }
    catch (BadHttpRequestException) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { code = "JSON_INVALID", message = "Проверьте формат запроса." }); }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        app.Logger.LogError(e, "Request failed: {Path}", ctx.Request.Path);
        ctx.Response.StatusCode = 500; await ctx.Response.WriteAsJsonAsync(new { code = "SERVER_ERROR", message = "Сервис не смог обработать запрос. Проверьте подключение к базе данных и журнал сервера." });
    }
});
app.UseCors(); app.UseRateLimiter();
app.UseSwagger(); app.UseSwaggerUI();
app.MapGet("/health", () => Results.Ok(new { service = "pc-assembly", status = "ok" }));
app.MapGet("/health/ready", async (AssemblyDb db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapPost("/api/auth/register", (Credentials body, SessionService service, CancellationToken ct) => service.Authenticate(body, true, ct)).RequireRateLimiting("login");
app.MapPost("/api/auth/login", (Credentials body, SessionService service, CancellationToken ct) => service.Authenticate(body, false, ct)).RequireRateLimiting("login");
app.MapPost("/api/auth/logout", async (HttpContext ctx, SessionService service, AssemblyDb db, CancellationToken ct) =>
{
    await service.RequireUser(ct); var token = ctx.Request.Headers.Authorization.ToString()[7..];
    await db.Sessions.Where(s => s.TokenHash == SessionService.Hash(token)).ExecuteDeleteAsync(ct); return Results.NoContent();
});
app.MapGet("/api/auth/me", async (SessionService service, CancellationToken ct) => { var user = await service.RequireUser(ct); return new { user.Id, user.Login }; });
app.MapGet("/api/products", (ShopClient client, CancellationToken ct) => client.Catalog(ct));
app.MapPost("/api/compatibility/check", (CheckRequest body, CompatibilityChecker checker, HttpContext context) =>
{
    var provided = context.Request.Headers["X-Service-Key"].ToString();
    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(provided), System.Text.Encoding.UTF8.GetBytes(serviceKey))) return Results.Unauthorized();
    if (body.Items is null || body.Products is null) return Results.BadRequest(new { code = "INPUT_INVALID", message = "Передайте items и products." });
    return Results.Ok(checker.Check(body));
});
app.MapGet("/api/configurations", async (SessionService sessions, AssemblyDb db, CancellationToken ct) =>
{
    var user = await sessions.RequireUser(ct); var list = await db.Configurations.AsNoTracking().Where(c => c.UserId == user.Id).OrderByDescending(c => c.UpdatedAt).ToListAsync(ct); return list.Select(ConfigurationService.View);
});
app.MapPost("/api/configurations", async (ConfigurationInput body, ConfigurationService service, CancellationToken ct) => { var entity = await service.Create(body, ct); return Results.Created($"/api/configurations/{entity.Id}", ConfigurationService.View(entity)); });
app.MapGet("/api/configurations/{id:guid}", async (Guid id, ConfigurationService service, CancellationToken ct) => ConfigurationService.View(await service.Owned(id, ct)));
app.MapPut("/api/configurations/{id:guid}", async (Guid id, ConfigurationInput body, ConfigurationService service, CancellationToken ct) => ConfigurationService.View(await service.Update(id, body, ct)));
app.MapPost("/api/configurations/{id:guid}/evaluate", (Guid id, ConfigurationService service, CancellationToken ct) => service.Evaluate(id, ct));
app.MapPost("/api/configurations/{id:guid}/save", async (Guid id, ConfigurationService service, CancellationToken ct) => ConfigurationService.View(await service.Save(id, ct)));
app.MapPost("/api/configurations/{id:guid}/clone", async (Guid id, ConfigurationService service, CancellationToken ct) =>
{
    var source = await service.Owned(id, ct); return ConfigurationService.View(await service.Create(new(source.Name[..Math.Min(110, source.Name.Length)] + " — копия", source.BudgetRub, JsonDefaults.Read<List<Selection>>(source.ItemsJson)), ct));
});
app.MapGet("/api/configurations/{id:guid}/recommendations", (Guid id, ConfigurationService service, CancellationToken ct) => service.Recommend(id, ct));
app.MapPost("/api/configurations/{id:guid}/cart", (Guid id, ConfigurationService service, CancellationToken ct) => service.Cart(id, "create", null, ct));
app.MapGet("/api/configurations/{id:guid}/cart", (Guid id, ConfigurationService service, CancellationToken ct) => service.Cart(id, "status", null, ct));
app.MapPost("/api/configurations/{id:guid}/cart/{action}", (Guid id, string action, CartAction body, ConfigurationService service, CancellationToken ct) => service.Cart(id, action, body, ct));
app.MapGet("/api/statistics/price-buckets", async (AssemblyDb db, CancellationToken ct) => PriceStatistics.Calculate(await db.Configurations.AsNoTracking().Where(c => c.SavedAt != null && c.SavedPriceRub != null).Select(c => c.SavedPriceRub!.Value).ToListAsync(ct)));
if (app.Configuration.GetValue<bool>("Database:Initialize"))
{
    using var scope = app.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AssemblyDb>().Database.EnsureCreatedAsync();
}
app.Run();
