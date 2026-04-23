namespace STailor.UI.Rcl.Models;

public sealed record OrderWizardSubmissionRequest(
    string ApiBaseUrl,
    Guid? ExistingCustomerId,
    string FullName,
    string PhoneNumber,
    string City,
    string? Notes,
    string GarmentType,
    IReadOnlyDictionary<string, decimal> Measurements,
    IReadOnlyList<OrderWizardPhotoAttachmentInput> PhotoAttachments,
    decimal AmountCharged,
    decimal InitialDeposit,
    DateTimeOffset DueAtUtc,
    DateTimeOffset? TrialScheduledAtUtc,
    string TrialScheduleStatus,
    bool ApplyTrialStatusTransition,
    string TargetStatus);
