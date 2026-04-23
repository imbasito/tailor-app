using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Application.Commands;
using STailor.Core.Application.ReadModels;
using STailor.Core.Domain.Entities;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class FakeCustomerService : ICustomerService
{
    public List<CreateCustomerCommand> CreateCommands { get; } = [];

    public List<CustomerProfile> CreatedProfiles { get; } = [];

    public IReadOnlyList<CustomerWorkspaceItem> WorklistResult { get; set; } = [];

    public CustomerWorkspaceDetail? DetailResult { get; set; }

    public Task<CustomerProfile> CreateAsync(
        CreateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        CreateCommands.Add(command);

        var profile = new CustomerProfile(
            command.FullName,
            command.PhoneNumber,
            command.City,
            command.Notes);
        profile.StampCreated(DateTimeOffset.UtcNow, "legacy-import");

        CreatedProfiles.Add(profile);
        return Task.FromResult(profile);
    }

    public Task<CustomerProfile> UpdateAsync(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Update is not used by this fake in migration tests.");
    }

    public Task<IReadOnlyList<CustomerWorkspaceItem>> GetWorklistAsync(
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(WorklistResult);
    }

    public Task<CustomerWorkspaceDetail?> GetWorkspaceDetailAsync(
        Guid customerId,
        int recentOrderLimit,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DetailResult);
    }

    public Task<CustomerProfile> UpsertBaselineMeasurementsAsync(
        UpsertBaselineMeasurementsCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Baseline updates are not used by this fake in migration tests.");
    }

    public Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Delete is not used by this fake in migration tests.");
    }
}
