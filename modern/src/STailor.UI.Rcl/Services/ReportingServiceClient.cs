using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Reports;

namespace STailor.UI.Rcl.Services;

/// <summary>
/// Client service for fetching reports from the API.
/// </summary>
public class ReportingServiceClient
{
    private readonly HttpClient _httpClient;

    public ReportingServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Gets the complete operations report.
    /// </summary>
    public async Task<OperationsReportResult> GetOperationsReportAsync(
        string apiBaseUrl,
        string? searchText = null,
        string? status = null,
        bool includeDelivered = true,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OperationsReportResult.Failure("API base URL is invalid.");
        }

        var queryParts = new List<string>
        {
            $"includeDelivered={includeDelivered.ToString().ToLowerInvariant()}",
        };

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryParts.Add($"search={Uri.EscapeDataString(searchText.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "Any", StringComparison.OrdinalIgnoreCase))
        {
            queryParts.Add($"status={Uri.EscapeDataString(status.Trim())}");
        }

        if (fromDate is not null)
        {
            queryParts.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }

        if (toDate is not null)
        {
            queryParts.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }

        var targetUri = new Uri(baseUri, $"api/reports/operations?{string.Join("&", queryParts)}");

        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OperationsReportResult.Failure(await ExtractErrorAsync(response, cancellationToken));
            }

            var report = await response.Content.ReadFromJsonAsync<OperationsReportDto>(cancellationToken: cancellationToken);
            return report is null
                ? OperationsReportResult.Failure("Operations report response was empty.")
                : OperationsReportResult.Success(report);
        }
        catch (HttpRequestException exception)
        {
            return OperationsReportResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OperationsReportResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OperationsReportResult.Failure($"Failed to load operations report: {exception.Message}");
        }
    }

    /// <summary>
    /// Gets daily orders report.
    /// </summary>
    public async Task<DailyOrdersReportDto?> GetDailyOrdersReportAsync(DateTime? date = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/reports/daily-orders?date={date:yyyy-MM-dd}";
        if (!date.HasValue)
        {
            url = "api/reports/daily-orders";
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DailyOrdersReportDto>(cancellationToken);
        }
        return null;
    }

    /// <summary>
    /// Gets outstanding dues report.
    /// </summary>
    public async Task<OutstandingDuesReportDto?> GetOutstandingDuesReportAsync(
        decimal? minBalanceDue = null,
        decimal? maxBalanceDue = null,
        string? status = null,
        bool overdueOnly = false,
        string orderBy = "BalanceDesc",
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>();

        if (minBalanceDue.HasValue)
            queryParams.Add($"minBalanceDue={minBalanceDue.Value}");
        if (maxBalanceDue.HasValue)
            queryParams.Add($"maxBalanceDue={maxBalanceDue.Value}");
        if (!string.IsNullOrWhiteSpace(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");
        if (overdueOnly)
            queryParams.Add("overdueOnly=true");
        if (!string.IsNullOrWhiteSpace(orderBy) && orderBy != "BalanceDesc")
            queryParams.Add($"orderBy={Uri.EscapeDataString(orderBy)}");

        var url = "api/reports/outstanding-dues";
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<OutstandingDuesReportDto>(cancellationToken);
        }
        return null;
    }

    /// <summary>
    /// Gets customer measurement history.
    /// </summary>
    public async Task<CustomerMeasurementHistoryDto?> GetCustomerMeasurementHistoryAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/reports/customers/{customerId}/measurement-history", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<CustomerMeasurementHistoryDto>(cancellationToken);
        }
        return null;
    }

    /// <summary>
    /// Gets delivery queue.
    /// </summary>
    public async Task<DeliveryQueueReportDto?> GetDeliveryQueueAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/reports/delivery-queue?fromDate={fromDate:yyyy-MM-dd}&toDate={toDate:yyyy-MM-dd}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<DeliveryQueueReportDto>(cancellationToken);
        }
        return null;
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
            // Fall through to raw content.
        }

        return content;
    }
}

public sealed record OperationsReportResult(
    bool IsSuccess,
    OperationsReportDto? Report,
    string? ErrorMessage)
{
    public static OperationsReportResult Success(OperationsReportDto report)
    {
        return new OperationsReportResult(true, report, null);
    }

    public static OperationsReportResult Failure(string errorMessage)
    {
        return new OperationsReportResult(false, null, errorMessage);
    }
}
