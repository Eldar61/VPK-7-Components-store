using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Domain;
namespace PcShop.Api.Services;
public sealed class RecommendationService(ShopDb db,CatalogService catalog,AssemblyClient assembly,CommerceService commerce,TimeProvider time)
{
 public async Task<List<Product>> Alternatives(Guid id,int quantity,CancellationToken ct)
 {
  if(quantity is <1 or >100)throw new ShopException(400,"QUANTITY_INVALID","Количество 1–100.");
  var source=await db.Products.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ShopException(404,"PRODUCT_NOT_FOUND","Товар не найден.");
  var reserved=await catalog.Reserved(ct);var since=time.GetUtcNow().AddDays(-90);
  var popularity=await db.Sales.Where(x=>x.ConfirmedAt>=since).GroupBy(x=>x.ProductId).Select(g=>new{Id=g.Key,Units=g.Sum(x=>x.Quantity)}).ToDictionaryAsync(x=>x.Id,x=>x.Units,ct);
  return (await db.Products.AsNoTracking().Where(x=>x.Active&&x.Category==source.Category&&x.Id!=id).ToListAsync(ct)).Where(x=>x.StockQuantity-reserved.GetValueOrDefault(x.Id)>=quantity).OrderByDescending(x=>popularity.GetValueOrDefault(x.Id)).ThenBy(x=>Math.Abs(x.PriceRub-source.PriceRub)).ThenBy(x=>x.Id).Take(10).Select(x=>CatalogService.Product(x,reserved)).ToList();
 }
 public async Task<List<BuildSuggestion>> Builds(decimal budget,CancellationToken ct)
 {
  if(budget is <=0 or >100000000)throw new ShopException(400,"BUDGET_INVALID","Бюджет должен быть положительным и не больше 100 млн ₽.");
  var products=(await catalog.List(false,ct)).Select(x=>x.Product).Where(x=>x.AvailableQuantity>0).OrderBy(x=>x.PriceRub).ThenBy(x=>x.Id).ToList();
  var result=new List<BuildSuggestion>();var tried=new HashSet<string>();int checks=0;
  // Bounded deterministic search. The partner remains the final compatibility authority.
  foreach(var cpu in products.Where(x=>x.Category==Category.CPU).Take(12))
  foreach(var board in products.Where(x=>x.Category==Category.MOTHERBOARD&&x.Specs.Socket==cpu.Specs.Socket).Take(3))
  foreach(var gpu in products.Where(x=>x.Category==Category.GPU).Take(3))
  {
   var ram=products.FirstOrDefault(x=>x.Category==Category.RAM&&x.Specs.MemoryType==board.Specs.MemoryType);
   var disk=products.FirstOrDefault(x=>x.Category==Category.STORAGE&&((x.Specs.StorageInterface=="M2"&&board.Specs.M2Slots>0)||(x.Specs.StorageInterface=="SATA"&&board.Specs.SataPorts>0)));
   if(ram==null||disk==null)continue;
   var baseParts=new[]{cpu,board,ram,gpu,disk};var power=baseParts.Sum(x=>x.Specs.EstimatedPowerW??0);
   var box=products.FirstOrDefault(x=>x.Category==Category.CASE&&x.Specs.SupportedMotherboardForms!.Contains(board.Specs.FormFactor!)&&x.Specs.MaxGpuLengthMm>=gpu.Specs.GpuLengthMm&&(disk.Specs.DriveForm=="M2"||x.Specs.DriveBays!.GetValueOrDefault(disk.Specs.DriveForm!)>0));
   if(box==null)continue;
   var psu=products.FirstOrDefault(x=>x.Category==Category.PSU&&box.Specs.SupportedPsuForms!.Contains(x.Specs.FormFactor!)&&x.Specs.RatedPowerW>=Math.Ceiling((power+box.Specs.EstimatedPowerW!.Value)*1.25m)&&gpu.Specs.RequiredPowerConnectors!.All(k=>x.Specs.PowerConnectors!.GetValueOrDefault(k.Key)>=k.Value));
   if(psu==null)continue;
   var selected=baseParts.Concat([psu,box]).OrderBy(x=>x.Category).ToList();if(selected.Sum(x=>x.PriceRub)>budget)continue;
   if(!tried.Add(string.Join("/",selected.Select(x=>x.Id))))continue;
   if(++checks>24)return result;
   var items=selected.Select(x=>new Selection(x.Id,x.Category,1)).ToList();
   var check=await assembly.Check(new(items,selected),ct);if(!check.Compatible)continue;
   var quote=await commerce.Quote(items,"service",ct);
   if(quote.Available&&quote.TotalRub<=budget)result.Add(new(items,selected,quote,check));
   if(result.Count==3)return result;
  }
  return result;
 }
}

