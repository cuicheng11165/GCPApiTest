using System.Text.Json;

namespace GcpApi.TestCommon;

/// <summary>
/// Loads GCP test configuration from environment variables or a local testsettings.json file.
/// 
/// Environment variables (highest priority):
///   GCP_BILLING_ACCOUNT_ID    — e.g. "012345-6789AB-CDEF01"
///   GCP_PROJECT_ID            — e.g. "my-gcp-project"
///   GCP_BIGQUERY_TABLE        — e.g. "my-project.billing_dataset.gcp_billing_export_v1_xxx"
///
/// Alternatively, place a testsettings.json file anywhere from the working directory upward:
///   {
///     "BillingAccountId": "012345-6789AB-CDEF01",
///     "ProjectId": "my-gcp-project",
///     "BigQueryExportTable": "my-project.dataset.gcp_billing_export_v1_xxx"
///   }
///
/// testsettings.json is git-ignored — copy testsettings.template.json and fill in your values.
/// </summary>
public sealed class GcpTestSettings
{
    public string? BillingAccountId { get; init; }
    public string? ProjectId { get; init; }
    public string? BigQueryExportTable { get; init; }

    public static GcpTestSettings Load()
    {
        string? billingAccountId = Environment.GetEnvironmentVariable("GCP_BILLING_ACCOUNT_ID");
        string? projectId        = Environment.GetEnvironmentVariable("GCP_PROJECT_ID");
        string? bqTable          = Environment.GetEnvironmentVariable("GCP_BIGQUERY_TABLE");

        string? settingsPath = FindSettingsFile();
        if (settingsPath != null)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(settingsPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (raw != null)
            {
                billingAccountId ??= raw.GetValueOrDefault("BillingAccountId");
                projectId        ??= raw.GetValueOrDefault("ProjectId");
                bqTable          ??= raw.GetValueOrDefault("BigQueryExportTable");
            }
        }

        return new GcpTestSettings
        {
            BillingAccountId   = billingAccountId,
            ProjectId          = projectId,
            BigQueryExportTable = bqTable
        };
    }

    // ── Required accessors (throw with actionable message if not set) ─────────

    public string RequiredBillingAccountId => BillingAccountId
        ?? throw new InvalidOperationException(
            "BillingAccountId is not configured. " +
            "Set the GCP_BILLING_ACCOUNT_ID environment variable or add it to testsettings.json.");

    public string RequiredProjectId => ProjectId
        ?? throw new InvalidOperationException(
            "ProjectId is not configured. " +
            "Set the GCP_PROJECT_ID environment variable or add it to testsettings.json.");

    public string RequiredBigQueryExportTable => BigQueryExportTable
        ?? throw new InvalidOperationException(
            "BigQueryExportTable is not configured. " +
            "Set the GCP_BIGQUERY_TABLE environment variable or add it to testsettings.json.");

    // ── Search for testsettings.json walking up from cwd ─────────────────────

    private static string? FindSettingsFile()
    {
        string? dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            string candidate = Path.Combine(dir, "testsettings.json");
            if (File.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }
}
