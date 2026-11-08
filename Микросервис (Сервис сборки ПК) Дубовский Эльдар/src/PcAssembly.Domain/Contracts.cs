namespace PcAssembly.Domain;

public enum Category { CPU, MOTHERBOARD, RAM, GPU, PSU, CASE, STORAGE }
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
    public Dictionary<string, int>? RequiredPowerConnectors { get; init; }
    public Dictionary<string, int>? PowerConnectors { get; init; }
    public int? RatedPowerW { get; init; }
    public decimal? EstimatedPowerW { get; init; }
    public string? StorageInterface { get; init; }
    public string? DriveForm { get; init; }
    public int? SataPorts { get; init; }
    public int? M2Slots { get; init; }
    public Dictionary<string, int>? DriveBays { get; init; }
}
public record Product(Guid Id, string Name, Category Category, bool Active, decimal PriceRub, int AvailableQuantity, Specifications Specs);
public record CheckRequest(List<Selection> Items, List<Product> Products);
public record Issue(string Code, string Message, Guid[] ProductIds);
public record PowerSlice(Guid ProductId, string Name, decimal Watts, decimal Percent);
public record PowerResult(decimal TotalPowerW, int RequiredPsuW, List<PowerSlice> Slices);
public record CompatibilityResult(bool Compatible, List<Issue> Errors, List<Issue> Warnings, PowerResult? Power);
public record QuoteLine(Guid ProductId, int Quantity, decimal UnitPriceRub, decimal DiscountPercent, decimal TotalRub, bool Available);
public record Quote(Guid Id, DateTimeOffset ExpiresAt, List<QuoteLine> Lines, decimal TotalRub, bool Available);
public record QuoteRequest(List<Selection> Items);
public record Evaluation(CompatibilityResult Technical, Quote? Quote, bool? Available, bool? WithinBudget, string? ExternalError, DateTimeOffset CheckedAt);
public record CartRequest(Guid ExternalConfigurationId, List<Selection> Items, string OperationKey);
public record ShopCart(Guid Id, string Status, Quote? Quote, Guid? ReservationId, DateTimeOffset? ExpiresAt);
public record CartAction(Guid QuoteId, string OperationKey);
public record PriceBucket(decimal FromRub, decimal ToRub, int Count);

public static class PriceStatistics
{
    public static List<PriceBucket> Calculate(IEnumerable<decimal> prices)
    {
        var groups = prices.Where(p => p >= 0).GroupBy(p => decimal.Floor(p / 50000m)).ToDictionary(g => g.Key, g => g.Count());
        if (groups.Count == 0) return [];
        var result = new List<PriceBucket>();
        for (decimal i = 0; i <= groups.Keys.Max(); i++) result.Add(new(i * 50000m, (i + 1) * 50000m, groups.GetValueOrDefault(i)));
        return result;
    }
}
