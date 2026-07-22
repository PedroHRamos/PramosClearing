using MediatR;
using PramosClearing.MarketService.Application.Ports;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed class GetLatestPriceTickQueryHandler : IRequestHandler<GetLatestPriceTickQuery, PriceTickResponse?>
{
    private readonly IPriceTickReadRepository _repository;

    public GetLatestPriceTickQueryHandler(IPriceTickReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<PriceTickResponse?> Handle(GetLatestPriceTickQuery query, CancellationToken ct)
    {
        var normalizedSymbol = query.Symbol.ToUpperInvariant();
        var normalizedExchange = query.Exchange.ToUpperInvariant();

        return await _repository
            .GetLatestSingleAsync(normalizedSymbol, normalizedExchange, ct)
            .ConfigureAwait(false);
    }
}