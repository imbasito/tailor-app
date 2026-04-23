namespace STailor.Core.Application.Commands;

public sealed record ScheduleTrialFittingCommand(
    Guid OrderId,
    DateTimeOffset TrialAtUtc,
    string ScheduleStatus,
    bool ApplyTrialStatusTransition);
