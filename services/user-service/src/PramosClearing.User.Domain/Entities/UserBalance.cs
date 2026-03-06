namespace PramosClearing.UserService.Domain.Entities;

public sealed class UserBalance
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Currency { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public UserBalance(Guid id, Guid userId, string currency, decimal initialAmount)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("UserBalance id must not be empty.", nameof(id));

        if (userId == Guid.Empty)
            throw new ArgumentException("UserId must not be empty.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(currency, nameof(currency));

        if (initialAmount < 0)
            throw new ArgumentOutOfRangeException(nameof(initialAmount), "Balance amount must be zero or greater.");

        Id        = id;
        UserId    = userId;
        Currency  = currency.ToUpperInvariant();
        Amount    = initialAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    private UserBalance()
    {
        Currency = null!;
    }
}
