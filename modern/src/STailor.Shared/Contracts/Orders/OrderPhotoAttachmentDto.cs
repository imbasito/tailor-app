namespace STailor.Shared.Contracts.Orders;

public sealed record OrderPhotoAttachmentDto(
    string FileName,
    string ResourcePath,
    string? Notes);
