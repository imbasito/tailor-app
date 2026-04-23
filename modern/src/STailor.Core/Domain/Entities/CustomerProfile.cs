using STailor.Core.Common.Entities;
using STailor.Core.Domain.Exceptions;

namespace STailor.Core.Domain.Entities;

public class CustomerProfile : AuditableEntity
{
    private CustomerProfile()
    {
    }

    public CustomerProfile(string fullName, string phoneNumber, string city, string? notes = null)
    {
        UpdateIdentity(fullName, phoneNumber, city, notes);
        BaselineMeasurementsJson = "{}";
    }

    public string FullName { get; private set; } = string.Empty;

    public string PhoneNumber { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string? Notes { get; private set; }

    public string BaselineMeasurementsJson { get; private set; } = "{}";

    public void UpdateIdentity(string fullName, string phoneNumber, string city, string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainRuleViolationException("Full name is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainRuleViolationException("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            throw new DomainRuleViolationException("City is required.");
        }

        FullName = fullName.Trim();
        PhoneNumber = phoneNumber.Trim();
        City = city.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void SetBaselineMeasurements(string baselineMeasurementsJson)
    {
        if (string.IsNullOrWhiteSpace(baselineMeasurementsJson))
        {
            throw new DomainRuleViolationException("Measurements payload cannot be empty.");
        }

        BaselineMeasurementsJson = baselineMeasurementsJson;
    }
}
