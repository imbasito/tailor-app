namespace STailor.UI.Rcl.Models;

public sealed record OrderWizardPhotoAttachmentInput(
    string FileName,
    string ResourcePath,
    string? Notes);
