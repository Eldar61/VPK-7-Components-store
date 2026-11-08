using Microsoft.EntityFrameworkCore;
using PcShop.Api.Data;
namespace PcShop.Api.Services;
public sealed class Transactions(ShopDb db)
{
 // A database lock, not an in-process lock: independent API instances share it.
 public async Task<T> Run<T>(Func<Task<T>> operation,CancellationToken ct)
 {
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(70971108)",ct);
  var result=await operation();await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;
 }
}

