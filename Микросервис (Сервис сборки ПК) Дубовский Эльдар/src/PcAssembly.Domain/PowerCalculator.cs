namespace PcAssembly.Domain;

public sealed class PowerCalculator
{
    public const decimal PsuReserveFactor = 1.25m;

    // CompatibilityChecker validates quantities and power specifications before this call.
    public PowerResult Calculate(IEnumerable<(Product Product, int Quantity)> components)
    {
        var consumers = components.Where(x => x.Product.Category != Category.PSU)
            .Select(x => new { x.Product, Watts = x.Quantity * x.Product.Specs.EstimatedPowerW!.Value }).ToList();
        var total = consumers.Sum(x => x.Watts);
        return new(total, (int)Math.Ceiling(total * PsuReserveFactor),
            consumers.Where(x => x.Watts > 0).Select(x => new PowerSlice(x.Product.Id, x.Product.Name, x.Watts,
                total == 0 ? 0 : Math.Round(x.Watts / total * 100, 2))).ToList());
    }
}
