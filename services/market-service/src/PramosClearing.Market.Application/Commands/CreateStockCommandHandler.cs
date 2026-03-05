using MediatR;
using PramosClearing.MarketService.Domain.Entities;
using PramosClearing.MarketService.Domain.Repositories;

namespace PramosClearing.MarketService.Application.Commands;

public sealed class CreateStockCommandHandler : IRequestHandler<CreateStockCommand, Guid>
{
    private readonly IStockRepository _repository;

    public CreateStockCommandHandler(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid> Handle(CreateStockCommand command, CancellationToken ct)
    {
        var normalizedSymbol   = command.Symbol.ToUpperInvariant();
        var normalizedExchange = command.Exchange.ToUpperInvariant();

        if (await _repository.ExistsAsync(normalizedSymbol, ct).ConfigureAwait(false))
            throw new InvalidOperationException(
                $"A stock with symbol '{normalizedSymbol}' already exists.");

        var stock = new Stock(
            id:       Guid.NewGuid(),
            name:     command.Name,
            currency: command.Currency,
            symbol:   normalizedSymbol,
            exchange: normalizedExchange,
            sector:   command.Sector);

        await _repository.AddAsync(stock, ct).ConfigureAwait(false);

        return stock.Id;
    }
}
