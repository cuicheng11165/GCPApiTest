using Google.Cloud.Billing.V1;

namespace GcpCostApiTests;

internal static class Phase1BillingDemo
{
    public static async Task RunAsync()
    {
        Console.WriteLine("Cloud Billing API test");
        Console.WriteLine();

        CloudBillingClient client = await CloudBillingClient.CreateAsync();

        List<BillingAccount> billingAccounts = new();

        Console.WriteLine("Billing accounts:");
        await foreach (BillingAccount billingAccount in client.ListBillingAccountsAsync())
        {
            billingAccounts.Add(billingAccount);
            Console.WriteLine($"- {billingAccount.DisplayName} ({billingAccount.Name})");
        }

        if (billingAccounts.Count == 0)
        {
            Console.WriteLine("No billing accounts were found for the current credentials.");
            return;
        }

        BillingAccount firstBillingAccount = billingAccounts[0];
        Console.WriteLine();
        Console.WriteLine($"Using first billing account: {firstBillingAccount.DisplayName} ({firstBillingAccount.Name})");
        Console.WriteLine("Projects linked to this billing account:");

        await foreach (ProjectBillingInfo projectBillingInfo in client.ListProjectBillingInfoAsync(firstBillingAccount.Name))
        {
            Console.WriteLine(
                $"- {GetProjectId(projectBillingInfo.Name)} | Billing enabled: {projectBillingInfo.BillingEnabled} | Billing account: {projectBillingInfo.BillingAccountName}");
        }
    }

    private static string GetProjectId(string resourceName)
    {
        const string prefix = "projects/";
        const string suffix = "/billingInfo";

        if (resourceName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return resourceName[prefix.Length..^suffix.Length];
        }

        return resourceName;
    }
}
