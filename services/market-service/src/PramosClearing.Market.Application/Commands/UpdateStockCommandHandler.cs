using MediatR;
using PramosClearing.MarketService.Domain.Repositories;

namespace PramosClearing.MarketService.Application.Commands;

public sealed class UpdateStockCommandHandler : IRequestHandler<UpdateStockCommand>
{
    private readonly IStockRepository _repository;

    public UpdateStockCommandHandler(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(UpdateStockCommand command, CancellationToken ct)
    {
        var stock = await _repository.GetByIdAsync(command.Id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Stock '{command.Id}' not found.");

        stock.Update(command.Name, command.Currency, command.Exchange, command.Sector);

        await _repository.UpdateAsync(stock, ct).ConfigureAwait(false);
    }
}
