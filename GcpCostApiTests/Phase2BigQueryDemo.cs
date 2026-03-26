using Google.Cloud.BigQuery.V2;

namespace GcpCostApiTests;

internal static class Phase2BigQueryDemo
{
    public static async Task RunAsync()
    {
        const string billingExportTable = "your-project-id.your_dataset.gcp_billing_export_v1_xxx";

        string projectId = GetProjectId(billingExportTable);
        BigQueryClient client = BigQueryClient.Create(projectId);

        string query = $"""
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

        Console.WriteLine($"Querying billing export table: {billingExportTable}");
        Console.WriteLine();

        BigQueryJob queryJob = await client.CreateQueryJobAsync(
            sql: query,
            parameters: null,
            options: new QueryOptions
            {
                UseQueryCache = false
            });

        BigQueryJob completedJob = await queryJob.PollUntilCompletedAsync();
        BigQueryResults results = await client.GetQueryResultsAsync(completedJob.Reference);

        Console.WriteLine("Current month cost by service:");

        foreach (BigQueryRow row in results)
        {
            string serviceDescription = row["service_description"]?.ToString() ?? "(unknown service)";
            decimal totalCost = Convert.ToDecimal(row["total_cost"]);
            Console.WriteLine($"- {serviceDescription}: {totalCost:F2}");
        }
    }

    private static string GetProjectId(string billingExportTable)
    {
        string[] parts = billingExportTable.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new ArgumentException(
                "The billing export table name must be in the format 'project.dataset.table'.",
                nameof(billingExportTable));
        }

        return parts[0];
    }
}
