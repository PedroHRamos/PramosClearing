using PramosClearing.UserService.Domain.Entities;

namespace PramosClearing.UserService.Domain.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    Task<User?> GetByUsernameAsync(string username, CancellationToken ct);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct);

    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);
}
