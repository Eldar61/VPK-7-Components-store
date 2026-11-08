namespace PcShop.Domain;
public record SaleFact(Guid OrderId,Guid ProductId,string Name,Category Category,int Quantity,decimal TotalRub,DateTimeOffset ConfirmedAt);
public record Metrics(int Orders,int Units,decimal RevenueRub,decimal AverageCheckRub);
public record TimelinePoint(DateTimeOffset From,DateTimeOffset To,string Label,int Orders,int Units,decimal RevenueRub,decimal AverageCheckRub);
public record TopProduct(Guid ProductId,string Name,int Units,decimal RevenueRub);
public record CategorySales(Category Category,int Units,decimal RevenueRub);
public record Dynamics(decimal? OrdersPercent,decimal? UnitsPercent,decimal? RevenuePercent,decimal? AverageCheckPercent);
public record AnalyticsResult(DateTimeOffset From,DateTimeOffset To,string Group,Category? Category,string TimeZone,Metrics Summary,Metrics Previous,Dynamics Change,List<TimelinePoint> Timeline,List<TopProduct> TopProducts,List<CategorySales> Categories);
public static class AnalyticsCalculator
{
    public static readonly TimeZoneInfo Moscow=TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
    public static Metrics Summarize(IEnumerable<SaleFact> facts)
    {
        var rows=facts.ToList(); var orders=rows.Select(x=>x.OrderId).Distinct().Count(); var revenue=rows.Sum(x=>x.TotalRub);
        return new(orders,rows.Sum(x=>x.Quantity),revenue,orders==0?0:Math.Round(revenue/orders,2,MidpointRounding.AwayFromZero));
    }
    public static decimal? Change(decimal current,decimal previous)=>previous==0?null:Math.Round((current-previous)/previous*100,2,MidpointRounding.AwayFromZero);
    public static AnalyticsResult Calculate(IEnumerable<SaleFact> input,DateTimeOffset from,DateTimeOffset to,string group,Category? category)
    {
        from=from.ToUniversalTime();to=to.ToUniversalTime();
        if(to<=from||to-from>TimeSpan.FromDays(730)||group is not ("day" or "week" or "month")||category.HasValue&&!Enum.IsDefined(category.Value))
            throw new ShopException(400,"ANALYTICS_RANGE","Период должен быть положительным, до 730 дней; группировка day/week/month.");
        var source=input.Where(x=>category==null||x.Category==category).ToList();
        var current=source.Where(x=>x.ConfirmedAt>=from&&x.ConfirmedAt<to).ToList();
        var previous=Summarize(source.Where(x=>x.ConfirmedAt>=from-(to-from)&&x.ConfirmedAt<from)); var summary=Summarize(current);
        DateTime Floor(DateTime d)=>group=="month"?new(d.Year,d.Month,1):group=="week"?d.Date.AddDays(-((int)d.DayOfWeek+6)%7):d.Date;
        DateTime Next(DateTime d)=>group=="month"?d.AddMonths(1):d.AddDays(group=="week"?7:1);
        DateTimeOffset Utc(DateTime d)=>new(TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(d,DateTimeKind.Unspecified),Moscow));
        var timeline=new List<TimelinePoint>();
        for(var cursor=Floor(TimeZoneInfo.ConvertTime(from,Moscow).DateTime);Utc(cursor)<to;cursor=Next(cursor))
        {
            var start=Utc(cursor)>from?Utc(cursor):from;var end=Utc(Next(cursor))<to?Utc(Next(cursor)):to;
            var m=Summarize(current.Where(x=>x.ConfirmedAt>=start&&x.ConfirmedAt<end));
            timeline.Add(new(start,end,cursor.ToString(group=="month"?"yyyy-MM":"yyyy-MM-dd"),m.Orders,m.Units,m.RevenueRub,m.AverageCheckRub));
        }
        var top=current.GroupBy(x=>x.ProductId).Select(g=>new TopProduct(g.Key,g.OrderByDescending(x=>x.ConfirmedAt).First().Name,g.Sum(x=>x.Quantity),g.Sum(x=>x.TotalRub))).OrderByDescending(x=>x.Units).ThenByDescending(x=>x.RevenueRub).ThenBy(x=>x.ProductId).Take(5).ToList();
        var categories=Enum.GetValues<Category>().Select(c=>new CategorySales(c,current.Where(x=>x.Category==c).Sum(x=>x.Quantity),current.Where(x=>x.Category==c).Sum(x=>x.TotalRub))).ToList();
        return new(from,to,group,category,"Europe/Moscow",summary,previous,new(Change(summary.Orders,previous.Orders),Change(summary.Units,previous.Units),Change(summary.RevenueRub,previous.RevenueRub),Change(summary.AverageCheckRub,previous.AverageCheckRub)),timeline,top,categories);
    }
}

