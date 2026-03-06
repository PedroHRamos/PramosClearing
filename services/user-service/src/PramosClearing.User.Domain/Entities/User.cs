using System.Security.Cryptography;

namespace PramosClearing.UserService.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string PasswordSalt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private readonly List<UserBalance> _balances = new();
    public IReadOnlyList<UserBalance> Balances => _balances.AsReadOnly();

    public User(Guid id, string email, string username, string password)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("User id must not be empty.", nameof(id));

        ArgumentException.ThrowIfNullOrWhiteSpace(email, nameof(email));
        ArgumentException.ThrowIfNullOrWhiteSpace(username, nameof(username));
        ArgumentException.ThrowIfNullOrWhiteSpace(password, nameof(password));

        Id           = id;
        Email        = email.ToLowerInvariant();
        Username     = username;
        IsActive     = true;
        CreatedAt    = DateTime.UtcNow;

        var saltBytes = RandomNumberGenerator.GetBytes(64);
        PasswordSalt  = Convert.ToBase64String(saltBytes);
        PasswordHash  = Convert.ToBase64String(
            Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, 600_000, HashAlgorithmName.SHA512, 64));
    }

    private User()
    {
        Email        = null!;
        Username     = null!;
        PasswordHash = null!;
        PasswordSalt = null!;
    }
}
