using System.Text.Json;
using STailor.Core.Application.Abstractions.Services;
using STailor.Core.Domain.Exceptions;

namespace STailor.Modules.Core.Services;

public sealed class MeasurementService : IMeasurementService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public IReadOnlyDictionary<string, decimal> MergeMeasurements(
        IReadOnlyDictionary<string, decimal> baseline,
        IReadOnlyDictionary<string, decimal>? overrides)
    {
        var merged = new Dictionary<string, decimal>(baseline, StringComparer.OrdinalIgnoreCase);

        if (overrides is null)
        {
            return merged;
        }

        foreach (var (key, value) in overrides)
        {
            merged[key] = value;
        }

        return merged;
    }

    public string Serialize(IReadOnlyDictionary<string, decimal> measurements)
    {
        return JsonSerializer.Serialize(measurements, JsonOptions);
    }

    public IReadOnlyDictionary<string, decimal> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var measurements = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json, JsonOptions)
                ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

            return new Dictionary<string, decimal>(measurements, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException exception)
        {
            throw new DomainRuleViolationException($"Invalid measurements payload: {exception.Message}");
        }
    }
}
