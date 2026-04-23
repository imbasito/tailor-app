using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Orders;

namespace STailor.UI.Rcl.Services;

public sealed class OrderWorklistService
{
    private readonly HttpClient _httpClient;

    public OrderWorklistService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderWorklistResult> GetAsync(
        string apiBaseUrl,
        bool includeDelivered,
        int maxItems,
        string? statusFilter = null,
        bool overdueOnly = false,
        DateTimeOffset? dueOnOrBeforeUtc = null,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderWorklistResult.Failure("API base URL is invalid.");
        }

        if (maxItems <= 0 || maxItems > 500)
        {
            return OrderWorklistResult.Failure("Max items must be between 1 and 500.");
        }

        var queryParts = new List<string>
        {
            $"includeDelivered={includeDelivered.ToString().ToLowerInvariant()}",
            $"overdueOnly={overdueOnly.ToString().ToLowerInvariant()}",
            $"maxItems={maxItems}",
        };

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            queryParts.Add($"status={Uri.EscapeDataString(statusFilter)}");
        }

        if (dueOnOrBeforeUtc is not null)
        {
            queryParts.Add($"dueOnOrBeforeUtc={Uri.EscapeDataString(dueOnOrBeforeUtc.Value.ToString("O"))}");
        }

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            queryParts.Add($"search={Uri.EscapeDataString(searchText.Trim())}");
        }

        var targetUri = new Uri(
            baseUri,
            $"api/orders/worklist?{string.Join("&", queryParts)}");

        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OrderWorklistResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<OrderWorklistItemDto>>(
                cancellationToken: cancellationToken);

            return OrderWorklistResult.Success(items ?? []);
        }
        catch (HttpRequestException exception)
        {
            return OrderWorklistResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderWorklistResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderWorklistResult.Failure($"Failed to load order worklist: {exception.Message}");
        }
    }

    public async Task<OrderStatusTransitionResult> TransitionStatusAsync(
        string apiBaseUrl,
        Guid orderId,
        string targetStatus,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderStatusTransitionResult.Failure("API base URL is invalid.");
        }

        if (orderId == Guid.Empty)
        {
            return OrderStatusTransitionResult.Failure("Order id is required.");
        }

        if (string.IsNullOrWhiteSpace(targetStatus))
        {
            return OrderStatusTransitionResult.Failure("Target status is required.");
        }

        var targetUri = new Uri(baseUri, $"api/orders/{orderId}/status");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                targetUri,
                new TransitionOrderStatusRequest(targetStatus),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return OrderStatusTransitionResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var order = await response.Content.ReadFromJsonAsync<OrderDto>(
                cancellationToken: cancellationToken);

            if (order is null)
            {
                return OrderStatusTransitionResult.Failure(
                    "Status transition succeeded but no order payload was returned.");
            }

            return OrderStatusTransitionResult.Success(order);
        }
        catch (HttpRequestException exception)
        {
            return OrderStatusTransitionResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderStatusTransitionResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderStatusTransitionResult.Failure($"Failed to transition order status: {exception.Message}");
        }
    }

    public async Task<OrderWorkspaceDetailResult> GetDetailAsync(
        string apiBaseUrl,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderWorkspaceDetailResult.Failure("API base URL is invalid.");
        }

        if (orderId == Guid.Empty)
        {
            return OrderWorkspaceDetailResult.Failure("Order id is required.");
        }

        var targetUri = new Uri(baseUri, $"api/orders/{orderId}");

        try
        {
            using var response = await _httpClient.GetAsync(targetUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return OrderWorkspaceDetailResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var order = await response.Content.ReadFromJsonAsync<OrderWorkspaceDetailDto>(
                cancellationToken: cancellationToken);

            return order is null
                ? OrderWorkspaceDetailResult.Failure("Order detail response was empty.")
                : OrderWorkspaceDetailResult.Success(order);
        }
        catch (HttpRequestException exception)
        {
            return OrderWorkspaceDetailResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderWorkspaceDetailResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderWorkspaceDetailResult.Failure($"Failed to load order detail: {exception.Message}");
        }
    }

    public async Task<OrderDeletionResult> DeleteAsync(
        string apiBaseUrl,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderDeletionResult.Failure("API base URL is invalid.");
        }

        if (orderId == Guid.Empty)
        {
            return OrderDeletionResult.Failure("Order id is required.");
        }

        var targetUri = new Uri(baseUri, $"api/orders/{orderId}");

        try
        {
            using var response = await _httpClient.DeleteAsync(targetUri, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return OrderDeletionResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            return OrderDeletionResult.Success();
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
            return OrderDeletionResult.Failure($"Failed to delete order: {exception.Message}");
        }
    }

    public async Task<OrderPaymentResult> AddPaymentAsync(
        string apiBaseUrl,
        Guid orderId,
        decimal amount,
        string? note,
        DateTimeOffset? paidAtUtc = null,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(apiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderPaymentResult.Failure("API base URL is invalid.");
        }

        if (orderId == Guid.Empty)
        {
            return OrderPaymentResult.Failure("Order id is required.");
        }

        if (amount <= 0)
        {
            return OrderPaymentResult.Failure("Payment amount must be greater than zero.");
        }

        var targetUri = new Uri(baseUri, $"api/orders/{orderId}/payments");

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                targetUri,
                new AddPaymentRequest(amount, paidAtUtc, string.IsNullOrWhiteSpace(note) ? null : note.Trim()),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return OrderPaymentResult.Failure(
                    await ExtractErrorAsync(response, cancellationToken));
            }

            var order = await response.Content.ReadFromJsonAsync<OrderDto>(
                cancellationToken: cancellationToken);

            if (order is null)
            {
                return OrderPaymentResult.Failure(
                    "Payment succeeded but no order payload was returned.");
            }

            return OrderPaymentResult.Success(order);
        }
        catch (HttpRequestException exception)
        {
            return OrderPaymentResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderPaymentResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderPaymentResult.Failure($"Failed to add payment: {exception.Message}");
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
