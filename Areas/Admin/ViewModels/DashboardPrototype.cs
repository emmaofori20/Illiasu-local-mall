using System.Globalization;
namespace ILLiasu_Local_Mall.Areas.Admin.ViewModels;

// Presentation-only, deterministic sample data. No customer or merchant records.
public sealed record MetricCard(string Label, string Value, string Detail, string Icon, bool ShowTrend = true);
public sealed record PanelHeading(string Id, string Title, string Subtitle, string? Action = null);
public sealed record PipelineStage(string Label, int Count, string Tone);
public sealed record SampleOrder(string Reference, string Customer, string Method, string Total, string Payment, string Stage, string Tone);
public sealed record SampleSeller(string Name, string Unit, string Image, int Units);
public sealed record SampleStock(string Name, string Variant, int OnHand, int Reserved, int Minimum, decimal Cost) { public int Available => OnHand - Reserved; }
public sealed record SamplePurchase(string Reference, string Supplier, string Product, int Ordered, int Received, decimal Cost);
public sealed record SampleMovement(string Reference, string Variant, string Type, string Quantity, string Reason);
public sealed class DashboardPrototype
{
    public int Period { get; init; }
    public decimal Revenue { get; init; }
    public int Orders { get; init; }
    public int PaidOrders { get; init; }
    public decimal[] Sales { get; init; } = [];
    public MetricCard[] Metrics { get; init; } = [];
    public PipelineStage[] Pipeline { get; init; } = [];
    public SampleSeller[] Sellers { get; init; } = [];
    public SampleOrder[] RecentOrders { get; init; } = [];
    public SampleStock[] Stock { get; init; } = [];
    public SamplePurchase[] Purchases { get; init; } = [];
    public SampleMovement[] Movements { get; init; } = [];
    public MetricCard[] StockMetrics => [
        new("Stock on hand", Stock.Sum(s => s.OnHand).ToString(), $"{Stock.Sum(s => s.Reserved)} reserved / {Stock.Sum(s => s.Available)} available", "products", false),
        new("Low-stock products", Stock.Count(s => s.Available > 0 && s.Available < s.Minimum).ToString(), "Variant alerts / below minimum", "reports", false),
        new("Out-of-stock products", Stock.Count(s => s.Available == 0).ToString(), "Variants with zero available", "products", false),
        new("Inventory value", Money(Stock.Sum(s => s.OnHand * s.Cost)), "Demo cost x on hand / method pending", "revenue", false),
        new("Pending purchase orders", Purchases.Length.ToString(), "Issued or partially received", "orders", false)
    ];
    public string Money(decimal value) => "GH₵ " + value.ToString("N0", CultureInfo.InvariantCulture);
    public static DashboardPrototype Create(int requested)
    {
        var days = requested is 7 or 30 or 90 ? requested : 30;
        var scale = days == 7 ? 1 : days == 30 ? 4 : 12;
        var paid = days == 7 ? 70 : 300 * (days / 30);
        var revenue = paid * 200m;
        var weights = Enumerable.Range(0, days).Select(i => (decimal)(1.1 + .25 * Math.Sin(i * 1.7) + .2 * Math.Sin(i * .4) + i / (double)days * .4)).ToArray();
        var sum = weights.Sum();
        var sales = weights.Select(v => decimal.Round(v / sum * revenue, 2)).ToArray();
        sales[^1] += revenue - sales.Sum();
        return new DashboardPrototype {
            Period = days, Revenue = revenue, PaidOrders = paid, Orders = 84 * scale, Sales = sales,
            Metrics = [new("Revenue", "GH₵ " + revenue.ToString("N0", CultureInfo.InvariantCulture), $"{paid:N0} sample paid orders", "revenue"), new("Orders", (84 * scale).ToString("N0"), "All sample payment states", "orders"), new("Items sold", (paid * 3).ToString("N0"), "Units in sample paid orders", "products"), new("Average order value", "GH₵ 200", "Sample revenue / paid orders", "reports")],
            Stock = [new("Long-grain rice", "5 kg bag", 32, 4, 10, 70), new("Cooking oil", "1 litre bottle", 18, 3, 20, 25), new("White beans", "1 kg pack", 8, 0, 12, 22), new("Fresh eggs", "Tray of 30", 0, 0, 8, 45), new("Milk", "400 g tin", 0, 0, 10, 18), new("Noodles", "Single pack", 42, 6, 15, 16)],
            Purchases = [new("DEMO-PO-104", "Demo supplier 01", "Rice / 5 kg bag", 50, 20, 70), new("DEMO-PO-103", "Demo supplier 02", "Oil / 1 litre bottle", 40, 0, 25), new("DEMO-PO-102", "Demo supplier 03", "Milk / 400 g tin", 24, 12, 18)],
            Movements = [new("DEMO-RCV-12", "Milk / 400 g tin", "Received", "+12", "Accepted partial delivery"), new("DEMO-SALE-08", "Rice / 5 kg bag", "Sold", "-3", "Verified payment example"), new("DEMO-RET-03", "Oil / 1 litre bottle", "Returned", "+1", "Inspected sellable return"), new("DEMO-DMG-02", "Beans / 1 kg pack", "Damaged", "-2", "Disposed damaged packs"), new("DEMO-ADJ-04", "Noodles / single pack", "Adjusted", "+4", "Approved count correction")],
            Pipeline = [new("Processing",23 * scale,"info"),new("Ready",17 * scale,"warning"),new("Dispatched",8 * scale,"brand"),new("Completed",36 * scale,"success")],
            Sellers = [new("Long-grain rice","5 kg bag","rice",56 * scale),new("Vegetable cooking oil","1 litre bottle","oil",44 * scale),new("Fresh eggs","Tray of 30","eggs",32 * scale),new("White beans","1 kg pack","beans",24 * scale)],
            RecentOrders = [new("DEMO-0106","Sample customer 06","Delivery","GH₵ 240","Paid (sample)","Processing","info"),new("DEMO-0105","Sample customer 05","Pickup","GH₵ 180","Paid (sample)","Ready","warning"),new("DEMO-0104","Sample customer 04","Delivery","GH₵ 320","Paid (sample)","Dispatched","brand"),new("DEMO-0103","Sample customer 03","Pickup","GH₵ 150","Paid (sample)","Completed","success"),new("DEMO-0102","Sample customer 02","Delivery","GH₵ 95","Pending (sample)","Awaiting payment","neutral")]
        };
    }
}
