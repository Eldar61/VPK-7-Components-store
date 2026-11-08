using PcAssembly.Domain;
namespace ShopStub;
public static class DemoCatalog
{
    public static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:000000000000}");
    public static List<Product> Products() =>
    [
        new(Id(1), "CPU A6 · AM5", Category.CPU, true, 18000, 20, new() { Socket="AM5", EstimatedPowerW=90 }),
        new(Id(2), "B650 · ATX · DDR5", Category.MOTHERBOARD, true, 15000, 20, new() { Socket="AM5", MemoryType="DDR5", MemorySlots=4, MaxMemoryGb=128, PcieX16Slots=1, FormFactor="ATX", SataPorts=4, M2Slots=2, EstimatedPowerW=35 }),
        new(Id(3), "DDR5 · 16 ГБ", Category.RAM, true, 5000, 40, new() { MemoryType="DDR5", ModuleCapacityGb=16, EstimatedPowerW=5 }),
        new(Id(4), "GPU G8 · 8 ГБ", Category.GPU, true, 35000, 10, new() { GpuLengthMm=260, RequiredPowerConnectors=new() { ["PCIE_8PIN"]=1 }, EstimatedPowerW=160 }),
        new(Id(5), "БП 650 Вт · ATX", Category.PSU, true, 7000, 20, new() { RatedPowerW=650, FormFactor="ATX", PowerConnectors=new() { ["PCIE_8PIN"]=2 } }),
        new(Id(6), "Корпус Air · ATX", Category.CASE, true, 6000, 20, new() { SupportedMotherboardForms=["ATX","MATX"], SupportedPsuForms=["ATX"], MaxGpuLengthMm=330, DriveBays=new() { ["2.5"]=2,["3.5"]=2 }, EstimatedPowerW=6 }),
        new(Id(7), "SSD M.2 · 1 ТБ", Category.STORAGE, true, 7000, 30, new() { StorageInterface="M2", DriveForm="M2", EstimatedPowerW=6 }),
        new(Id(8), "CPU B6 · LGA1700", Category.CPU, true, 16000, 20, new() { Socket="LGA1700", EstimatedPowerW=110 }),
        new(Id(9), "DDR4 · 16 ГБ", Category.RAM, true, 3500, 20, new() { MemoryType="DDR4", ModuleCapacityGb=16, EstimatedPowerW=5 }),
        new(Id(10), "GPU G6 · 6 ГБ", Category.GPU, true, 23000, 20, new() { GpuLengthMm=220, RequiredPowerConnectors=new() { ["PCIE_8PIN"]=1 }, EstimatedPowerW=100 }),
        new(Id(11), "БП 300 Вт · ATX", Category.PSU, true, 3000, 20, new() { RatedPowerW=300, FormFactor="ATX", PowerConnectors=new() { ["PCIE_8PIN"]=1 } }),
        new(Id(12), "SSD SATA · 500 ГБ", Category.STORAGE, true, 4000, 20, new() { StorageInterface="SATA", DriveForm="2.5", EstimatedPowerW=4 })
    ];
    public static List<Selection> Build() => Products().Take(7).Select(p => new Selection(p.Id,p.Category,p.Category==Category.RAM?2:1)).ToList();
}
