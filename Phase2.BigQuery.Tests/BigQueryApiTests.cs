using GcpApi.TestCommon;
using Google.Cloud.BigQuery.V2;

namespace Phase2.BigQuery.Tests;

/// <summary>
/// Integration tests for the BigQuery Billing Export API.
/// Requires: gcloud auth application-default login
///           BigQuery Data Viewer role on the export dataset
///           GCP_BIGQUERY_TABLE (or BigQueryExportTable in testsettings.json) to run query tests.
/// </summary>
public sealed class BigQueryApiTests(GcpClientFixture fixture) : IClassFixture<GcpClientFixture>
{
    // ── Pure-logic helpers (no GCP credentials needed) ────────────────────────

    [Theory]
    [InlineData("my-project.my_dataset.gcp_billing_export_v1_ABC",  "my-project")]
    [InlineData("org-project.billing.gcp_billing_export_v1_XYZ",    "org-project")]
    public void ParseProjectId_WithValidTable_ReturnsProjectPart(string table, string expectedProjectId)
    {
        string[] parts = table.Split('.');
        Assert.Equal(expectedProjectId, parts[0]);
    }

    [Fact]
    public void BuildCostQuery_ContainsAllRequiredClauses()
    {
        const string table = "my-project.my_dataset.gcp_billing_export_v1_ABC";
        string query = BuildCostQuery(table);

        Assert.Contains($"`{table}`",                        query);
        Assert.Contains("service.description",               query);
        Assert.Contains("SUM(cost)",                         query);
        Assert.Contains("UNNEST(credits)",                   query);
        Assert.Contains("DATE_TRUNC(CURRENT_DATE(), MONTH)", query);
        Assert.Contains("GROUP BY service_description",      query);
        Assert.Contains("ORDER BY total_cost DESC",          query);
    }

    // ── Real API tests (require BigQuery table config) ────────────────────────

    [SkippableFact]
    public async Task QueryCurrentMonthCosts_ReturnsResultsWithoutError()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null,
            "Set GCP_BIGQUERY_TABLE or BigQueryExportTable in testsettings.json to run this test.");
        Skip.If(fixture.BigQueryClient is null,
            "BigQueryClient could not be initialised — check your export table name.");

        string table = fixture.Settings.BigQueryExportTable!;
        string query = BuildCostQuery(table);

        BigQueryJob job = await fixture.BigQueryClient!.CreateQueryJobAsync(
            sql: query,
            parameters: null,
            options: new QueryOptions { UseQueryCache = false });

        BigQueryJob completed = await job.PollUntilCompletedAsync();
        BigQueryResults results = await fixture.BigQueryClient.GetQueryResultsAsync(completed.Reference);

        // Verify that rows have the expected columns and parseable values.
        foreach (BigQueryRow row in results)
        {
            Assert.NotNull(row["service_description"]);
            Assert.True(decimal.TryParse(row["total_cost"]?.ToString(), out _),
                "total_cost must be parseable as decimal");
        }
    }

    [SkippableFact]
    public async Task QueryCurrentMonthCosts_ServiceDescriptions_AreNotEmpty()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null,
            "Set GCP_BIGQUERY_TABLE or BigQueryExportTable in testsettings.json to run this test.");
        Skip.If(fixture.BigQueryClient is null,
            "BigQueryClient could not be initialised.");

        string table = fixture.Settings.BigQueryExportTable!;
        BigQueryJob job = await fixture.BigQueryClient!.CreateQueryJobAsync(
            sql: BuildCostQuery(table), parameters: null,
            options: new QueryOptions { UseQueryCache = false });

        BigQueryJob completed = await job.PollUntilCompletedAsync();
        BigQueryResults results = await fixture.BigQueryClient.GetQueryResultsAsync(completed.Reference);

        foreach (BigQueryRow row in results)
            Assert.False(string.IsNullOrWhiteSpace(row["service_description"]?.ToString()));
    }

    // ── Dataset operations ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ListDatasets_ForExportProject_ReturnsAtLeastOneDataset()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        BigQueryDataset? first = null;
        await foreach (BigQueryDataset dataset in fixture.BigQueryClient!.ListDatasetsAsync())
        {
            first = dataset;
            break;
        }

        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.Reference.DatasetId));
        Assert.False(string.IsNullOrWhiteSpace(first.Reference.ProjectId));
    }

    [SkippableFact]
    public async Task GetDataset_ExportDataset_ReturnsDatasetWithMatchingId()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string datasetId   = fixture.Settings.BigQueryExportTable!.Split('.')[1];
        BigQueryDataset ds = await fixture.BigQueryClient!.GetDatasetAsync(datasetId);

        Assert.Equal(datasetId, ds.Reference.DatasetId);
    }

    [SkippableFact]
    public async Task GetDataset_ExportDataset_HasCreationTime()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string datasetId   = fixture.Settings.BigQueryExportTable!.Split('.')[1];
        BigQueryDataset ds = await fixture.BigQueryClient!.GetDatasetAsync(datasetId);

        Assert.NotNull(ds.Resource.CreationTime);
    }

    // ── Table operations ──────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ListTables_InExportDataset_ContainsExportTable()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string datasetId = fixture.Settings.BigQueryExportTable!.Split('.')[1];
        string tableId   = fixture.Settings.BigQueryExportTable!.Split('.')[2];

        var tableIds = new List<string>();
        await foreach (BigQueryTable table in fixture.BigQueryClient!.ListTablesAsync(datasetId))
            tableIds.Add(table.Reference.TableId);

        Assert.Contains(tableIds,
            id => id.Equals(tableId, StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task ListTables_InExportDataset_AllTablesHaveValidReference()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string datasetId = fixture.Settings.BigQueryExportTable!.Split('.')[1];

        await foreach (BigQueryTable table in fixture.BigQueryClient!.ListTablesAsync(datasetId))
        {
            Assert.False(string.IsNullOrWhiteSpace(table.Reference.TableId));
            Assert.False(string.IsNullOrWhiteSpace(table.Reference.DatasetId));
            Assert.False(string.IsNullOrWhiteSpace(table.Reference.ProjectId));
        }
    }

    [SkippableFact]
    public async Task GetTable_ExportTable_HasSchema()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string datasetId   = fixture.Settings.BigQueryExportTable!.Split('.')[1];
        string tableId     = fixture.Settings.BigQueryExportTable!.Split('.')[2];
        BigQueryTable table = await fixture.BigQueryClient!.GetTableAsync(datasetId, tableId);

        Assert.NotNull(table.Schema);
        Assert.NotEmpty(table.Schema.Fields);

        // The billing export table must contain 'cost' and 'service' fields.
        var fieldNames = table.Schema.Fields.Select(f => f.Name).ToList();
        Assert.Contains("cost",    fieldNames);
        Assert.Contains("service", fieldNames);
    }

    // ── Additional queries ────────────────────────────────────────────────────

    [SkippableFact]
    public async Task QueryCostGroupedByProject_ReturnsProjectIds()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string table = fixture.Settings.BigQueryExportTable!;
        string query = $"""
            SELECT
              project.id                                                              AS project_id,
              ROUND(SUM(cost) + SUM(IFNULL((SELECT SUM(c.amount) FROM UNNEST(credits) AS c), 0)), 2)
                                                                                     AS total_cost
            FROM `{table}`
            WHERE DATE(usage_start_time) >= DATE_SUB(CURRENT_DATE(), INTERVAL 30 DAY)
            GROUP BY project_id
            ORDER BY total_cost DESC
            LIMIT 20
            """;

        BigQueryJob job = await fixture.BigQueryClient!.CreateQueryJobAsync(
            sql: query, parameters: null, options: new QueryOptions { UseQueryCache = false });
        BigQueryJob     completed = await job.PollUntilCompletedAsync();
        BigQueryResults results   = await fixture.BigQueryClient.GetQueryResultsAsync(completed.Reference);

        foreach (BigQueryRow row in results)
        {
            Assert.True(decimal.TryParse(row["total_cost"]?.ToString(), out _),
                "total_cost must be parseable as decimal");
        }
    }

    [SkippableFact]
    public async Task QueryCostUsingParameterisedDate_ReturnsResults()
    {
        Skip.If(fixture.Settings.BigQueryExportTable is null, "BigQueryExportTable not configured.");
        Skip.If(fixture.BigQueryClient is null, "BigQueryClient could not be initialised.");

        string table = fixture.Settings.BigQueryExportTable!;
        string query = $"""
            SELECT
              service.description AS service_description,
              ROUND(SUM(cost), 2) AS total_cost
            FROM `{table}`
            WHERE DATE(usage_start_time) >= @start_date
            GROUP BY service_description
            ORDER BY total_cost DESC
            LIMIT 10
            """;

        var parameters = new[]
        {
            new BigQueryParameter("start_date", BigQueryDbType.Date,
                DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"))
        };

        BigQueryJob job = await fixture.BigQueryClient!.CreateQueryJobAsync(
            sql: query, parameters: parameters, options: new QueryOptions { UseQueryCache = false });
        BigQueryJob     completed = await job.PollUntilCompletedAsync();
        BigQueryResults results   = await fixture.BigQueryClient.GetQueryResultsAsync(completed.Reference);

        foreach (BigQueryRow row in results)
        {
            Assert.NotNull(row["service_description"]);
            Assert.True(decimal.TryParse(row["total_cost"]?.ToString(), out _));
        }
    }

    // ── Query builder (shared with demo code) ─────────────────────────────────

    private static string BuildCostQuery(string billingExportTable) => $"""
        SELECT
          service.description AS service_description,
          ROUND(
            SUM(cost) + SUM(IFNULL((SELECT SUM(c.amount) FROM UNNEST(credits) AS c), 0)),
            2
          ) AS total_cost
        FROM `{billingExportTable}`
        WHERE DATE(usage_start_time) >= DATE_TRUNC(CURRENT_DATE(), MONTH)
          AND DATE(usage_start_time) < DATE_ADD(CURRENT_DATE(), INTERVAL 1 DAY)
        GROUP BY service_description
        ORDER BY total_cost DESC
        """;
}
