using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Migration;
using STailor.UI.Rcl.Models;

namespace STailor.UI.Rcl.Services;

public sealed class LegacyMigrationSubmissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;

    public LegacyMigrationSubmissionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LegacyMigrationSubmissionResult> SubmitAsync(
        LegacyMigrationSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(request.ApiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return LegacyMigrationSubmissionResult.Failure("API base URL is invalid.");
        }

        if (!TryParseJsonArray(request.CustomersJson, "customers", out IReadOnlyList<LegacyCustomerMigrationDto> customers, out var customerError))
        {
            return LegacyMigrationSubmissionResult.Failure(customerError!);
        }

        if (!TryParseJsonArray(request.OrdersJson, "orders", out IReadOnlyList<LegacyOrderMigrationDto> orders, out var orderError))
        {
            return LegacyMigrationSubmissionResult.Failure(orderError!);
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                new Uri(baseUri, "api/migration/import"),
                new LegacyMigrationImportRequest(
                    customers,
                    orders,
                    request.ImportInactiveCustomers,
                    request.ImportClosedOrders),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return LegacyMigrationSubmissionResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var report = await response.Content.ReadFromJsonAsync<LegacyMigrationReportDto>(
                cancellationToken: cancellationToken);

            if (report is null)
            {
                return LegacyMigrationSubmissionResult.Failure(
                    "Migration import succeeded but no report payload was returned.");
            }

            return LegacyMigrationSubmissionResult.Success(report);
        }
        catch (HttpRequestException exception)
        {
            return LegacyMigrationSubmissionResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return LegacyMigrationSubmissionResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return LegacyMigrationSubmissionResult.Failure($"Migration import failed: {exception.Message}");
        }
    }

    private static bool TryParseJsonArray<T>(
        string json,
        string label,
        out IReadOnlyList<T> items,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            items = [];
            error = null;
            return true;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
            items = parsed;
            error = null;
            return true;
        }
        catch (JsonException exception)
        {
            items = [];
            error = $"Invalid {label} JSON: {exception.Message}";
            return false;
        }
    }

    private static string NormalizeBaseUrl(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return string.Empty;
        }

        var normalized = apiBaseUrl.Trim();
        if (!normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized += "/";
        }

        return normalized;
    }

    private static async Task<string> ExtractErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return $"Request failed with HTTP {(int)response.StatusCode}.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                var message = errorElement.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }
        }
        catch (JsonException)
        {
            // Fall through to raw content when API did not return JSON.
        }

        return content;
    }
}
