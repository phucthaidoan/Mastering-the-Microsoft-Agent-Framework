using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MasteringAgentFramework.Ch08.ComplianceMcpServer;

[McpServerToolType]
public static class ComplianceTools
{
    private static readonly IReadOnlyDictionary<string, ControlCheck> _controls =
        new Dictionary<string, ControlCheck>(StringComparer.OrdinalIgnoreCase)
        {
            ["AC-2"] = new(
                "AC-2",
                "Access reviews",
                "Passing",
                "User access reviews were completed for all active systems.",
                "2026-06-18"),
            ["backup-retention"] = new(
                "backup-retention",
                "Backup retention",
                "Failing",
                "Two application backups are retained for 21 days, below the required 35-day retention window.",
                "2026-06-20"),
            ["vendor-risk"] = new(
                "vendor-risk",
                "Vendor risk review",
                "Needs attention",
                "One vendor review is due within the next seven days.",
                "2026-06-22"),
            ["audit-log"] = new(
                "audit-log",
                "Audit log export",
                "Failing",
                "The central audit log export has missed two scheduled transfers.",
                "2026-06-21")
        };

    [McpServerTool]
    [Description("Returns the current status for a compliance control.")]
    public static string CheckControlStatus(
        [Description("The control identifier, such as AC-2 or backup-retention.")]
        string controlId)
    {
        if (!_controls.TryGetValue(controlId, out ControlCheck? control))
        {
            return $"Control {controlId} was not found.";
        }

        return $"{control.Id} ({control.Name}) is {control.Status}.";
    }

    [McpServerTool]
    [Description("Lists controls that are currently failing.")]
    public static string ListFailingControls()
    {
        ControlCheck[] failing = [.. _controls.Values.Where(control => control.Status.Equals("Failing", StringComparison.OrdinalIgnoreCase))];

        if (failing.Length == 0)
        {
            return "No controls are currently failing.";
        }

        return string.Join(
            Environment.NewLine,
            failing.Select(control => $"{control.Id}: {control.Name} - {control.Detail}"));
    }

    [McpServerTool]
    [Description("Returns the description and last-checked date for a compliance control.")]
    public static string GetControlDetail(
        [Description("The control identifier, such as AC-2 or backup-retention.")]
        string controlId)
    {
        if (!_controls.TryGetValue(controlId, out ControlCheck? control))
        {
            return $"Control {controlId} was not found.";
        }

        return $"{control.Id} ({control.Name}){Environment.NewLine}Status: {control.Status}{Environment.NewLine}Last checked: {control.LastChecked}{Environment.NewLine}Detail: {control.Detail}";
    }

    private sealed record ControlCheck(
        string Id,
        string Name,
        string Status,
        string Detail,
        string LastChecked);
}
