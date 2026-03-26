using GcpApi.TestCommon;
using Google.Cloud.Billing.V1;

namespace Phase3.Catalog.Tests;

/// <summary>
/// Integration tests for the Cloud Catalog (Pricing) API.
/// Requires: gcloud auth application-default login
///           Cloud Billing API enabled (no special IAM role — catalog data is public)
/// </summary>
public sealed class CatalogApiTests(GcpClientFixture fixture) : IClassFixture<GcpClientFixture>
{
    // ── Service listing ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListServices_ReturnsAtLeastOneService()
    {
        Service? first = null;
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            first = service;
            break;
        }

        Assert.NotNull(first);
        Assert.StartsWith("services/", first.Name);
    }

    [Fact]
    public async Task ListServices_ContainsComputeEngine()
    {
        Service? computeEngine = null;
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            if (string.Equals(service.DisplayName, "Compute Engine", StringComparison.OrdinalIgnoreCase))
            {
                computeEngine = service;
                break;
            }
        }

        Assert.NotNull(computeEngine);
        Assert.StartsWith("services/", computeEngine!.Name);
    }

    [Fact]
    public async Task ListServices_ContainsCloudStorage()
    {
        Service? cloudStorage = null;
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            if (string.Equals(service.DisplayName, "Cloud Storage", StringComparison.OrdinalIgnoreCase))
            {
                cloudStorage = service;
                break;
            }
        }

        Assert.NotNull(cloudStorage);
    }

    // ── SKU listing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListComputeEngineSkus_ReturnsAtLeastOneSku()
    {
        string serviceId = await GetComputeEngineServiceIdAsync();

        Sku? first = null;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            first = sku;
            break;
        }

        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.Description));
    }

    [Fact]
    public async Task ListComputeEngineSkus_ContainsCoreSkuWithPricingInfo()
    {
        string serviceId = await GetComputeEngineServiceIdAsync();

        Sku? coreSku = null;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            if (sku.Description.Contains("Core", StringComparison.OrdinalIgnoreCase) &&
                sku.Category?.ResourceFamily == "Compute")
            {
                coreSku = sku;
                break;
            }
        }

        Assert.NotNull(coreSku);
        Assert.NotEmpty(coreSku!.PricingInfo);
    }

    [Fact]
    public async Task ListComputeEngineSkus_N1StandardCoreSku_HasTieredPricing()
    {
        string serviceId = await GetComputeEngineServiceIdAsync();

        Sku? n1CoreSku = null;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            if (sku.Description.Contains("N1 Standard", StringComparison.OrdinalIgnoreCase) &&
                sku.Description.Contains("Core",         StringComparison.OrdinalIgnoreCase) &&
                sku.Description.Contains("Americas",     StringComparison.OrdinalIgnoreCase))
            {
                n1CoreSku = sku;
                break;
            }
        }

        Skip.If(n1CoreSku is null, "N1 Standard Core Americas SKU not found — catalog may have changed.");

        Assert.NotEmpty(n1CoreSku!.PricingInfo);
        Assert.NotEmpty(n1CoreSku.PricingInfo[0].PricingExpression.TieredRates);

        // Verify first tier price is a positive amount.
        PricingExpression.Types.TierRate firstTier = n1CoreSku.PricingInfo[0].PricingExpression.TieredRates[0];
        decimal unitPrice = firstTier.UnitPrice.Units + (firstTier.UnitPrice.Nanos / 1_000_000_000m);
        Assert.True(unitPrice > 0, "N1 Standard Core unit price should be greater than zero");
    }

    // ── Service structure validation ──────────────────────────────────────────

    [Fact]
    public async Task ListServices_FirstTen_AllHaveValidNameAndServiceId()
    {
        int count = 0;
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            Assert.StartsWith("services/", service.Name, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(service.ServiceId));
            Assert.False(string.IsNullOrWhiteSpace(service.DisplayName));

            if (++count >= 10) break;
        }
        Assert.True(count >= 1, "Expected at least one service.");
    }

    [Fact]
    public async Task ListServices_ContainsBigQuery()
    {
        Service? bigQuery = null;
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            if (string.Equals(service.DisplayName, "BigQuery", StringComparison.OrdinalIgnoreCase))
            {
                bigQuery = service;
                break;
            }
        }
        Assert.NotNull(bigQuery);
        Assert.StartsWith("services/", bigQuery!.Name, StringComparison.Ordinal);
    }

    // ── SKU structure validation ───────────────────────────────────────────────

    [Fact]
    public async Task ComputeEngineSkus_FirstFifty_AllHaveNonEmptyDescription()
    {
        string serviceId = await GetServiceIdAsync("Compute Engine");
        int count = 0;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            Assert.False(string.IsNullOrWhiteSpace(sku.Description));
            if (++count >= 50) break;
        }
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task ComputeEngineSkus_FirstFifty_AllHaveCategoryWithResourceFamily()
    {
        string serviceId = await GetServiceIdAsync("Compute Engine");
        int count = 0;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            Assert.NotNull(sku.Category);
            Assert.False(string.IsNullOrWhiteSpace(sku.Category!.ResourceFamily));
            Assert.False(string.IsNullOrWhiteSpace(sku.Category.ResourceGroup));
            if (++count >= 50) break;
        }
        Assert.True(count >= 1);
    }

    [Fact]
    public async Task ComputeEngineSkus_ContainsN1StandardRamSku()
    {
        string serviceId = await GetServiceIdAsync("Compute Engine");

        Sku? ramSku = null;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            if (sku.Description.Contains("N1 Standard",  StringComparison.OrdinalIgnoreCase) &&
                sku.Description.Contains("Ram",           StringComparison.OrdinalIgnoreCase))
            {
                ramSku = sku;
                break;
            }
        }

        Skip.If(ramSku is null, "N1 Standard RAM SKU not found — catalog may have changed.");
        Assert.NotEmpty(ramSku!.PricingInfo);
    }

    [Fact]
    public async Task ComputeEngineSkus_WithPricingInfo_HaveNonEmptyUsageUnit()
    {
        string serviceId = await GetServiceIdAsync("Compute Engine");
        int checked_ = 0;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            foreach (PricingInfo pi in sku.PricingInfo)
            {
                if (pi.PricingExpression is not null)
                {
                    Assert.False(string.IsNullOrWhiteSpace(pi.PricingExpression.UsageUnit));
                    if (++checked_ >= 20) break;
                }
            }
            if (checked_ >= 20) break;
        }
    }

    // ── Cloud Storage SKU coverage ─────────────────────────────────────────────

    [Fact]
    public async Task CloudStorageSkus_ContainsStorageOperationsSku()
    {
        string serviceId = await GetServiceIdAsync("Cloud Storage");

        Sku? opsSku = null;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            if (sku.Description.Contains("Storage", StringComparison.OrdinalIgnoreCase))
            {
                opsSku = sku;
                break;
            }
        }

        Assert.NotNull(opsSku);
        Assert.NotEmpty(opsSku!.PricingInfo);
    }

    [Fact]
    public async Task CloudStorageSkus_FirstTen_AllHaveServiceRegionsOrGlobal()
    {
        string serviceId = await GetServiceIdAsync("Cloud Storage");
        int count = 0;
        await foreach (Sku sku in fixture.CatalogClient.ListSkusAsync(serviceId))
        {
            // Each SKU must declare at least one region or the special 'global' region.
            Assert.NotEmpty(sku.ServiceRegions);
            if (++count >= 10) break;
        }
        Assert.True(count >= 1);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private async Task<string> GetComputeEngineServiceIdAsync() =>
        await GetServiceIdAsync("Compute Engine");

    private async Task<string> GetServiceIdAsync(string displayName)
    {
        await foreach (Service service in fixture.CatalogClient.ListServicesAsync())
        {
            if (string.Equals(service.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                return service.Name;
        }
        throw new InvalidOperationException($"'{displayName}' service not found in catalog.");
    }
}
