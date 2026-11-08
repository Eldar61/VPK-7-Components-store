using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Api.Services;
using PcShop.Domain;
var builder=WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json",optional:true).AddEnvironmentVariables();
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=512*1024);
builder.Services.AddDbContext<ShopDb>(o=>o.UseNpgsql(builder.Configuration.GetConnectionString("ShopDb")));
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues:false)));
builder.Services.AddSingleton(TimeProvider.System);builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();builder.Services.AddScoped<Transactions>();builder.Services.AddScoped<CatalogService>();builder.Services.AddScoped<CommerceService>();builder.Services.AddScoped<AnalyticsService>();builder.Services.AddScoped<RecommendationService>();
builder.Services.AddHttpClient<AssemblyClient>(client=>{client.BaseAddress=new Uri(builder.Configuration["Assembly:BaseUrl"]??"http://localhost:5080/");client.Timeout=TimeSpan.FromSeconds(8);client.DefaultRequestHeaders.Add("X-Service-Key",builder.Configuration["Integration:ServiceKey"]);});
builder.Services.AddHostedService<ReservationCleanup>();
builder.Services.AddEndpointsApiExplorer();builder.Services.AddSwaggerGen();
builder.Services.AddCors(o=>o.AddDefaultPolicy(p=>p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()??["http://localhost:5174"]).AllowAnyMethod().AllowAnyHeader()));
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.AddPolicy("login",ctx=>RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString()??"local",_=>new FixedWindowRateLimiterOptions{PermitLimit=15,Window=TimeSpan.FromMinutes(1)}));});
var app=builder.Build();var key=app.Configuration["Integration:ServiceKey"];
if(string.IsNullOrWhiteSpace(key)||key.Length<20||!app.Environment.IsDevelopment()&&key.StartsWith("development"))throw new InvalidOperationException("Set Integration__ServiceKey (random, >=20 chars).");
app.Use(async(ctx,next)=>{
 try {await next();}
 catch(ShopException e){ctx.Response.StatusCode=e.Status;await ctx.Response.WriteAsJsonAsync(new{code=e.Code,message=e.Message});}
 catch(DbUpdateConcurrencyException){ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{code="VERSION_CONFLICT",message="Карточка изменилась. Обновите данные."});}
 catch(DbUpdateException e)when(e.InnerException is Npgsql.PostgresException{SqlState:"23505"}){ctx.Response.StatusCode=409;await ctx.Response.WriteAsJsonAsync(new{code="DUPLICATE",message="SKU, логин или операция уже существуют."});}
 catch(BadHttpRequestException){ctx.Response.StatusCode=400;await ctx.Response.WriteAsJsonAsync(new{code="INPUT_INVALID",message="Проверьте JSON и параметры запроса."});}
 catch(Exception e)when(e is not OperationCanceledException){app.Logger.LogError(e,"Request failed {Path}",ctx.Request.Path);ctx.Response.StatusCode=500;await ctx.Response.WriteAsJsonAsync(new{code="SERVER_ERROR",message="Ошибка магазина. Проверьте БД и журнал API."});}
});
app.UseCors();app.UseRateLimiter();app.UseSwagger();app.UseSwaggerUI();
app.Use(async(ctx,next)=>{
 if(ctx.Request.Path.StartsWithSegments("/api/products")||ctx.Request.Path.StartsWithSegments("/api/quotes")||ctx.Request.Path.StartsWithSegments("/api/carts"))
 {if(!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Service-Key"].ToString()),Encoding.UTF8.GetBytes(key))){ctx.Response.StatusCode=401;await ctx.Response.WriteAsJsonAsync(new{code="SERVICE_KEY_INVALID",message="Неверный межсервисный ключ."});return;}}
 await next();
});
app.MapGet("/health",()=>new{service="pc-shop",status="ok"});
app.MapGet("/health/ready",async(ShopDb db,CancellationToken ct)=>await db.Database.CanConnectAsync(ct)?Results.Ok(new{status="ready"}):Results.StatusCode(503));
app.MapPost("/api/auth/register",(Credentials x,AuthService auth,CancellationToken ct)=>auth.Authenticate(x,true,ct)).RequireRateLimiting("login");
app.MapPost("/api/auth/login",(Credentials x,AuthService auth,CancellationToken ct)=>auth.Authenticate(x,false,ct)).RequireRateLimiting("login");
app.MapGet("/api/auth/me",async(AuthService auth,CancellationToken ct)=>AuthService.View(await auth.Require(ct)));
app.MapPost("/api/auth/logout",async(AuthService auth,CancellationToken ct)=>{await auth.Logout(ct);return Results.NoContent();});
app.MapGet("/api/products",(string? ids,CatalogService catalog,CancellationToken ct)=>catalog.Integration(ids,ct));
app.MapPost("/api/quotes",(QuoteRequest x,CommerceService shop,CancellationToken ct)=>shop.Quote(x.Items,"service",ct));
app.MapPost("/api/carts",(CartRequest x,CommerceService shop,CancellationToken ct)=>shop.Create(x.Items,"service",x.OperationKey,x.ExternalConfigurationId,ct));
app.MapGet("/api/carts/{id:guid}",(Guid id,CommerceService shop,CancellationToken ct)=>shop.Get(id,"service",ct));
app.MapPost("/api/carts/{id:guid}/{action}",(Guid id,string action,CartAction x,CommerceService shop,CancellationToken ct)=>shop.Act(id,"service",action,x,ct));
app.MapGet("/api/catalog",(CatalogService catalog,CancellationToken ct)=>catalog.List(false,ct));
app.MapGet("/api/catalog/{id:guid}",async(Guid id,CatalogService catalog,CancellationToken ct)=>(await catalog.List(false,ct)).SingleOrDefault(x=>x.Product.Id==id)??throw new ShopException(404,"PRODUCT_NOT_FOUND","Товар не найден."));
app.MapGet("/api/catalog/{id:guid}/alternatives",(Guid id,int? quantity,RecommendationService recommendations,CancellationToken ct)=>recommendations.Alternatives(id,quantity??1,ct));
app.MapPost("/api/recommendations/builds",(BuildRequest x,RecommendationService recommendations,CancellationToken ct)=>recommendations.Builds(x.BudgetRub,ct));
app.MapPost("/api/customer/quotes",async(QuoteRequest x,AuthService auth,CommerceService shop,CancellationToken ct)=>await shop.Quote(x.Items,(await auth.Require(ct)).Id.ToString(),ct));
app.MapPost("/api/customer/carts",async(CustomerCartRequest x,AuthService auth,CommerceService shop,CancellationToken ct)=>await shop.Create(x.Items,(await auth.Require(ct)).Id.ToString(),x.OperationKey,null,ct));
app.MapGet("/api/customer/carts",async(AuthService auth,CommerceService shop,CancellationToken ct)=>await shop.List((await auth.Require(ct)).Id.ToString(),ct));
app.MapGet("/api/customer/carts/{id:guid}",async(Guid id,AuthService auth,CommerceService shop,CancellationToken ct)=>await shop.Get(id,(await auth.Require(ct)).Id.ToString(),ct));
app.MapPost("/api/customer/carts/{id:guid}/{action}",async(Guid id,string action,CartAction x,AuthService auth,CommerceService shop,CancellationToken ct)=>await shop.Act(id,(await auth.Require(ct)).Id.ToString(),action,x,ct));
app.MapGet("/api/admin/products",async(AuthService auth,CatalogService catalog,CancellationToken ct)=>{await auth.Admin(ct);return await catalog.List(true,ct);});
app.MapPost("/api/admin/products",async(ProductInput x,AuthService auth,CatalogService catalog,CancellationToken ct)=>{await auth.Admin(ct);var p=await catalog.Save(null,x,ct);return Results.Created("/api/catalog/"+p.Product.Id,p);});
app.MapPut("/api/admin/products/{id:guid}",async(Guid id,ProductInput x,AuthService auth,CatalogService catalog,CancellationToken ct)=>{await auth.Admin(ct);return await catalog.Save(id,x,ct);});
app.MapPost("/api/admin/products/{id:guid}/archive",async(Guid id,AuthService auth,CatalogService catalog,CancellationToken ct)=>{await auth.Admin(ct);await catalog.Archive(id,ct);return Results.NoContent();});
app.MapGet("/api/admin/users",async(AuthService auth,ShopDb db,CancellationToken ct)=>{await auth.Admin(ct);return (await db.Users.AsNoTracking().OrderBy(x=>x.Login).ToListAsync(ct)).Select(AuthService.View);});
app.MapPut("/api/admin/users/{id:guid}/customer-type",async(Guid id,CustomerTypeInput input,AuthService auth,ShopDb db,Transactions tx,CancellationToken ct)=>{
 await auth.Admin(ct);if(!Enum.IsDefined(input.CustomerType))throw new ShopException(400,"TYPE_INVALID","Неизвестный тип покупателя.");
 return await tx.Run(async()=>{var user=await db.Users.FindAsync([id],ct)??throw new ShopException(404,"USER_NOT_FOUND","Покупатель не найден.");user.CustomerType=input.CustomerType;return AuthService.View(user);},ct);
});
app.MapGet("/api/admin/analytics",async(DateTimeOffset from,DateTimeOffset to,string? group,Category? category,AuthService auth,AnalyticsService stats,CancellationToken ct)=>{await auth.Admin(ct);return await stats.Get(from,to,group??"day",category,ct);});
if(app.Configuration.GetValue<bool>("Database:Initialize"))
{
 await using var scope=app.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<ShopDb>();await db.Database.EnsureCreatedAsync();await Seeder.Run(db,app.Configuration,app.Environment);
}
app.Run();
public record CustomerTypeInput(CustomerType CustomerType);

