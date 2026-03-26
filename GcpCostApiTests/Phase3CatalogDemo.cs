using Google.Cloud.Billing.V1;

namespace GcpCostApiTests;

internal static class Phase3CatalogDemo
{
    public static async Task RunAsync()
    {
        CloudCatalogClient client = await CloudCatalogClient.CreateAsync();

        Console.WriteLine("Finding Compute Engine service...");

        Service? computeEngineService = null;
        await foreach (Service service in client.ListServicesAsync())
        {
            if (string.Equals(service.DisplayName, "Compute Engine", StringComparison.OrdinalIgnoreCase))
            {
                computeEngineService = service;
                break;
            }
        }

        if (computeEngineService is null)
        {
            Console.WriteLine("Compute Engine service was not found.");
            return;
        }

        Console.WriteLine($"Compute Engine service ID: {computeEngineService.Name}");
        Console.WriteLine();
        Console.WriteLine("Fetching SKUs...");

        List<Sku> skus = new();
        await foreach (Sku sku in client.ListSkusAsync(computeEngineService.Name))
        {
            skus.Add(sku);
        }

        Sku? selectedSku = skus
            .Where(sku => ContainsIgnoreCase(sku.Description, "N1 Standard"))
            .Where(sku => ContainsIgnoreCase(sku.Description, "Core"))
            .Where(sku => ContainsIgnoreCase(sku.Description, "Americas"))
            .OrderBy(sku => sku.Description)
            .FirstOrDefault()
            ?? skus
                .Where(sku => ContainsIgnoreCase(sku.Description, "Core"))
                .Where(sku => sku.Category?.ResourceFamily == "Compute")
                .OrderBy(sku => sku.Description)
                .FirstOrDefault();

        if (selectedSku is null)
        {
            Console.WriteLine("No matching Compute Engine core SKU was found.");
            return;
        }

        Console.WriteLine($"Selected SKU: {selectedSku.Description}");
        Console.WriteLine($"SKU ID: {selectedSku.Name}");
        Console.WriteLine();
        Console.WriteLine("Pricing tiers:");

        PricingInfo? pricingInfo = selectedSku.PricingInfo.FirstOrDefault();
        if (pricingInfo is null)
        {
            Console.WriteLine("No pricing information was returned for the selected SKU.");
            return;
        }

        foreach (PricingExpression.Types.TierRate tierRate in pricingInfo.PricingExpression.TieredRates)
        {
            decimal usdAmount = ConvertMoneyToDecimal(tierRate.UnitPrice);
            Console.WriteLine($"- Start usage amount: {tierRate.StartUsageAmount}, unit price: ${usdAmount:F6}");
        }
    }

    private static bool ContainsIgnoreCase(string? value, string text) =>
        value?.Contains(text, StringComparison.OrdinalIgnoreCase) == true;

    private static decimal ConvertMoneyToDecimal(Google.Type.Money money) =>
        money.Units + (money.Nanos / 1_000_000_000m);
}
