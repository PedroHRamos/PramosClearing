using MediatR;
using Microsoft.AspNetCore.Mvc;
using PramosClearing.MarketService.Application.Commands;
using PramosClearing.MarketService.Application.Commands.Requests;
using PramosClearing.MarketService.Application.Queries;
using PramosClearing.MarketService.Application.Queries.Responses;
namespace PramosClearing.MarketService.API.Controllers;

[ApiController]
[Route("api/stocks")]
public sealed class StocksController : ControllerBase
{
    private readonly IMediator _mediator;

    public StocksController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IResult> CreateAsync(
        [FromBody] CreateStockRequest request,
        CancellationToken ct)
    {
        try
        {
            var id = await _mediator.Send(
                new CreateStockCommand(
                    request.Name,
                    request.Currency,
                    request.Symbol,
                    request.Exchange,
                    request.Sector),
                ct).ConfigureAwait(false);

            return TypedResults.Created($"/api/stocks/{id}", id);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title  = "Conflict",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
    }

    [HttpGet("{symbol}")]
    [ProducesResponseType(typeof(StockResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GetBySymbolAsync(
        [FromRoute] string symbol,
        CancellationToken ct)
    {
        var result = await _mediator
            .Send(new GetStockBySymbolQuery(symbol), ct)
            .ConfigureAwait(false);

        return result is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<StockResponse>), StatusCodes.Status200OK)]
    public async Task<IResult> GetPagedAsync(
        [FromQuery] string symbol = "",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator
            .Send(new GetStocksPagedQuery(symbol, page, pageSize), ct)
            .ConfigureAwait(false);

        return TypedResults.Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> UpdateAsync(
        [FromRoute] Guid id,
        [FromBody] UpdateStockRequest request,
        CancellationToken ct)
    {
        try
        {
            await _mediator.Send(
                new UpdateStockCommand(id, request.Name, request.Currency, request.Exchange, request.Sector),
                ct).ConfigureAwait(false);

            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> DeleteAsync(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new DeleteStockCommand(id), ct).ConfigureAwait(false);

            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title  = "Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }
}
