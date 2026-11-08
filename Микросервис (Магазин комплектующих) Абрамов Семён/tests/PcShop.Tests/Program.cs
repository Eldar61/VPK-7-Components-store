using PcShop.Domain;
int passed=0,failed=0;
void Test(string name,Action body){try{body();passed++;Console.WriteLine("PASS "+name);}catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Invalid(Action body){try{body();throw new Exception("Expected validation error");}catch(ShopException e){Equal(400,e.Status);}}
Test("STANDARD default",()=>Equal(0m,Pricing.Discount(CustomerType.STANDARD,1,0)));
Test("Quantity boundary 2",()=>Equal(3m,Pricing.Discount(CustomerType.STANDARD,2,0)));
Test("Quantity boundary 4",()=>Equal(3m,Pricing.Discount(CustomerType.STANDARD,4,0)));
Test("Quantity boundary 5",()=>Equal(7m,Pricing.Discount(CustomerType.STANDARD,5,0)));
Test("Regular beats quantity",()=>Equal(5m,Pricing.Discount(CustomerType.REGULAR,2,0)));
Test("Wholesale beats quantity",()=>Equal(10m,Pricing.Discount(CustomerType.WHOLESALE,5,0)));
Test("Promotion beats all, not stacked",()=>Equal(20m,Pricing.Discount(CustomerType.WHOLESALE,5,20)));
Test("Rounding half away from zero",()=>Equal(0.01m,Pricing.Total(0.01m,1,50)));
Test("Round entire line",()=>Equal(0.02m,Pricing.Total(0.01m,3,50)));
var a=Guid.NewGuid();var b=Guid.NewGuid();var order1=Guid.NewGuid();var order2=Guid.NewGuid();
var from=DateTimeOffset.Parse("2026-11-01T00:00:00+03:00");var to=from.AddDays(3);
List<SaleFact> facts=[new(order1,a,"CPU",Category.CPU,2,2000,from),new(order1,b,"RAM",Category.RAM,1,500,from),new(order2,b,"RAM",Category.RAM,3,1500,from.AddDays(2)),new(Guid.NewGuid(),a,"Excluded",Category.CPU,100,99999,to),new(Guid.NewGuid(),a,"Previous",Category.CPU,1,1000,from.AddDays(-1))];
var stats=AnalyticsCalculator.Calculate(facts,from,to,"day",null);
Test("Distinct orders, not sale lines",()=>Equal(2,stats.Summary.Orders));
Test("Units",()=>Equal(6,stats.Summary.Units));
Test("Revenue",()=>Equal(4000m,stats.Summary.RevenueRub));
Test("Average check",()=>Equal(2000m,stats.Summary.AverageCheckRub));
Test("Half open interval excludes to",()=>Equal(2,stats.Summary.Orders));
Test("Zero filled day",()=>Equal(0,stats.Timeline[1].Units));
Test("Moscow day start",()=>Equal(from.ToUniversalTime(),stats.Timeline[0].From));
Test("Previous same duration",()=>Equal(1000m,stats.Previous.RevenueRub));
Test("Revenue dynamics",()=>Equal<decimal?>(300m,stats.Change.RevenuePercent));
Test("Zero comparison has no basis",()=>Equal<decimal?>(null,AnalyticsCalculator.Change(10,0)));
Test("Category count distinct",()=>Equal(2,AnalyticsCalculator.Calculate(facts,from,to,"day",Category.RAM).Summary.Orders));
Test("Category average only selected lines",()=>Equal(1000m,AnalyticsCalculator.Calculate(facts,from,to,"day",Category.RAM).Summary.AverageCheckRub));
Test("Popularity by units",()=>Equal(b,stats.TopProducts[0].ProductId));
Test("Empty series, zero metrics",()=>Equal(0m,AnalyticsCalculator.Calculate([],from,to,"day",null).Summary.RevenueRub));
Test("Empty series fills all days",()=>Equal(3,AnalyticsCalculator.Calculate([],from,to,"day",null).Timeline.Count));
Test("Week Monday boundary clipped",()=>Equal(from.ToUniversalTime(),AnalyticsCalculator.Calculate(facts,from,to,"week",null).Timeline[0].From));
Test("Month grouping",()=>Equal("2026-11",AnalyticsCalculator.Calculate(facts,from,to,"month",null).Timeline[0].Label));
Test("Reject empty range",()=>Invalid(()=>AnalyticsCalculator.Calculate([],from,from,"day",null)));
Test("Reject unknown grouping",()=>Invalid(()=>AnalyticsCalculator.Calculate([],from,to,"year",null)));
var q1=new Quote(Guid.NewGuid(),to,[new(a,1,100,0,100,true),new(b,1,200,0,200,true)],300,true);
Test("Compare price ignores order and available flag",()=>Equal(true,Pricing.SamePrice(q1,q1 with{Lines=[q1.Lines[1] with{Available=false},q1.Lines[0]]})));
Test("Same total but changed positions is price conflict",()=>Equal(false,Pricing.SamePrice(q1,q1 with{Lines=[q1.Lines[0] with{UnitPriceRub=110,TotalRub=110},q1.Lines[1] with{UnitPriceRub=190,TotalRub=190}]})));
Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;

