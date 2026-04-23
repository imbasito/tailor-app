using STailor.Core.Application.Migration;

namespace STailor.Core.Application.Abstractions.Services;

public interface ILegacyMigrationService
{
    Task<LegacyMigrationReport> ImportAsync(
        LegacyMigrationBatch batch,
        CancellationToken cancellationToken = default);
}
