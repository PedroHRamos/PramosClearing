using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PramosClearing.OrderBook.Application.Options;
using PramosClearing.OrderBook.Application.Ports;
using PramosClearing.OrderBook.Application.Services;

namespace PramosClearing.OrderBook.Worker.Workers;

public sealed class OrderBookSimulatorWorker : BackgroundService
{
    private readonly SimulationEngine _engine;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SimulationOptions _options;
    private readonly ILogger<OrderBookSimulatorWorker> _logger;

    public OrderBookSimulatorWorker(
        SimulationEngine engine,
        IServiceScopeFactory scopeFactory,
        IOptions<SimulationOptions> options,
        ILogger<OrderBookSimulatorWorker> logger)
    {
        _engine       = engine;
        _scopeFactory = scopeFactory;
        _options      = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderBook simulator starting.");

        await using (var scope = _scopeFactory.CreateAsyncScope())
        {
            var loader = scope.ServiceProvider.GetRequiredService<IStockLoader>();
            var stocks = await loader.LoadAllActiveAsync(stoppingToken).ConfigureAwait(false);
            _engine.Initialize(stocks);
        }

        _logger.LogInformation(
            "Simulation running. Tick interval: {Min}–{Max} ms.",
            _options.MinDelayMs,
            _options.MaxDelayMs);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _engine.TickAsync(stoppingToken).ConfigureAwait(false);

            var delay = Random.Shared.Next(_options.MinDelayMs, _options.MaxDelayMs + 1);
            await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("OrderBook simulator stopped.");
    }
}
