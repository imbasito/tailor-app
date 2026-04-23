using System.Net.Http.Json;
using System.Text.Json;
using STailor.Shared.Contracts.Customers;
using STailor.Shared.Contracts.Measurements;
using STailor.Shared.Contracts.Orders;
using STailor.UI.Rcl.Models;

namespace STailor.UI.Rcl.Services;

public sealed class OrderWizardSubmissionService
{
    private static readonly string[] OrderedStatuses =
    [
        "New",
        "InProgress",
        "TrialFitting",
        "Rework",
        "Ready",
        "Delivered",
    ];

    private readonly HttpClient _httpClient;

    public OrderWizardSubmissionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OrderWizardSubmissionResult> SubmitAsync(
        OrderWizardSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(NormalizeBaseUrl(request.ApiBaseUrl), UriKind.Absolute, out var baseUri))
        {
            return OrderWizardSubmissionResult.Failure("API base URL is invalid.");
        }

        if (request.Measurements.Count == 0)
        {
            return OrderWizardSubmissionResult.Failure("At least one measurement is required.");
        }

        if (request.InitialDeposit > request.AmountCharged)
        {
            return OrderWizardSubmissionResult.Failure("Initial deposit cannot exceed amount charged.");
        }

        try
        {
            var customer = await ResolveCustomerAsync(baseUri, request, cancellationToken);
            if (!customer.IsSuccess)
            {
                return OrderWizardSubmissionResult.Failure(customer.ErrorMessage!);
            }

            using var measurementResponse = await _httpClient.PutAsJsonAsync(
                new Uri(baseUri, $"api/customers/{customer.CustomerId}/measurements"),
                new MeasurementSetDto(request.GarmentType, request.Measurements),
                cancellationToken);

            if (!measurementResponse.IsSuccessStatusCode)
            {
                return OrderWizardSubmissionResult.Failure(
                    await ExtractErrorAsync(measurementResponse, cancellationToken));
            }

            using var orderResponse = await _httpClient.PostAsJsonAsync(
                new Uri(baseUri, "api/orders"),
                new CreateOrderRequest(
                    CustomerId: customer.CustomerId,
                    GarmentType: request.GarmentType,
                    OverrideMeasurements: null,
                    AmountCharged: request.AmountCharged,
                    InitialDeposit: request.InitialDeposit,
                    DueAtUtc: request.DueAtUtc,
                    PhotoAttachments: request.PhotoAttachments.Select(attachment =>
                        new OrderPhotoAttachmentDto(
                            attachment.FileName,
                            attachment.ResourcePath,
                            attachment.Notes))
                        .ToArray(),
                    TrialScheduledAtUtc: request.TrialScheduledAtUtc,
                    TrialScheduleStatus: request.TrialScheduleStatus,
                    ApplyTrialStatusTransition: request.ApplyTrialStatusTransition),
                cancellationToken);

            if (!orderResponse.IsSuccessStatusCode)
            {
                return OrderWizardSubmissionResult.Failure(
                    await ExtractErrorAsync(orderResponse, cancellationToken));
            }

            var order = await orderResponse.Content.ReadFromJsonAsync<OrderDto>(
                cancellationToken: cancellationToken);

            if (order is null)
            {
                return OrderWizardSubmissionResult.Failure(
                    "Order creation succeeded but no order payload was returned.");
            }

            var finalStatus = order.Status;
            if (!TryBuildTransitionTargets(order.Status, request.TargetStatus, out var transitionTargets, out var transitionError))
            {
                return OrderWizardSubmissionResult.Failure(transitionError!);
            }

            foreach (var transitionTarget in transitionTargets)
            {
                using var statusResponse = await _httpClient.PostAsJsonAsync(
                    new Uri(baseUri, $"api/orders/{order.Id}/status"),
                    new TransitionOrderStatusRequest(transitionTarget),
                    cancellationToken);

                if (!statusResponse.IsSuccessStatusCode)
                {
                    return OrderWizardSubmissionResult.Failure(
                        await ExtractErrorAsync(statusResponse, cancellationToken));
                }

                var transitionedOrder = await statusResponse.Content.ReadFromJsonAsync<OrderDto>(
                    cancellationToken: cancellationToken);

                if (transitionedOrder is null)
                {
                    return OrderWizardSubmissionResult.Failure(
                        "Status update succeeded but no order payload was returned.");
                }

                finalStatus = transitionedOrder.Status;
                order = transitionedOrder;
            }

            return OrderWizardSubmissionResult.Success(
                customer.CustomerId,
                order.Id,
                finalStatus,
                customer.FullName,
                customer.PhoneNumber,
                order.DueAtUtc,
                order.BalanceDue);
        }
        catch (HttpRequestException exception)
        {
            return OrderWizardSubmissionResult.Failure($"Unable to reach API: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OrderWizardSubmissionResult.Failure("API request timed out.");
        }
        catch (Exception exception)
        {
            return OrderWizardSubmissionResult.Failure($"Submission failed: {exception.Message}");
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

    private async Task<ResolvedCustomerResult> ResolveCustomerAsync(
        Uri baseUri,
        OrderWizardSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ExistingCustomerId.HasValue)
        {
            if (request.ExistingCustomerId.Value == Guid.Empty)
            {
                return ResolvedCustomerResult.Failure("Existing customer id is invalid.");
            }

            return ResolvedCustomerResult.Success(
                request.ExistingCustomerId.Value,
                request.FullName,
                request.PhoneNumber);
        }

        var customerPayload = new CreateCustomerRequest(
            request.FullName,
            request.PhoneNumber,
            request.City,
            request.Notes);

        using var customerResponse = await _httpClient.PostAsJsonAsync(
            new Uri(baseUri, "api/customers"),
            customerPayload,
            cancellationToken);

        if (!customerResponse.IsSuccessStatusCode)
        {
            return ResolvedCustomerResult.Failure(
                await ExtractErrorAsync(customerResponse, cancellationToken));
        }

        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerProfileDto>(
            cancellationToken: cancellationToken);

        if (customer is null)
        {
            return ResolvedCustomerResult.Failure(
                "Customer creation succeeded but no customer payload was returned.");
        }

        return ResolvedCustomerResult.Success(
            customer.Id,
            customer.FullName,
            customer.PhoneNumber);
    }

    private static bool TryBuildTransitionTargets(
        string currentStatus,
        string requestedStatus,
        out IReadOnlyList<string> transitionTargets,
        out string? error)
    {
        transitionTargets = Array.Empty<string>();
        error = null;

        var normalizedCurrent = NormalizeStatus(currentStatus);
        var normalizedRequested = NormalizeStatus(requestedStatus);

        if (normalizedCurrent is null || normalizedRequested is null)
        {
            error = "Unknown order status.";
            return false;
        }

        var currentIndex = Array.IndexOf(OrderedStatuses, normalizedCurrent);
        var requestedIndex = Array.IndexOf(OrderedStatuses, normalizedRequested);

        if (requestedIndex < currentIndex)
        {
            error = $"Cannot move order backwards from {normalizedCurrent} to {normalizedRequested}.";
            return false;
        }

        if (requestedIndex == currentIndex)
        {
            return true;
        }

        var targets = new List<string>();
        for (var index = currentIndex + 1; index <= requestedIndex; index++)
        {
            targets.Add(OrderedStatuses[index]);
        }

        transitionTargets = targets;
        return true;
    }

    private static string? NormalizeStatus(string status)
    {
        return OrderedStatuses.FirstOrDefault(
            candidate => string.Equals(candidate, status, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ResolvedCustomerResult(
        bool IsSuccess,
        string? ErrorMessage,
        Guid CustomerId,
        string FullName,
        string PhoneNumber)
    {
        public static ResolvedCustomerResult Success(Guid customerId, string fullName, string phoneNumber)
        {
            return new ResolvedCustomerResult(true, null, customerId, fullName, phoneNumber);
        }

        public static ResolvedCustomerResult Failure(string errorMessage)
        {
            return new ResolvedCustomerResult(false, errorMessage, Guid.Empty, string.Empty, string.Empty);
        }
    }
}
