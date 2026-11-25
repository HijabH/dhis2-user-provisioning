namespace Dhis2UserProvisioning.Models;

public enum ProvisioningStatus
{
    Created,
    WouldCreate,
    Skipped,
    Invalid,
    Failed,
}

public sealed record ProvisioningResult(
    int RowNumber,
    string Username,
    ProvisioningStatus Status,
    string Message,
    string? UserId = null,
    string? TemporaryPassword = null);
