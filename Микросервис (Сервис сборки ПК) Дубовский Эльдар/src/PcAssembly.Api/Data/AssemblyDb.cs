using Microsoft.EntityFrameworkCore;
namespace PcAssembly.Api.Data;

public sealed class AssemblyDb(DbContextOptions<AssemblyDb> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<LoginSession> Sessions => Set<LoginSession>();
    public DbSet<PCConfiguration> Configurations => Set<PCConfiguration>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<UserAccount>().HasIndex(x => x.Login).IsUnique();
        model.Entity<UserAccount>().Property(x => x.Login).HasMaxLength(80);
        model.Entity<LoginSession>().HasKey(x => x.TokenHash);
        model.Entity<LoginSession>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<PCConfiguration>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<PCConfiguration>().Property(x => x.Version).IsConcurrencyToken();
        model.Entity<PCConfiguration>().Property(x => x.BudgetRub).HasPrecision(14, 2);
        model.Entity<PCConfiguration>().Property(x => x.SavedPriceRub).HasPrecision(14, 2);
        model.Entity<PCConfiguration>().HasIndex(x => x.SavedAt);
    }
}
public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Login { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}
public sealed class LoginSession
{
    public string TokenHash { get; set; } = "";
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
public sealed class PCConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Name { get; set; } = "Новая сборка";
    public decimal BudgetRub { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public string Status { get; set; } = "DRAFT";
    public string? EvaluationJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SavedAt { get; set; }
    public decimal? SavedPriceRub { get; set; }
    public string? SavedProductsJson { get; set; }
    public int Version { get; set; }
    // Correlation only: cart state and stock remain owned by the shop.
    public Guid? CartId { get; set; }
    public string CartOperationKey { get; set; } = Guid.NewGuid().ToString();
}
