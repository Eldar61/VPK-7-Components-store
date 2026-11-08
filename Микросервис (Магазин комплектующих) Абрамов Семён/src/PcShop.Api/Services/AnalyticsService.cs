using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
using PcShop.Domain;
namespace PcShop.Api.Services;
public sealed class AnalyticsService(ShopDb db)
{
 public async Task<AnalyticsResult> Get(DateTimeOffset from,DateTimeOffset to,string group,Category? category,CancellationToken ct)
 {
  // Validate before executing a potentially large query.
  AnalyticsCalculator.Calculate([],from,to,group,category);var previousFrom=from-(to-from);
  var rows=await db.Sales.AsNoTracking().Where(x=>x.ConfirmedAt>=previousFrom.ToUniversalTime()&&x.ConfirmedAt<to.ToUniversalTime()&&(!category.HasValue||x.Category==category)).ToListAsync(ct);
  return AnalyticsCalculator.Calculate(rows.Select(x=>new SaleFact(x.OrderId,x.ProductId,x.Name,x.Category,x.Quantity,x.TotalRub,x.ConfirmedAt)),from,to,group,category);
 }
}

