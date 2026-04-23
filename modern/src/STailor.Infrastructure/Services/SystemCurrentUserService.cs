using STailor.Core.Application.Abstractions;

namespace STailor.Infrastructure.Services;

public sealed class SystemCurrentUserService : ICurrentUserService
{
    public string GetCurrentUserId()
    {
        return "system";
    }
}
