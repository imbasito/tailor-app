namespace STailor.Core.Application.Commands;

public sealed record CreateOrderPhotoAttachmentCommand(
    string FileName,
    string ResourcePath,
    string? Notes);
