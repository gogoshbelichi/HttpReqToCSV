using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;

Console.WriteLine("Starting...");
const string ordersApiUrl = "https://api.example.com/orders";
const string json = """
                    [
                        { "id": 1, "customerEmail": "anna@mail.com",  "amount": 120.50, "status": "paid", "createdAt": "2026-10-01T10:15:00Z" },
                        { "id": 2, "customerEmail": "ivan@mail.com",  "amount": 40.00,  "status": "cancelled", "createdAt": "2026-10-02T12:00:00Z" },
                        { "id": 3, "customerEmail": "anna@mail.com",  "amount": 79.50,  "status": "paid", "createdAt": "2026-10-03T09:30:00Z" },
                        { "id": 4, "customerEmail": "petr@mail.com",  "amount": 300.00, "status": "paid", "createdAt": "2026-10-03T14:45:00Z" },
                        { "id": 5, "customerEmail": "ivan@mail.com",  "amount": 55.25,  "status": "paid", "createdAt": "2026-10-04T08:10:00Z" },
                        { "id": 6, "customerEmail": "maria@mail.com", "amount": 99.99,  "status": "pending", "createdAt": "2026-10-05T16:20:00Z" },
                        { "id": 7, "customerEmail": "petr@mail.com",  "amount": 15.00,  "status": "paid", "createdAt": "2026-10-06T11:00:00Z" },
                        { "id": 8, "customerEmail": "maria@mail.com", "amount": 210.10, "status": "paid", "createdAt": "2026-10-07T13:35:00Z" },
                        { "id": 9, "customerEmail": "ivan@mail.com",  "amount": 44.75,  "status": "paid", "createdAt": "2026-10-08T19:05:00Z" },
                        { "id": 10, "customerEmail": "anna@mail.com",  "amount": 50.00,  "status": "refunded", "createdAt": "2026-10-09T10:00:00Z" }
                    ]
                    """;

using var client = new HttpClient(new FakeHandler(json));

var response = await client.GetAsync(ordersApiUrl);

if (response.IsSuccessStatusCode)
{
    var content = await response.Content.ReadFromJsonAsync<IList<Order>>();
    
    if (content == null)
    {
        // no data - no file - > log warn/error... depends on the expected behavior
        return;
    }
    
    var count = content.Count;

    if (count == 0)
    {
        Console.WriteLine("No orders found");
    }
    
    // validation + what to do with broken records
    
    Console.WriteLine("Validation...");
    
    // оставить только оплаченные заказы

    content = content.Where(o => o.Status == StatusExtensions.Paid).ToList();

    // сгруппировать по email

    var grouped = content.GroupBy(o => o.CustomerEmail);

    // каждую группу превратить в строку отчета

    var report = grouped
        .Select(g => new CustomerReport(
            g.Key,
            g.Count(),
            g.Sum(o => o.Amount)))
        .OrderByDescending(r => r.TotalAmount)
        .ToList();
    
    var lines = new List<string>() { $"{nameof(CustomerReport.Email)}, {nameof(CustomerReport.OrdersCount)}, {nameof(CustomerReport.TotalAmount)}" };
    
    lines.AddRange(report.Select(r => $"{r.Email}, {r.OrdersCount}, {r.TotalAmount:F2}"));
    
    var path = Path.Combine(AppContext.BaseDirectory, "report.csv");

    await File.WriteAllLinesAsync(path, lines);
    
    Console.WriteLine("Done");
    Console.WriteLine($"{path} exists: {File.Exists(path)}");
    Process.Start("xdg-open", path);
    Console.ReadLine();
}

public class FakeHandler : HttpMessageHandler
{
    private readonly string _json;

    public FakeHandler(string json) => _json = json;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_json, Encoding.UTF8, "application/json")
        };
        
        return Task.FromResult(response);
    }
}

record Order(int Id, string CustomerEmail, decimal Amount, string? Status, DateTime CreatedAt);

enum Status
{
    None = 0,
    Pending = 1,
    Paid = 2,
    Refunded = 3,
    Cancelled = 4,
}

static class StatusExtensions
{
    public const string Pending = "pending";
    public const string Refunded = "refunded";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";
    
    static Status Map(this string status) => status switch
    {
        Pending => Status.Pending,
        Paid => Status.Paid,
        Refunded => Status.Refunded,
        Cancelled => Status.Cancelled,
        _ => Status.None,  
    };    
}

record CustomerReport(string Email, int OrdersCount, decimal TotalAmount);
