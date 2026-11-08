using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Domain;
namespace PcShop.Api.Services;
public record ProductInput(string Sku,string Name,Category Category,bool Active,decimal PriceRub,int StockQuantity,Specifications Specs,string SourceUrl,string SourceNotes,string PowerBasis,decimal PromotionPercent=0,DateTimeOffset? PromotionFrom=null,DateTimeOffset? PromotionTo=null,int Version=0);
public record CatalogView(Product Product,string Sku,string SourceUrl,string SourceNotes,string PowerBasis,decimal PromotionPercent,DateTimeOffset? PromotionFrom,DateTimeOffset? PromotionTo,int? StockQuantity,int Version);
public sealed class CatalogService(ShopDb db,Transactions tx,TimeProvider time)
{
 public async Task<Dictionary<Guid,int>> Reserved(CancellationToken ct)
 {
  var now=time.GetUtcNow();var carts=await db.Carts.AsNoTracking().Where(x=>x.Status=="RESERVED"&&x.ExpiresAt>now).Select(x=>x.ItemsJson).ToListAsync(ct);
  return carts.SelectMany(x=>Json.Read<List<Selection>>(x)).GroupBy(x=>x.ProductId).ToDictionary(g=>g.Key,g=>g.Sum(x=>x.Quantity));
 }
 public static Product Product(ProductEntity p,Dictionary<Guid,int> reserved)=>new(p.Id,p.Name,p.Category,p.Active,p.PriceRub,Math.Max(0,p.StockQuantity-reserved.GetValueOrDefault(p.Id)),Json.Read<Specifications>(p.SpecsJson));
 public static CatalogView View(ProductEntity p,Dictionary<Guid,int> reserved,bool admin=false)=>new(Product(p,reserved),p.Sku,p.SourceUrl,p.SourceNotes,p.PowerBasis,p.PromotionPercent,p.PromotionFrom,p.PromotionTo,admin?p.StockQuantity:null,p.Version);
 public async Task<List<CatalogView>> List(bool admin,CancellationToken ct)
 {
  var stock=await Reserved(ct);return (await db.Products.AsNoTracking().Where(x=>admin||x.Active).OrderBy(x=>x.Category).ThenBy(x=>x.PriceRub).ThenBy(x=>x.Id).ToListAsync(ct)).Select(x=>View(x,stock,admin)).ToList();
 }
 public async Task<List<Product>> Integration(string? ids,CancellationToken ct)
 {
  var stock=await Reserved(ct);var q=db.Products.AsNoTracking();
  if(!string.IsNullOrWhiteSpace(ids)) {var parts=ids.Split(',');if(parts.Length>100||parts.Any(x=>!Guid.TryParse(x,out _)))throw new ShopException(400,"IDS_INVALID","Передайте до 100 UUID через запятую.");var keys=parts.Select(Guid.Parse).Distinct().ToList();q=q.Where(x=>keys.Contains(x.Id));}
  return (await q.OrderBy(x=>x.Name).ToListAsync(ct)).Select(x=>Product(x,stock)).ToList();
 }
 public static void Validate(ProductInput x)
 {
  if(string.IsNullOrWhiteSpace(x.Name)||x.Name.Length>200||string.IsNullOrWhiteSpace(x.Sku)||x.Sku.Length>100||!Enum.IsDefined(x.Category)||x.PriceRub is <0 or >100000000||decimal.Round(x.PriceRub,2)!=x.PriceRub||x.StockQuantity is <0 or >1000000||x.PromotionPercent is <0 or >100||x.PromotionPercent!=decimal.Round(x.PromotionPercent,2)||x.Specs==null)
   throw new ShopException(400,"PRODUCT_INVALID","Проверьте название, SKU, категорию, цену, остаток и процент скидки.");
  if(x.PromotionPercent>0&&(!x.PromotionFrom.HasValue||!x.PromotionTo.HasValue||x.PromotionTo<=x.PromotionFrom))throw new ShopException(400,"PROMOTION_INVALID","Для акции нужен положительный период [from,to).");
  if(x.SourceUrl?.Length>2000||x.SourceNotes?.Length>3000||x.PowerBasis?.Length>2000||!Uri.TryCreate(x.SourceUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https")throw new ShopException(400,"SOURCE_INVALID","Укажите HTTPS-ссылку на источник.");
  var s=x.Specs;bool Has(string? v)=>!string.IsNullOrWhiteSpace(v);bool Dict(Dictionary<string,int>? d)=>d!=null&&d.All(k=>Has(k.Key)&&k.Value>=0&&k.Value<=100);
  var valid=x.Category switch {
   Category.CPU=>Has(s.Socket),
   Category.MOTHERBOARD=>Has(s.Socket)&&Has(s.MemoryType)&&s.MemorySlots>0&&s.MaxMemoryGb>0&&s.PcieX16Slots>=0&&Has(s.FormFactor)&&s.SataPorts>=0&&s.M2Slots>=0,
   Category.RAM=>Has(s.MemoryType)&&s.ModuleCapacityGb>0,
   Category.GPU=>s.GpuLengthMm>0&&Dict(s.RequiredPowerConnectors),
   Category.PSU=>s.RatedPowerW>0&&Has(s.FormFactor)&&Dict(s.PowerConnectors),
   Category.CASE=>s.SupportedMotherboardForms is {Length:>0}&&s.SupportedMotherboardForms.All(Has)&&s.SupportedPsuForms is {Length:>0}&&s.SupportedPsuForms.All(Has)&&s.MaxGpuLengthMm>0&&Dict(s.DriveBays),
   Category.STORAGE=>(s.StorageInterface=="M2"&&s.DriveForm=="M2")||(s.StorageInterface=="SATA"&&s.DriveForm is "2.5" or "3.5"),_=>false};
  if(x.Category!=Category.PSU)valid &= s.EstimatedPowerW.HasValue&&s.EstimatedPowerW>=0&&s.EstimatedPowerW<=10000&&(x.Category==Category.CASE||s.EstimatedPowerW>0);
  if(!valid)throw new ShopException(400,"SPEC_INVALID","Не заполнены обязательные характеристики категории.");
 }
 public Task<CatalogView> Save(Guid? id,ProductInput x,CancellationToken ct)=>tx.Run(async()=>{
  Validate(x);var p=id.HasValue?await db.Products.SingleOrDefaultAsync(p=>p.Id==id,ct)??throw new ShopException(404,"PRODUCT_NOT_FOUND","Товар не найден."):new ProductEntity{Id=Guid.NewGuid()};
  if(id.HasValue&&p.Version!=x.Version)throw new ShopException(409,"VERSION_CONFLICT","Откройте актуальную карточку.");
  var reserved=await Reserved(ct);if(x.StockQuantity<reserved.GetValueOrDefault(p.Id))throw new ShopException(409,"STOCK_RESERVED","Физический остаток не может быть меньше действующих резервов.");
  p.Sku=x.Sku.Trim();p.Name=x.Name.Trim();p.Category=x.Category;p.Active=x.Active;p.PriceRub=x.PriceRub;p.StockQuantity=x.StockQuantity;p.SpecsJson=Json.Write(x.Specs);p.SourceUrl=x.SourceUrl;p.SourceNotes=x.SourceNotes??"";p.PowerBasis=x.PowerBasis??"";p.PromotionPercent=x.PromotionPercent;p.PromotionFrom=x.PromotionFrom?.ToUniversalTime();p.PromotionTo=x.PromotionTo?.ToUniversalTime();p.Version++;
  if(!id.HasValue)db.Products.Add(p);return View(p,reserved,true);
 },ct);
 public Task<bool> Archive(Guid id,CancellationToken ct)=>tx.Run(async()=>{var p=await db.Products.FindAsync([id],ct)??throw new ShopException(404,"PRODUCT_NOT_FOUND","Товар не найден.");p.Active=false;p.Version++;return true;},ct);
}

