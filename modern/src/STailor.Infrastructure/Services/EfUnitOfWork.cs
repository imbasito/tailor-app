using STailor.Core.Application.Abstractions;
using STailor.Infrastructure.Persistence;

namespace STailor.Infrastructure.Services;

public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly LocalTailorDbContext _dbContext;

    public EfUnitOfWork(LocalTailorDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
