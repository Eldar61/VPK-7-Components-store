using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Domain;
namespace PcShop.Api.Services;
public sealed class CommerceService(ShopDb db,Transactions tx,CatalogService catalog,TimeProvider time)
{
 public static void ValidateItems(List<Selection>? items)
 {
  if(items==null||items.Count is <1 or >100||items.Any(i=>i==null||i.ProductId==Guid.Empty||!Enum.IsDefined(i.Category)||i.Quantity is <1 or >100)||items.Select(x=>x.ProductId).Distinct().Count()!=items.Count)
   throw new ShopException(400,"ITEMS_INVALID","Нужно 1–100 уникальных позиций с количеством 1–100 и верной категорией.");
 }
 public static void ValidateKey(string? key){if(string.IsNullOrWhiteSpace(key)||key.Length>128)throw new ShopException(400,"OPERATION_KEY_REQUIRED","Нужен ключ операции длиной до 128 символов.");}
 public async Task Expire(CancellationToken ct) {var now=time.GetUtcNow();await db.Carts.Where(x=>x.Status=="RESERVED"&&x.ExpiresAt<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"EXPIRED"),ct);}
 async Task<CustomerType> Type(string owner,CancellationToken ct)
 {
  if(owner=="service")return CustomerType.STANDARD;
  var id=Guid.Parse(owner);return (await db.Users.AsNoTracking().SingleAsync(x=>x.Id==id,ct)).CustomerType;
 }
 public static ShopCart View(CartEntity c)=>new(c.Id,c.Status,c.QuoteJson==null?null:Json.Read<Quote>(c.QuoteJson),c.ReservationId,c.ExpiresAt);
 async Task<CartEntity> Owned(Guid id,string owner,CancellationToken ct)=>await db.Carts.SingleOrDefaultAsync(x=>x.Id==id&&x.Owner==owner,ct)??throw new ShopException(404,"CART_NOT_FOUND","Корзина не найдена.");
 async Task<ShopCart> Idempotent(string owner,string key,string request,Func<Task<ShopCart>> action,CancellationToken ct)
 {
  ValidateKey(key);var scope=AuthService.Hash(owner+"/"+key);var hash=AuthService.Hash(request);
  var old=await db.Operations.FindAsync([scope],ct);
  if(old!=null) {if(old.RequestHash!=hash)throw new ShopException(409,"KEY_REUSED","Этот ключ уже использован для другого запроса.");return Json.Read<ShopCart>(old.ResponseJson);}
  var response=await action();db.Operations.Add(new(){Key=scope,RequestHash=hash,ResponseJson=Json.Write(response)});return response;
 }
 public async Task<Quote> MakeQuote(List<Selection> items,string owner,Guid? cartId,CancellationToken ct)
 {
  ValidateItems(items);var ids=items.Select(i=>i.ProductId).ToList();var products=await db.Products.Where(p=>ids.Contains(p.Id)).ToListAsync(ct);
  var reserved=await catalog.Reserved(ct);var type=await Type(owner,ct);var now=time.GetUtcNow();var lines=new List<QuoteLine>();
  foreach(var i in items)
  {
   var p=products.SingleOrDefault(p=>p.Id==i.ProductId&&p.Active&&p.Category==i.Category)??throw new ShopException(400,"PRODUCT_INVALID","Товар не найден, архивирован или категория не совпадает.");
   var promo=p.PromotionFrom<=now&&p.PromotionTo>now?p.PromotionPercent:0;
   var discount=Pricing.Discount(type,i.Quantity,promo);
   lines.Add(new(p.Id,i.Quantity,p.PriceRub,discount,Pricing.Total(p.PriceRub,i.Quantity,discount),p.StockQuantity-reserved.GetValueOrDefault(p.Id)>=i.Quantity));
  }
  var quote=new Quote(Guid.NewGuid(),now.AddMinutes(15),lines,lines.Sum(x=>x.TotalRub),lines.All(x=>x.Available));
  db.Quotes.Add(new(){Id=quote.Id,Owner=owner,CartId=cartId,ItemsJson=Json.Write(items),QuoteJson=Json.Write(quote),ExpiresAt=quote.ExpiresAt});return quote;
 }
 public Task<Quote> Quote(List<Selection> items,string owner,CancellationToken ct)=>tx.Run(async()=>{await Expire(ct);return await MakeQuote(items,owner,null,ct);},ct);
 public Task<ShopCart> Create(List<Selection> items,string owner,string operationKey,Guid? external,CancellationToken ct)=>tx.Run(async()=>{
  ValidateItems(items);if(external==Guid.Empty)throw new ShopException(400,"CONFIGURATION_ID_INVALID","Нужен UUID внешней конфигурации.");
  return await Idempotent(owner,operationKey,"create/"+external+"/"+Json.Write(items.OrderBy(x=>x.ProductId)),async()=>{
   await Expire(ct);var cart=new CartEntity{Owner=owner,ExternalConfigurationId=external,ItemsJson=Json.Write(items)};
   cart.QuoteJson=Json.Write(await MakeQuote(items,owner,cart.Id,ct));db.Carts.Add(cart);return View(cart);
  },ct);
 },ct);
 public Task<ShopCart> Get(Guid id,string owner,CancellationToken ct)=>tx.Run(async()=>{await Expire(ct);return View(await Owned(id,owner,ct));},ct);
 public Task<List<ShopCart>> List(string owner,CancellationToken ct)=>tx.Run(async()=>{await Expire(ct);return (await db.Carts.AsNoTracking().Where(x=>x.Owner==owner).OrderByDescending(x=>x.CreatedAt).ToListAsync(ct)).Select(View).ToList();},ct);
 public Task<ShopCart> Act(Guid id,string owner,string action,CartAction input,CancellationToken ct)=>tx.Run(async()=>{
  if(action is not ("quote" or "reserve" or "confirm" or "cancel"))throw new ShopException(400,"ACTION_INVALID","Неизвестное действие.");
  await Owned(id,owner,ct);
  return await Idempotent(owner,input.OperationKey,id+"/"+action+"/"+input.QuoteId,async()=>{
   await Expire(ct);
   // Expire is a direct SQL update, so remove any tracked stale cart before re-reading.
   foreach(var e in db.ChangeTracker.Entries<CartEntity>().Where(e=>e.Entity.Id==id).ToList())e.State=EntityState.Detached;
   var cart=await Owned(id,owner,ct);var items=Json.Read<List<Selection>>(cart.ItemsJson);var now=time.GetUtcNow();
   var current=cart.QuoteJson==null?null:Json.Read<Quote>(cart.QuoteJson);
   if(action=="quote")
   {
    if(cart.Status!="DRAFT")throw new ShopException(409,"CART_STATE","Новое предложение доступно только для черновика.");
    cart.QuoteJson=Json.Write(await MakeQuote(items,owner,id,ct));
   }
   else if(action=="reserve")
   {
    if(cart.Status!="DRAFT")throw new ShopException(409,"CART_STATE","Резерв доступен только для черновика.");
    if(current==null||current.Id!=input.QuoteId||current.ExpiresAt<=now)throw new ShopException(409,"QUOTE_EXPIRED","Предложение истекло или не принадлежит корзине.");
    Quote fresh;
    try {fresh=await MakeQuote(items,owner,id,ct);}
    catch(ShopException e) when(e.Code=="PRODUCT_INVALID") {throw new ShopException(409,"PRODUCT_CHANGED","Состав изменился или товар архивирован. Резерв не создан.");}
    if(!fresh.Available)throw new ShopException(409,"OUT_OF_STOCK","Не хватает товара. Ни одна позиция не зарезервирована.");
    if(!Pricing.SamePrice(current,fresh))throw new ShopException(409,"PRICE_CHANGED","Цены или скидки изменились. Запросите новое предложение.");
    cart.Status="RESERVED";cart.ReservationId=Guid.NewGuid();cart.ExpiresAt=now.AddMinutes(15);
    var ids=items.Select(i=>i.ProductId).ToList();cart.ReservedProductsJson=Json.Write((await db.Products.AsNoTracking().Where(p=>ids.Contains(p.Id)).ToListAsync(ct)).Select(p=>new ReservedProduct(p.Id,p.Name,p.Category)).ToList());
   }
   else if(action=="confirm")
   {
    if(cart.Status!="RESERVED"||cart.ExpiresAt<=now||current==null||current.Id!=input.QuoteId)throw new ShopException(409,"RESERVATION_INVALID","Нет действующего резерва с этим предложением.");
    var snapshot=Json.Read<List<ReservedProduct>>(cart.ReservedProductsJson!);
    foreach(var line in current.Lines)
    {
     var p=await db.Products.SingleAsync(p=>p.Id==line.ProductId,ct);
     if(p.StockQuantity<line.Quantity)throw new ShopException(409,"STOCK_CONFLICT","Нарушен складской остаток.");
     p.StockQuantity-=line.Quantity;p.Version++;var s=snapshot.Single(p=>p.Id==line.ProductId);
     db.Sales.Add(new(){OrderId=cart.Id,ProductId=line.ProductId,Name=s.Name,Category=s.Category,Quantity=line.Quantity,UnitPriceRub=line.UnitPriceRub,DiscountPercent=line.DiscountPercent,UnitPriceAfterDiscountRub=Pricing.Total(line.UnitPriceRub,1,line.DiscountPercent),TotalRub=line.TotalRub,ConfirmedAt=now});
    }
    cart.Status="CONFIRMED";cart.ConfirmedAt=now;
   }
   else
   {
    if(cart.Status is not ("DRAFT" or "RESERVED"))throw new ShopException(409,"CART_STATE","Эту корзину уже нельзя отменить.");
    cart.Status="CANCELLED";
   }
   return View(cart);
  },ct);
 },ct);
 public record ReservedProduct(Guid Id,string Name,Category Category);
}

