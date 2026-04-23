using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed class OrderReminderWorklistService
{
    private readonly HttpClient _httpClient;

    public OrderReminderWorklistService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderReminderWorklistResult> GetAsync(
        string apiBaseUrl,
        int daysAhead,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderReminderWorklistResult.Failure("API base URL is invalid.");
        }

        if (daysAhead < 0 || daysAhead > 365)
        {
            return OrderReminderWorklistResult.Failure("Days ahead must be between 0 and 365.");
        }

        if (maxItems <= 0 || maxItems > 500)
        {
            return OrderReminderWorklistResult.Failure("Max items must be between 1 and 500.");
        }

        var dueOnOrBeforeUtc = DateTimeOffset.UtcNow.Date.AddDays(daysAhead + 1).AddTicks(-1);
        var targetUri = new Uri(
            baseUri,
            $"api/orders/reminders?dueOnOrBeforeUtc={Uri.EscapeDataString(dueOnOrBeforeUtc.ToString("O", CultureInfo.InvariantCulture))}&maxItems={maxItems}");

        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OrderReminderWorklistResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderReminderDto>>(
                cancellationToken: cancellationToken);

            return OrderReminderWorklistResult.Success(items ?? []);
        }
        catch (HttpRequestException exception)
        {
            return OrderReminderWorklistResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderReminderWorklistResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderReminderWorklistResult.Failure($"Failed to load reminders: {exception.Message}");
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
