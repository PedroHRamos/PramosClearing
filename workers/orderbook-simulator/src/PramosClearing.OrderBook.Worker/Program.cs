using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PramosClearing.OrderBook.Application.Options;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Application.Services;
using PramosClearing.OrderBook.Infrastructure;
using PramosClearing.OrderBook.Infrastructure.Persistence;
using PramosClearing.OrderBook.Infrastructure.Publishers;
using PramosClearing.OrderBook.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ConnectionStringsOptions>(
    builder.Configuration.GetSection("ConnectionStrings"));

builder.Services.Configure<SimulationOptions>(
    builder.Configuration.GetSection("Simulation"));

builder.Services.AddDbContext<MarketReadDbContext>((sp, options) =>
{
    var cs = sp.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
    options.UseSqlServer(cs.MarketConnection)
           .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.Configure<KafkaOptions>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.AddScoped<IStockLoader, StockLoader>();
builder.Services.AddSingleton<IOrderBookEventPublisher, KafkaOrderBookEventPublisher>();
builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddHostedService<OrderBookSimulatorWorker>();

var host = builder.Build();

host.Run();
