using MediatR;
using PramosClearing.MarketService.Application.Ports;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.Application.Queries;

public sealed class GetPriceTicksQueryHandler : IRequestHandler<GetPriceTicksQuery, IReadOnlyList<PriceTickResponse>>
{
    private readonly IPriceTickReadRepository _repository;

    public GetPriceTicksQueryHandler(IPriceTickReadRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PriceTickResponse>> Handle(GetPriceTicksQuery query, CancellationToken ct)
    {
        var normalizedSymbol = query.Symbol.ToUpperInvariant();
        var normalizedExchange = query.Exchange.ToUpperInvariant();
        var take = Math.Clamp(query.Take, 1, 500);

        return await _repository
            .GetLatestAsync(normalizedSymbol, normalizedExchange, take, ct)
            .ConfigureAwait(false);
    }
}