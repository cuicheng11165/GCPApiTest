namespace GcpCostApiTests;

internal static class Program
{
    public static async Task Main()
    {
        // Set your billing account ID here (e.g. "012345-6789AB-CDEF01")
        const string billingAccountId = "your-billing-account-id";

        await Phase4BudgetDemo.RunAsync(billingAccountId);

        // To create a test budget, uncomment the line below and supply a project ID:
        // await Phase4BudgetDemo.CreateTestBudgetAsync(billingAccountId, "your-project-id");
    }
}
