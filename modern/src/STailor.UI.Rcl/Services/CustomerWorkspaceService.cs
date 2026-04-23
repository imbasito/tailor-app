using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Measurements;

namespace STailor.UI.Rcl.Services;

public sealed class CustomerWorkspaceService
{
    private readonly HttpClient _httpClient;

    public CustomerWorkspaceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CustomerWorkspaceListResult> GetWorklistAsync(
        string apiBaseUrl,
        string? searchText,
        int maxItems,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return CustomerWorkspaceListResult.Failure("API base URL is invalid.");
        }

        if (maxItems <= 0 || maxItems > 500)
        {
            return CustomerWorkspaceListResult.Failure("Max items must be between 1 and 500.");
        }

        var queryParts = new List<string> { $"maxItems={maxItems}" };
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryParts.Add($"search={Uri.EscapeDataString(searchText.Trim())}");
        }

        var targetUri = new Uri(baseUri, $"api/customers?{string.Join("&", queryParts)}");
        return await GetListAsync(targetUri, cancellationToken);
    }

    public async Task<CustomerWorkspaceDetailResult> GetDetailAsync(
        string apiBaseUrl,
        Guid customerId,
        int recentOrderLimit,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return CustomerWorkspaceDetailResult.Failure("API base URL is invalid.");
        }

        if (customerId == Guid.Empty)
        {
            return CustomerWorkspaceDetailResult.Failure("Customer id is required.");
        }

        if (recentOrderLimit <= 0 || recentOrderLimit > 50)
        {
            return CustomerWorkspaceDetailResult.Failure("Recent order limit must be between 1 and 50.");
        }

        var targetUri = new Uri(baseUri, $"api/customers/{customerId}?recentOrderLimit={recentOrderLimit}");

        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return CustomerWorkspaceDetailResult.Failure("Customer profile was not found.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return CustomerWorkspaceDetailResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var customer = await response.Content.ReadFromJsonAsync<CustomerWorkspaceDetailDto>(
                cancellationToken: cancellationToken);

            return customer is null
                ? CustomerWorkspaceDetailResult.Failure("Customer detail response was empty.")
                : CustomerWorkspaceDetailResult.Success(customer);
        }
        catch (HttpRequestException exception)
        {
            return CustomerWorkspaceDetailResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerWorkspaceDetailResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return CustomerWorkspaceDetailResult.Failure($"Failed to load customer detail: {exception.Message}");
        }
    }

    public async Task<OrderDeletionResult> DeleteAsync(
        string apiBaseUrl,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderDeletionResult.Failure("API base URL is invalid.");
        }

        if (customerId == Guid.Empty)
        {
            return OrderDeletionResult.Failure("Customer id is required.");
        }

        var targetUri = new Uri(baseUri, $"api/customers/{customerId}");

        try
        {
            using var response = await _httpClient.DeleteAsync(targetUri, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return OrderDeletionResult.Success();
            }

            return OrderDeletionResult.Failure(
                await ExtractErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            return OrderDeletionResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderDeletionResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderDeletionResult.Failure($"Failed to delete customer: {exception.Message}");
        }
    }

    public async Task<CustomerMeasurementSaveResult> UpdateAsync(
        string apiBaseUrl,
        Guid customerId,
        string fullName,
        string phoneNumber,
        string city,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return CustomerMeasurementSaveResult.Failure("API base URL is invalid.");
        }

        if (customerId == Guid.Empty)
        {
            return CustomerMeasurementSaveResult.Failure("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return CustomerMeasurementSaveResult.Failure("Customer name is required.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return CustomerMeasurementSaveResult.Failure("Phone number is required.");
        }

        if (string.IsNullOrWhiteSpace(city))
        {
            return CustomerMeasurementSaveResult.Failure("City is required.");
        }

        var targetUri = new Uri(baseUri, $"api/customers/{customerId}");
        var payload = new UpdateCustomerRequest(
            fullName.Trim(),
            phoneNumber.Trim(),
            city.Trim(),
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());

        try
        {
            using var response = await _httpClient.PutAsJsonAsync(targetUri, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CustomerMeasurementSaveResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var customer = await response.Content.ReadFromJsonAsync<CustomerProfileDto>(
                cancellationToken: cancellationToken);

            return customer is null
                ? CustomerMeasurementSaveResult.Failure("Customer update response was empty.")
                : CustomerMeasurementSaveResult.Success(customer);
        }
        catch (HttpRequestException exception)
        {
            return CustomerMeasurementSaveResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerMeasurementSaveResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return CustomerMeasurementSaveResult.Failure($"Failed to update customer: {exception.Message}");
        }
    }

    public async Task<CustomerMeasurementSaveResult> UpsertBaselineMeasurementsAsync(
        string apiBaseUrl,
        Guid customerId,
        string garmentType,
        IReadOnlyDictionary<string, decimal> measurements,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return CustomerMeasurementSaveResult.Failure("API base URL is invalid.");
        }

        if (customerId == Guid.Empty)
        {
            return CustomerMeasurementSaveResult.Failure("Customer id is required.");
        }

        if (string.IsNullOrWhiteSpace(garmentType))
        {
            return CustomerMeasurementSaveResult.Failure("Garment type is required.");
        }

        if (measurements.Count == 0)
        {
            return CustomerMeasurementSaveResult.Failure("At least one measurement is required.");
        }

        var targetUri = new Uri(baseUri, $"api/customers/{customerId}/measurements");
        var payload = new MeasurementSetDto(garmentType.Trim(), measurements);

        try
        {
            using var response = await _httpClient.PutAsJsonAsync(targetUri, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CustomerMeasurementSaveResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var customer = await response.Content.ReadFromJsonAsync<CustomerProfileDto>(
                cancellationToken: cancellationToken);

            return customer is null
                ? CustomerMeasurementSaveResult.Failure("Measurement save response was empty.")
                : CustomerMeasurementSaveResult.Success(customer);
        }
        catch (HttpRequestException exception)
        {
            return CustomerMeasurementSaveResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerMeasurementSaveResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return CustomerMeasurementSaveResult.Failure($"Failed to save measurements: {exception.Message}");
        }
    }

    private async Task<CustomerWorkspaceListResult> GetListAsync(Uri targetUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CustomerWorkspaceListResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<CustomerWorkspaceItemDto>>(
                cancellationToken: cancellationToken);

            return CustomerWorkspaceListResult.Success(items ?? []);
        }
        catch (HttpRequestException exception)
        {
            return CustomerWorkspaceListResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CustomerWorkspaceListResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return CustomerWorkspaceListResult.Failure($"Failed to load customers: {exception.Message}");
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
        }

        return content;
    }
}
