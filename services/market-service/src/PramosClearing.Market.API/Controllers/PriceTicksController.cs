using MediatR;
using Microsoft.AspNetCore.Mvc;
using PramosClearing.MarketService.Application.Queries;
using PramosClearing.MarketService.Application.Queries.Responses;

namespace PramosClearing.MarketService.API.Controllers;

[ApiController]
[Route("api/price-ticks")]
public sealed class PriceTicksController : ControllerBase
{
    private readonly IMediator _mediator;

    public PriceTicksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PriceTickResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetLatestAsync(
        [FromQuery] string symbol,
        [FromQuery] string exchange,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var result = await _mediator
            .Send(new GetPriceTicksQuery(symbol, exchange, take), ct)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    [HttpGet("latest")]
    [ProducesResponseType(typeof(PriceTickResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetLatestSingleAsync(
        [FromQuery] string symbol,
        [FromQuery] string exchange,
        CancellationToken ct)
    {
        var result = await _mediator
            .Send(new GetLatestPriceTickQuery(symbol, exchange), ct)
            .ConfigureAwait(false);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }
}