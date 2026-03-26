using Google.Cloud.BigQuery.V2;
using Google.Cloud.Billing.Budgets.V1;
using Google.Cloud.Billing.V1;
using Xunit;

namespace GcpApi.TestCommon;

/// <summary>
/// xUnit class fixture that creates authenticated GCP clients once per test class.
/// Authentication uses Application Default Credentials (ADC).
/// Run "gcloud auth application-default login" before executing tests that call real APIs.
///
/// Client properties throw the stored credential error if ADC was not set up,
/// so pure-logic tests that never access a client pass without credentials.
/// </summary>
public sealed class GcpClientFixture : IAsyncLifetime
{
    private CloudBillingClient?  _billingClient;
    private CloudCatalogClient?  _catalogClient;
    private BudgetServiceClient? _budgetClient;
    private BigQueryClient?      _bigQueryClient;

    /// <summary>Non-null when ADC initialisation failed.</summary>
    private Exception? _initError;

    public GcpTestSettings Settings { get; } = GcpTestSettings.Load();

    public CloudBillingClient BillingClient =>
        _billingClient ?? throw (_initError ?? new InvalidOperationException("BillingClient not initialised."));

    public CloudCatalogClient CatalogClient =>
        _catalogClient ?? throw (_initError ?? new InvalidOperationException("CatalogClient not initialised."));

    public BudgetServiceClient BudgetClient =>
        _budgetClient ?? throw (_initError ?? new InvalidOperationException("BudgetClient not initialised."));

    /// <summary>Null when BigQueryExportTable is not configured.</summary>
    public BigQueryClient? BigQueryClient => _bigQueryClient;

    public async Task InitializeAsync()
    {
        try
        {
            _billingClient = await CloudBillingClient.CreateAsync();
            _catalogClient = await CloudCatalogClient.CreateAsync();
            _budgetClient  = await BudgetServiceClient.CreateAsync();

            if (Settings.BigQueryExportTable != null)
            {
                string projectId = Settings.BigQueryExportTable.Split('.')[0];
                _bigQueryClient = Google.Cloud.BigQuery.V2.BigQueryClient.Create(projectId);
            }
        }
        catch (Exception ex)
        {
            // Store the error; tests that access a client will fail with a clear message.
            // Tests that only exercise pure logic will not access any client and will pass.
            _initError = ex;
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
