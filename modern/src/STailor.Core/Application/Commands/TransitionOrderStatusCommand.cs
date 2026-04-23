using STailor.Core.Domain.Enums;

namespace STailor.Core.Application.Commands;

public sealed record TransitionOrderStatusCommand(
    Guid OrderId,
    OrderStatus TargetStatus);
