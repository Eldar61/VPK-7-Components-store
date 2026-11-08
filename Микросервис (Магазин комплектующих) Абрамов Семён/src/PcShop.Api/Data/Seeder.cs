using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PcShop.Api.Services;
namespace PcShop.Api.Data;
public static class Seeder
{
 public static async Task Run(ShopDb db,IConfiguration config,IHostEnvironment env,CancellationToken ct=default)
 {
  var tx=new Transactions(db);
  await tx.Run(async()=>{
   var file=Path.Combine(AppContext.BaseDirectory,"data","catalog.json");
   var input=Json.Read<List<CatalogSeed>>(await File.ReadAllTextAsync(file,ct));
   var existing=(await db.Products.Select(p=>p.Id).ToListAsync(ct)).ToHashSet();
   foreach(var row in input)
   {
    CatalogService.Validate(row.Input);
    if(existing.Contains(row.Id))continue;var x=row.Input;
    db.Products.Add(new(){Id=row.Id,Sku=x.Sku,Name=x.Name,Category=x.Category,Active=x.Active,PriceRub=x.PriceRub,StockQuantity=x.StockQuantity,SpecsJson=Json.Write(x.Specs),SourceUrl=x.SourceUrl,SourceNotes=x.SourceNotes,PowerBasis=x.PowerBasis});
   }
   var login=config["Bootstrap:AdminLogin"]??(env.IsDevelopment()?"admin":null);
   var password=config["Bootstrap:AdminPassword"]??(env.IsDevelopment()?"Shop-local-admin-2026!":null);
   if(login!=null&&!await db.Users.AnyAsync(u=>u.Role=="ADMIN",ct))
   {
    if(string.IsNullOrWhiteSpace(password)||password.Length<12||password.Length>200)throw new InvalidOperationException("Bootstrap__AdminPassword must contain 12–200 characters.");
    var user=new UserAccount{Login=login.Trim().ToLowerInvariant(),Role="ADMIN"};
    user.PasswordHash=new PasswordHasher<UserAccount>().HashPassword(user,password);db.Users.Add(user);
   }
   return true;
  },ct);
 }
 public record CatalogSeed(Guid Id,ProductInput Input);
}

