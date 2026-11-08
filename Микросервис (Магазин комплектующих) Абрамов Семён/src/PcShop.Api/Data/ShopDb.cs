using Microsoft.EntityFrameworkCore;
using PcShop.Domain;
namespace PcShop.Api.Data;
public sealed class ShopDb(DbContextOptions<ShopDb> options):DbContext(options)
{
 public DbSet<ProductEntity> Products=>Set<ProductEntity>();
 public DbSet<UserAccount> Users=>Set<UserAccount>();
 public DbSet<LoginSession> Sessions=>Set<LoginSession>();
 public DbSet<QuoteEntity> Quotes=>Set<QuoteEntity>();
 public DbSet<CartEntity> Carts=>Set<CartEntity>();
 public DbSet<OperationEntity> Operations=>Set<OperationEntity>();
 public DbSet<SaleLine> Sales=>Set<SaleLine>();
 protected override void OnModelCreating(ModelBuilder m)
 {
  m.Entity<ProductEntity>().HasIndex(p=>p.Sku).IsUnique();
  m.Entity<UserAccount>().HasIndex(p=>p.Login).IsUnique();
  m.Entity<LoginSession>().HasKey(p=>p.TokenHash);
  m.Entity<LoginSession>().HasOne<UserAccount>().WithMany().HasForeignKey(p=>p.UserId);
  m.Entity<OperationEntity>().HasKey(p=>p.Key);
  m.Entity<SaleLine>().HasIndex(p=>new{p.OrderId,p.ProductId}).IsUnique();
  m.Entity<SaleLine>().HasIndex(p=>p.ConfirmedAt);
  m.Entity<SaleLine>().HasOne<ProductEntity>().WithMany().HasForeignKey(p=>p.ProductId).OnDelete(DeleteBehavior.Restrict);
  m.Entity<CartEntity>().HasIndex(p=>new{p.Status,p.ExpiresAt});
  m.Entity<ProductEntity>().Property(p=>p.Version).IsConcurrencyToken();
  foreach(var entity in m.Model.GetEntityTypes()) foreach(var prop in entity.GetProperties().Where(p=>p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?))) {prop.SetPrecision(18);prop.SetScale(2);}
 }
}
public sealed class ProductEntity
{
 public Guid Id{get;set;} public string Sku{get;set;}=""; public string Name{get;set;}=""; public Category Category{get;set;}
 public bool Active{get;set;}=true;public decimal PriceRub{get;set;} public int StockQuantity{get;set;} public string SpecsJson{get;set;}="{}";
 public string SourceUrl{get;set;}="";public string SourceNotes{get;set;}=""; public string PowerBasis{get;set;}="";
 public decimal PromotionPercent{get;set;} public DateTimeOffset? PromotionFrom{get;set;} public DateTimeOffset? PromotionTo{get;set;} public int Version{get;set;}
}
public sealed class UserAccount {public Guid Id{get;set;}=Guid.NewGuid();public string Login{get;set;}="";public string PasswordHash{get;set;}="";public string Role{get;set;}="CUSTOMER";public CustomerType CustomerType{get;set;}}
public sealed class LoginSession {public string TokenHash{get;set;}="";public Guid UserId{get;set;}public DateTimeOffset ExpiresAt{get;set;}}
public sealed class QuoteEntity {public Guid Id{get;set;} public string Owner{get;set;}="";public Guid? CartId{get;set;}public string ItemsJson{get;set;}="[]";public string QuoteJson{get;set;}="";public DateTimeOffset ExpiresAt{get;set;}}
public sealed class CartEntity
{
 public Guid Id{get;set;}=Guid.NewGuid();public Guid? ExternalConfigurationId{get;set;}public string Owner{get;set;}="";
 public string Status{get;set;}="DRAFT";public string ItemsJson{get;set;}="[]"; public string? QuoteJson{get;set;}
 public string? ReservedProductsJson{get;set;} public Guid? ReservationId{get;set;}public DateTimeOffset? ExpiresAt{get;set;}
 public DateTimeOffset CreatedAt{get;set;}=DateTimeOffset.UtcNow;public DateTimeOffset? ConfirmedAt{get;set;}
}
public sealed class OperationEntity {public string Key{get;set;}="";public string RequestHash{get;set;}="";public string ResponseJson{get;set;}="";public DateTimeOffset CreatedAt{get;set;}=DateTimeOffset.UtcNow;}
public sealed class SaleLine
{
 public Guid Id{get;set;}=Guid.NewGuid();public Guid OrderId{get;set;}public Guid ProductId{get;set;} public string Name{get;set;}="";
 public Category Category{get;set;}public int Quantity{get;set;}public decimal UnitPriceRub{get;set;}public decimal DiscountPercent{get;set;}
 public decimal UnitPriceAfterDiscountRub{get;set;} public decimal TotalRub{get;set;}public DateTimeOffset ConfirmedAt{get;set;}
}

