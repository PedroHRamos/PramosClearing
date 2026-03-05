using MediatR;
using Microsoft.AspNetCore.Mvc;
using PramosClearing.MarketService.Application.Commands;
using PramosClearing.MarketService.Application.Commands.Requests;

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
}
