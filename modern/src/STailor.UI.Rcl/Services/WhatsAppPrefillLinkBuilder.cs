using System.Globalization;
using System.Text;
using STailor.Shared.Contracts.Orders;
using STailor.Shared.Contracts.Reports;
using STailor.UI.Rcl.Models;

namespace STailor.UI.Rcl.Services;

public static class WhatsAppPrefillLinkBuilder
{
    public static string BuildFromOrderResult(OrderWizardSubmissionResult result, bool useDueBalanceTemplate)
    {
        if (result.OrderId is Guid orderId && orderId != Guid.Empty)
        {
            return BuildSingleOrderLink(useDueBalanceTemplate, orderId, message: null);
        }

        return BuildLink(
            useDueBalanceTemplate,
            result.PhoneNumber,
            result.CustomerName,
            result.OrderId?.ToString(),
            result.FinalStatus,
            result.DueAtUtc,
            result.BalanceDue,
            message: null);
    }

    public static string BuildFromReminderCandidate(OrderReminderDto candidate, bool useDueBalanceTemplate)
    {
        return BuildSingleOrderLink(useDueBalanceTemplate, candidate.OrderId, message: null);
    }

    public static string BuildFromWorklistItem(OrderWorklistItemDto item, bool useDueBalanceTemplate)
    {
        return BuildSingleOrderLink(useDueBalanceTemplate, item.OrderId, message: null);
    }

    public static string BuildFromWorklistGroup(OrdersBoardWorklistGroup group, bool useDueBalanceTemplate)
    {
        ArgumentNullException.ThrowIfNull(group);

        var earliestDue = group.Items.Count == 0
            ? (DateTimeOffset?)null
            : group.Items.Min(item => item.DueAtUtc);

        var totalBalance = group.Items.Sum(item => item.BalanceDue);
        var message = BuildGroupMessage(group, useDueBalanceTemplate);

        return BuildLink(
            useDueBalanceTemplate,
            phoneNumber: null,
            customerName: $"Group: {group.Title}",
            orderId: null,
            status: group.Title,
            dueAtUtc: earliestDue,
            balanceDue: totalBalance,
            message: message);
    }

    public static string BuildFromRetryCandidatesGroup(
        string groupTitle,
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> candidates)
    {
        if (string.IsNullOrWhiteSpace(groupTitle))
        {
            throw new ArgumentException("Group title is required.", nameof(groupTitle));
        }

        ArgumentNullException.ThrowIfNull(candidates);

        var sortedCandidates = candidates
            .OrderBy(candidate => GetStatusOrder(candidate.TargetStatus))
            .ThenBy(candidate => OrdersBoardFilterStateMapper.NormalizeStatus(candidate.TargetStatus), StringComparer.Ordinal)
            .ThenBy(candidate => NormalizeCustomerName(candidate.CustomerName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.OrderId)
            .ToList();

        return BuildLink(
            useDueBalanceTemplate: false,
            phoneNumber: null,
            customerName: $"Retryable: {groupTitle}",
            orderId: null,
            status: "Retryable",
            dueAtUtc: null,
            balanceDue: null,
            message: BuildRetryableGroupMessage(groupTitle.Trim(), sortedCandidates));
    }

    public static string BuildFromOutstandingDueItem(OutstandingDueItemDto item, bool useDueBalanceTemplate = true)
    {
        ArgumentNullException.ThrowIfNull(item);

        return BuildSingleOrderLink(useDueBalanceTemplate, item.OrderId, message: null);
    }

    private static string BuildSingleOrderLink(bool useDueBalanceTemplate, Guid orderId, string? message)
    {
        return BuildLink(
            useDueBalanceTemplate,
            phoneNumber: null,
            customerName: null,
            orderId: orderId.ToString(),
            status: null,
            dueAtUtc: null,
            balanceDue: null,
            message: message);
    }

    private static string BuildLink(
        bool useDueBalanceTemplate,
        string? phoneNumber,
        string? customerName,
        string? orderId,
        string? status,
        DateTimeOffset? dueAtUtc,
        decimal? balanceDue,
        string? message)
    {
        var targetRoute = useDueBalanceTemplate
            ? "/communications/reminder"
            : "/communications/whatsapp";

        var queryParts = new List<string>();
        AppendQueryPart(queryParts, "phone", phoneNumber);
        AppendQueryPart(queryParts, "customerName", customerName);
        AppendQueryPart(queryParts, "orderId", orderId);
        AppendQueryPart(queryParts, "status", status);

        if (dueAtUtc is not null)
        {
            AppendQueryPart(
                queryParts,
                "dueAtUtc",
                dueAtUtc.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        if (balanceDue is not null)
        {
            AppendQueryPart(
                queryParts,
                "balanceDue",
                balanceDue.Value.ToString(CultureInfo.InvariantCulture));
        }

        AppendQueryPart(queryParts, "message", message);

        if (useDueBalanceTemplate)
        {
            AppendQueryPart(queryParts, "template", "balance");
        }

        if (queryParts.Count == 0)
        {
            return targetRoute;
        }

        var builder = new StringBuilder(targetRoute);
        builder.Append('?');
        builder.Append(string.Join("&", queryParts));
        return builder.ToString();
    }

    private static string BuildGroupMessage(OrdersBoardWorklistGroup group, bool useDueBalanceTemplate)
    {
        var intro = useDueBalanceTemplate
            ? $"Selam, SINYX Tailor Management balance reminders for {group.Title}."
            : $"Selam, SINYX Tailor Management status updates for {group.Title}.";

        var entries = group.Items
            .Take(8)
            .Select(item =>
                $"- {item.CustomerName} ({item.PhoneNumber}), due {item.DueAtUtc:yyyy-MM-dd}, balance {item.BalanceDue:0.##}")
            .ToList();

        if (group.Items.Count > entries.Count)
        {
            entries.Add($"- ...and {group.Items.Count - entries.Count} more order(s).");
        }

        var lines = new List<string> { intro };
        lines.AddRange(entries);

        return string.Join("\n", lines);
    }

    private static string BuildRetryableGroupMessage(
        string groupTitle,
        IReadOnlyList<OrdersBoardBulkAdvanceRetryCandidate> candidates)
    {
        var lines = new List<string>
        {
            $"Selam, SINYX Tailor Management retryable updates for {groupTitle}.",
        };

        if (candidates.Count == 0)
        {
            lines.Add("- No retryable orders right now.");
            return string.Join("\n", lines);
        }

        var entries = candidates
            .Take(12)
            .Select(candidate =>
                $"- {NormalizeCustomerName(candidate.CustomerName)} ({FormatOrderToken(candidate.OrderId)}) -> {FormatStatusLabel(candidate.TargetStatus)}")
            .ToList();

        lines.AddRange(entries);

        if (candidates.Count > entries.Count)
        {
            lines.Add($"- ...and {candidates.Count - entries.Count} more retryable order(s).");
        }

        return string.Join("\n", lines);
    }

    private static string FormatOrderToken(Guid orderId)
    {
        var value = orderId.ToString("N");
        return $"#{value[..8]}";
    }

    private static string FormatStatusLabel(string status)
    {
        var normalizedStatus = OrdersBoardFilterStateMapper.NormalizeStatus(status);

        return normalizedStatus switch
        {
            "InProgress" => "In Progress",
            "TrialFitting" => "Trial/Fitting",
            _ => normalizedStatus,
        };
    }

    private static string NormalizeCustomerName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown Customer"
            : value.Trim();
    }

    private static int GetStatusOrder(string status)
    {
        var normalizedStatus = OrdersBoardFilterStateMapper.NormalizeStatus(status);

        return normalizedStatus switch
        {
            "InProgress" => 0,
            "TrialFitting" => 1,
            "Rework" => 2,
            "Ready" => 3,
            "Delivered" => 4,
            _ => 9,
        };
    }

    private static void AppendQueryPart(List<string> queryParts, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
    }
}
