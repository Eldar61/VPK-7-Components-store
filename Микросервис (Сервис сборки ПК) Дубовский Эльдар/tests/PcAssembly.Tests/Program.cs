using PcAssembly.Domain;
using ShopStub;
var checker = new CompatibilityChecker(); int passed=0, failed=0;
void Test(string name, Action test) { try { test(); passed++; Console.WriteLine($"PASS {name}"); } catch(Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e.Message}"); } }
void Assert(bool value,string message="Assertion failed") { if(!value) throw new Exception(message); }
void Rule(string name,string code,Action<List<Product>,List<Selection>> mutate) => Test(name,()=> {var ps=DemoCatalog.Products();var items=DemoCatalog.Build();mutate(ps,items);var result=checker.Check(new(items,ps));Assert(!result.Compatible && result.Errors.Any(e=>e.Code==code), $"Expected {code}, got {string.Join(',',result.Errors.Select(e=>e.Code))}");});
void Spec(List<Product> ps,int n,Func<Specifications,Specifications> change) {var i=ps.FindIndex(p=>p.Id==DemoCatalog.Id(n));ps[i]=ps[i] with { Specs=change(ps[i].Specs) };}
Test("Complete build + power sum excludes PSU",()=>{var r=checker.Check(new(DemoCatalog.Build(),DemoCatalog.Products()));Assert(r.Compatible);Assert(r.Power?.TotalPowerW==307);Assert(r.Power!.RequiredPsuW==384);Assert(r.Power.Slices.Sum(s=>s.Watts)==307);Assert(r.Power.Slices.All(s=>s.ProductId!=DemoCatalog.Id(5)));});
Rule("Missing product","PRODUCT_NOT_FOUND",(p,i)=>p.RemoveAt(0));
Rule("Archived product","PRODUCT_INACTIVE",(p,i)=>p[0]=p[0] with { Active=false });
Rule("Category mismatch","CATEGORY_MISMATCH",(p,i)=>i[0]=i[0] with { Category=Category.GPU });
Rule("CPU required","CATEGORY_REQUIRED",(p,i)=>i.RemoveAt(0));
Rule("Zero quantity","INVALID_QUANTITY",(p,i)=>i[0]=i[0] with { Quantity=0 });
Rule("Single GPU","SINGLE_COMPONENT_REQUIRED",(p,i)=>i[3]=i[3] with { Quantity=2 });
Rule("Duplicate items","DUPLICATE_ITEM",(p,i)=>i.Add(i[0]));
Rule("Socket mismatch","SOCKET_MISMATCH",(p,i)=>i[0]=i[0] with { ProductId=DemoCatalog.Id(8) });
Rule("DDR mismatch","RAM_TYPE_MISMATCH",(p,i)=>i[2]=i[2] with { ProductId=DemoCatalog.Id(9) });
Rule("RAM slots","RAM_SLOTS_EXCEEDED",(p,i)=>i[2]=i[2] with { Quantity=5 });
Rule("RAM capacity","RAM_CAPACITY_EXCEEDED",(p,i)=>Spec(p,2,s=>s with { MaxMemoryGb=16 }));
Rule("GPU slot","GPU_SLOT_MISSING",(p,i)=>Spec(p,2,s=>s with { PcieX16Slots=0 }));
Rule("GPU dimensions","GPU_TOO_LONG",(p,i)=>Spec(p,6,s=>s with { MaxGpuLengthMm=200 }));
Rule("GPU connectors","GPU_POWER_CONNECTORS_MISSING",(p,i)=>Spec(p,5,s=>s with { PowerConnectors=[] }));
Rule("Board case form","BOARD_CASE_MISMATCH",(p,i)=>Spec(p,6,s=>s with { SupportedMotherboardForms=["ITX"] }));
Rule("PSU case form","PSU_CASE_MISMATCH",(p,i)=>Spec(p,6,s=>s with { SupportedPsuForms=["SFX"] }));
Rule("Storage ports","STORAGE_PORTS_EXCEEDED",(p,i)=>Spec(p,2,s=>s with { M2Slots=0 }));
Rule("Storage bays","DRIVE_BAYS_EXCEEDED",(p,i)=>{i[6]=i[6] with { ProductId=DemoCatalog.Id(12) };Spec(p,6,s=>s with { DriveBays=[] });});
Rule("Inconsistent storage specification","STORAGE_SPEC_INVALID",(p,i)=>Spec(p,7,s=>s with { DriveForm="2.5" }));
Rule("Missing power","SPEC_MISSING",(p,i)=>Spec(p,3,s=>s with { EstimatedPowerW=null }));
Rule("Missing connector specification","SPEC_MISSING",(p,i)=>Spec(p,4,s=>s with { RequiredPowerConnectors=null }));
Rule("PSU below 25% boundary","PSU_INSUFFICIENT",(p,i)=>Spec(p,5,s=>s with { RatedPowerW=383 }));
Test("PSU exact boundary accepted",()=>{var p=DemoCatalog.Products();Spec(p,5,s=>s with { RatedPowerW=384 });Assert(checker.Check(new(DemoCatalog.Build(),p)).Compatible);});
Test("Missing power prevents misleading pie",()=>{var p=DemoCatalog.Products();Spec(p,1,s=>s with { EstimatedPowerW=null });Assert(checker.Check(new(DemoCatalog.Build(),p)).Power is null);});
Test("Price histogram edges and empty bins",()=>{var b=PriceStatistics.Calculate([49999.99m,50000m,150000m]);Assert(b.Count==4);Assert(b[0].Count==1&&b[1].Count==1&&b[2].Count==0&&b[3].Count==1);});
Test("No saved prices yields no bars",()=>Assert(PriceStatistics.Calculate([]).Count==0));
Test("Null item is an input error",()=>Assert(checker.Check(new([null!],DemoCatalog.Products())).Errors.Any(e=>e.Code=="INPUT_INVALID")));
Rule("Empty case form entry","SPEC_MISSING",(p,i)=>Spec(p,6,s=>s with { SupportedMotherboardForms=[null!] }));
Console.WriteLine($"{passed} passed; {failed} failed.");return failed==0?0:1;
