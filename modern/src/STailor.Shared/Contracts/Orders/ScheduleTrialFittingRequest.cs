namespace STailor.Shared.Contracts.Orders;

public sealed record ScheduleTrialFittingRequest(
    DateTimeOffset TrialAtUtc,
    string ScheduleStatus,
    bool ApplyTrialStatusTransition);
