using MediatR;
using PramosClearing.MarketService.Domain.Repositories;

namespace PramosClearing.MarketService.Application.Commands;

public sealed class DeleteStockCommandHandler : IRequestHandler<DeleteStockCommand>
{
    private readonly IStockRepository _repository;

    public DeleteStockCommandHandler(IStockRepository repository)
    {
        _repository = repository;
    }

    public async Task Handle(DeleteStockCommand command, CancellationToken ct)
    {
        var stock = await _repository.GetByIdAsync(command.Id, ct).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Stock '{command.Id}' not found.");

        stock.Deactivate();

        await _repository.DeleteAsync(stock, ct).ConfigureAwait(false);
    }
}
