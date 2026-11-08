namespace PcAssembly.Domain;

// This service is deliberately pure: it never fetches the shop or writes the database.
public sealed class CompatibilityChecker
{
    private readonly PowerCalculator powerCalculator = new();
    public CompatibilityResult Check(CheckRequest request)
    {
        var errors = new List<Issue>();
        void Error(string code, string message, params Guid[] ids) => errors.Add(new(code, message, ids));
        var items = request.Items ?? [];
        var products = request.Products ?? [];
        if (items.Any(i => i is null) || products.Any(p => p is null)) return new(false, [new("INPUT_INVALID", "Позиции и товары не могут содержать null.", [])], [], null);
        if (items.Count > 100 || products.Count > 100) return new(false, [new("TOO_MANY_ITEMS", "Допустимо не более 100 позиций.", [])], [], null);
        if (products.GroupBy(p => p.Id).Any(g => g.Count() > 1)) return new(false, [new("DUPLICATE_PRODUCT", "Каталог содержит повторяющиеся ID.", [])], [], null);
        var catalog = products.ToDictionary(p => p.Id);
        var selected = new List<(Selection Item, Product Product)>();
        foreach (var item in items)
        {
            if (!Enum.IsDefined(item.Category)) Error("INVALID_CATEGORY", "Неизвестная категория.", item.ProductId);
            if (item.Quantity is < 1 or > 100) Error("INVALID_QUANTITY", "Количество должно быть от 1 до 100.", item.ProductId);
            if (!catalog.TryGetValue(item.ProductId, out var p)) { Error("PRODUCT_NOT_FOUND", "Комплектующее не найдено.", item.ProductId); continue; }
            if (!p.Active) Error("PRODUCT_INACTIVE", $"Товар «{p.Name}» архивирован.", p.Id);
            if (p.Category != item.Category) Error("CATEGORY_MISMATCH", $"Категория «{p.Name}» не совпадает с выбранной.", p.Id);
            if (p.Specs is null) { Error("SPEC_MISSING", $"Для «{p.Name}» отсутствуют характеристики.", p.Id); continue; }
            selected.Add((item, p));
        }
        foreach (var g in items.GroupBy(i => i.ProductId).Where(g => g.Count() > 1)) Error("DUPLICATE_ITEM", "Одинаковые позиции нужно объединить.", g.Key);
        foreach (var category in Enum.GetValues<Category>())
        {
            var group = items.Where(i => i.Category == category).ToList();
            if (group.Count == 0) Error("CATEGORY_REQUIRED", $"Не выбрана обязательная категория {category}.");
            else if (category is not (Category.RAM or Category.STORAGE) && (group.Count != 1 || group[0].Quantity != 1))
                Error("SINGLE_COMPONENT_REQUIRED", $"В категории {category} нужна ровно одна деталь.", group.Select(i => i.ProductId).ToArray());
        }
        bool Need(Product p, bool condition, string field)
        {
            if (!condition) Error("SPEC_MISSING", $"У «{p.Name}» отсутствует или некорректна характеристика {field}.", p.Id);
            return condition;
        }
        foreach (var (_, p) in selected)
        {
            var s = p.Specs;
            switch (p.Category)
            {
                case Category.CPU: Need(p, !string.IsNullOrWhiteSpace(s.Socket), "socket"); break;
                case Category.MOTHERBOARD:
                    Need(p, !string.IsNullOrWhiteSpace(s.Socket), "socket"); Need(p, !string.IsNullOrWhiteSpace(s.MemoryType), "memoryType");
                    Need(p, s.MemorySlots > 0, "memorySlots"); Need(p, s.MaxMemoryGb > 0, "maxMemoryGb"); Need(p, s.PcieX16Slots >= 0, "pcieX16Slots");
                    Need(p, !string.IsNullOrWhiteSpace(s.FormFactor), "formFactor"); Need(p, s.SataPorts >= 0, "sataPorts"); Need(p, s.M2Slots >= 0, "m2Slots"); break;
                case Category.RAM: Need(p, !string.IsNullOrWhiteSpace(s.MemoryType), "memoryType"); Need(p, s.ModuleCapacityGb > 0, "moduleCapacityGb"); break;
                case Category.GPU: Need(p, s.GpuLengthMm > 0, "gpuLengthMm"); Need(p, ValidConnectors(s.RequiredPowerConnectors), "requiredPowerConnectors"); break;
                case Category.PSU: Need(p, s.RatedPowerW > 0, "ratedPowerW"); Need(p, !string.IsNullOrWhiteSpace(s.FormFactor), "formFactor"); Need(p, ValidConnectors(s.PowerConnectors), "powerConnectors"); break;
                case Category.CASE:
                    Need(p, s.SupportedMotherboardForms is { Length: > 0 } && s.SupportedMotherboardForms.All(f => !string.IsNullOrWhiteSpace(f)), "supportedMotherboardForms"); Need(p, s.SupportedPsuForms is { Length: > 0 } && s.SupportedPsuForms.All(f => !string.IsNullOrWhiteSpace(f)), "supportedPsuForms");
                    Need(p, s.MaxGpuLengthMm > 0, "maxGpuLengthMm"); Need(p, ValidConnectors(s.DriveBays), "driveBays"); break;
                case Category.STORAGE:
                    Need(p, s.StorageInterface is "SATA" or "M2", "storageInterface (SATA/M2)");
                    Need(p, s.DriveForm is "2.5" or "3.5" or "M2", "driveForm (2.5/3.5/M2)");
                    if (s.StorageInterface is "SATA" && s.DriveForm is "M2" || s.StorageInterface is "M2" && s.DriveForm is not "M2") Error("STORAGE_SPEC_INVALID", "Интерфейс накопителя не согласован с форм-фактором.", p.Id);
                    break;
            }
            if (p.Category != Category.PSU) Need(p, s.EstimatedPowerW.HasValue && (p.Category == Category.CASE ? s.EstimatedPowerW >= 0 : s.EstimatedPowerW > 0) && s.EstimatedPowerW <= 10000, "estimatedPowerW");
        }
        Product? One(Category c) => selected.Count(x => x.Product.Category == c) == 1 ? selected.First(x => x.Product.Category == c).Product : null;
        var cpu = One(Category.CPU); var board = One(Category.MOTHERBOARD); var gpu = One(Category.GPU); var psu = One(Category.PSU); var box = One(Category.CASE);
        if (cpu?.Specs.Socket is { } socket && board?.Specs.Socket is { } boardSocket && !Equal(socket, boardSocket)) Error("SOCKET_MISMATCH", "Сокеты CPU и платы не совпадают.", cpu.Id, board.Id);
        if (board is not null)
        {
            var ram = selected.Where(x => x.Product.Category == Category.RAM).ToList();
            foreach (var (_, p) in ram) if (p.Specs.MemoryType is { } type && board.Specs.MemoryType is { } supported && !Equal(type, supported)) Error("RAM_TYPE_MISMATCH", "Тип RAM не поддерживается платой.", p.Id, board.Id);
            if (board.Specs.MemorySlots.HasValue && ram.Sum(x => (long)x.Item.Quantity) > board.Specs.MemorySlots) Error("RAM_SLOTS_EXCEEDED", "Не хватает слотов RAM.", board.Id);
            if (ram.All(x => x.Product.Specs.ModuleCapacityGb.HasValue) && board.Specs.MaxMemoryGb.HasValue && ram.Sum(x => (long)x.Item.Quantity * x.Product.Specs.ModuleCapacityGb!.Value) > board.Specs.MaxMemoryGb) Error("RAM_CAPACITY_EXCEEDED", "Превышен максимальный объём RAM.", board.Id);
            if (gpu is not null && board.Specs.PcieX16Slots == 0) Error("GPU_SLOT_MISSING", "На плате нет слота PCIe x16.", gpu.Id, board.Id);
            foreach (var type in new[] { "SATA", "M2" })
            {
                var count = selected.Where(x => x.Product.Category == Category.STORAGE && x.Product.Specs.StorageInterface == type).Sum(x => (long)x.Item.Quantity);
                var capacity = type == "SATA" ? board.Specs.SataPorts : board.Specs.M2Slots;
                if (capacity.HasValue && count > capacity) Error("STORAGE_PORTS_EXCEEDED", $"Не хватает портов {type}.", board.Id);
            }
        }
        if (box is not null)
        {
            if (board?.Specs.FormFactor is { } form && box.Specs.SupportedMotherboardForms is { } forms && !forms.Any(f => !string.IsNullOrWhiteSpace(f) && Equal(f, form))) Error("BOARD_CASE_MISMATCH", "Корпус не поддерживает форм-фактор платы.", board.Id, box.Id);
            if (psu?.Specs.FormFactor is { } psuForm && box.Specs.SupportedPsuForms is { } psuForms && !psuForms.Any(f => !string.IsNullOrWhiteSpace(f) && Equal(f, psuForm))) Error("PSU_CASE_MISMATCH", "Корпус не поддерживает форм-фактор БП.", psu.Id, box.Id);
            if (gpu?.Specs.GpuLengthMm > box.Specs.MaxGpuLengthMm) Error("GPU_TOO_LONG", "Видеокарта не помещается в корпус.", gpu.Id, box.Id);
            foreach (var driveForm in new[] { "2.5", "3.5" })
            {
                var count = selected.Where(x => x.Product.Category == Category.STORAGE && x.Product.Specs.DriveForm == driveForm).Sum(x => (long)x.Item.Quantity);
                if (box.Specs.DriveBays is { } bays && count > bays.GetValueOrDefault(driveForm)) Error("DRIVE_BAYS_EXCEEDED", $"Не хватает мест для накопителей {driveForm}.", box.Id);
            }
        }
        if (gpu?.Specs.RequiredPowerConnectors is { } required && psu?.Specs.PowerConnectors is { } provided)
            foreach (var (connector, count) in required) if (count > provided.GetValueOrDefault(connector)) Error("GPU_POWER_CONNECTORS_MISSING", $"БП не обеспечивает {count} разъёмов {connector}.", gpu.Id, psu.Id);
        PowerResult? power = null;
        if (!errors.Any(e => e.Code is "SPEC_MISSING" or "PRODUCT_NOT_FOUND" or "CATEGORY_REQUIRED" or "INVALID_QUANTITY" or "DUPLICATE_ITEM" or "SINGLE_COMPONENT_REQUIRED" or "CATEGORY_MISMATCH"))
        {
            power = powerCalculator.Calculate(selected.Select(x => (x.Product, x.Item.Quantity)));
            if (psu?.Specs.RatedPowerW < power.RequiredPsuW) Error("PSU_INSUFFICIENT", $"Нужен БП не менее {power.RequiredPsuW} Вт (запас 25%).", psu.Id);
        }
        return new(errors.Count == 0, errors, [], power);
    }
    private static bool Equal(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    private static bool ValidConnectors(Dictionary<string, int>? value) => value is not null && value.All(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value >= 0);
}
