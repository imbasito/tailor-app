using STailor.Core.Application.Abstractions;

namespace STailor.Modules.Core.Tests.Fakes;

internal sealed class FakeCurrentUserService : ICurrentUserService
{
    private readonly string _userId;

    public FakeCurrentUserService(string userId)
    {
        _userId = userId;
    }

    public string GetCurrentUserId()
    {
        return _userId;
    }
}
