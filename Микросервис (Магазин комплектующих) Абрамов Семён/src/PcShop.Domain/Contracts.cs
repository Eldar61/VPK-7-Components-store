namespace PcShop.Domain;
public enum Category { CPU, MOTHERBOARD, RAM, GPU, PSU, CASE, STORAGE }
public enum CustomerType { STANDARD, REGULAR, WHOLESALE }
public record Selection(Guid ProductId, Category Category, int Quantity);
public record Specifications
{
    public string? Socket { get; init; }
    public string? MemoryType { get; init; }
    public int? MemorySlots { get; init; }
    public int? MaxMemoryGb { get; init; }
    public int? ModuleCapacityGb { get; init; }
    public int? PcieX16Slots { get; init; }
    public string? FormFactor { get; init; }
    public string[]? SupportedMotherboardForms { get; init; }
    public string[]? SupportedPsuForms { get; init; }
    public int? GpuLengthMm { get; init; }
    public int? MaxGpuLengthMm { get; init; }
    public Dictionary<string,int>? RequiredPowerConnectors { get; init; }
    public Dictionary<string,int>? PowerConnectors { get; init; }
    public int? RatedPowerW { get; init; }
    public decimal? EstimatedPowerW { get; init; }
    public string? StorageInterface { get; init; }
    public string? DriveForm { get; init; }
    public int? SataPorts { get; init; }
    public int? M2Slots { get; init; }
    public Dictionary<string,int>? DriveBays { get; init; }
}
public record Product(Guid Id,string Name,Category Category,bool Active,decimal PriceRub,int AvailableQuantity,Specifications Specs);
public record QuoteLine(Guid ProductId,int Quantity,decimal UnitPriceRub,decimal DiscountPercent,decimal TotalRub,bool Available);
public record Quote(Guid Id,DateTimeOffset ExpiresAt,List<QuoteLine> Lines,decimal TotalRub,bool Available);
public record QuoteRequest(List<Selection> Items);
public record CartRequest(Guid ExternalConfigurationId,List<Selection> Items,string OperationKey);
public record CustomerCartRequest(List<Selection> Items,string OperationKey);
public record CartAction(Guid QuoteId,string OperationKey);
public record ShopCart(Guid Id,string Status,Quote? Quote,Guid? ReservationId,DateTimeOffset? ExpiresAt);
public record Issue(string Code,string Message,Guid[] ProductIds);
public record PowerSlice(Guid ProductId,string Name,decimal Watts,decimal Percent);
public record PowerResult(decimal TotalPowerW,int RequiredPsuW,List<PowerSlice> Slices);
public record CompatibilityResult(bool Compatible,List<Issue> Errors,List<Issue> Warnings,PowerResult? Power);
public record CheckRequest(List<Selection> Items,List<Product> Products);
public record BuildRequest(decimal BudgetRub);
public record BuildSuggestion(List<Selection> Items,List<Product> Products,Quote Quote,CompatibilityResult Technical);
public static class Pricing
{
    public static decimal Discount(CustomerType type,int quantity,decimal promotion) =>
        Math.Max(promotion,Math.Max(type switch {CustomerType.REGULAR=>5m,CustomerType.WHOLESALE=>10m,_=>0m},quantity>=5?7m:quantity>=2?3m:0m));
    public static decimal Total(decimal price,int quantity,decimal discount) => Math.Round(price*quantity*(1-discount/100m),2,MidpointRounding.AwayFromZero);
    public static bool SamePrice(Quote a,Quote b) => a.Lines.Count==b.Lines.Count && a.Lines.OrderBy(x=>x.ProductId).Zip(b.Lines.OrderBy(x=>x.ProductId)).All(p=>p.First.ProductId==p.Second.ProductId&&p.First.Quantity==p.Second.Quantity&&p.First.UnitPriceRub==p.Second.UnitPriceRub&&p.First.DiscountPercent==p.Second.DiscountPercent&&p.First.TotalRub==p.Second.TotalRub);
}
public sealed class ShopException(int status,string code,string message) : Exception(message)
{
    public int Status {get;}=status; public string Code {get;}=code;
}

