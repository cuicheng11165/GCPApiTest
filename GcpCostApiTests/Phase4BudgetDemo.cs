using Google.Cloud.Billing.Budgets.V1;
using Google.Type;

namespace GcpCostApiTests;

internal static class Phase4BudgetDemo
{
    /// <summary>
    /// Lists all budgets for the given billing account and prints their names and amounts.
    /// Requires the Billing Account Administrator IAM role.
    /// </summary>
    /// <param name="billingAccountId">The billing account ID (e.g. "012345-6789AB-CDEF01").</param>
    public static async Task RunAsync(string billingAccountId)
    {
        BudgetServiceClient client = await BudgetServiceClient.CreateAsync();

        string billingAccountName = $"billingAccounts/{billingAccountId}";

        Console.WriteLine($"Listing budgets for billing account: {billingAccountName}");
        Console.WriteLine();

        int count = 0;
        await foreach (Budget budget in client.ListBudgetsAsync(billingAccountName))
        {
            count++;
            decimal budgetAmount = budget.Amount?.SpecifiedAmount is Money money
                ? money.Units + (money.Nanos / 1_000_000_000m)
                : 0m;

            Console.WriteLine($"Budget #{count}");
            Console.WriteLine($"  Name:         {budget.Name}");
            Console.WriteLine($"  Display name: {budget.DisplayName}");
            Console.WriteLine($"  Amount:       ${budgetAmount:F2} {budget.Amount?.SpecifiedAmount?.CurrencyCode}");
            Console.WriteLine($"  Thresholds:   {string.Join(", ", budget.ThresholdRules.Select(r => $"{r.ThresholdPercent * 100:F0}% ({r.SpendBasis})"))}");
            Console.WriteLine();
        }

        if (count == 0)
        {
            Console.WriteLine("No budgets found for this billing account.");
        }
    }

    /// <summary>
    /// Creates a $50 USD test budget for a single project with 50%, 90%, and 100% alert thresholds.
    /// This method is intentionally NOT called from Main — call it explicitly when ready to test.
    /// Requires the Billing Account Administrator IAM role.
    /// </summary>
    /// <param name="billingAccountId">The billing account ID (e.g. "012345-6789AB-CDEF01").</param>
    /// <param name="projectId">The GCP project ID to scope the budget to (e.g. "my-project-id").</param>
    public static async Task CreateTestBudgetAsync(string billingAccountId, string projectId)
    {
        BudgetServiceClient client = await BudgetServiceClient.CreateAsync();

        string billingAccountName = $"billingAccounts/{billingAccountId}";

        Budget budget = new Budget
        {
            DisplayName = $"Test budget - {projectId}",
            BudgetFilter = new Filter
            {
                Projects = { $"projects/{projectId}" }
            },
            Amount = new BudgetAmount
            {
                SpecifiedAmount = new Money
                {
                    CurrencyCode = "USD",
                    Units = 50,
                    Nanos = 0
                }
            },
            ThresholdRules =
            {
                new ThresholdRule { ThresholdPercent = 0.50, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend },
                new ThresholdRule { ThresholdPercent = 0.90, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend },
                new ThresholdRule { ThresholdPercent = 1.00, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend }
            }
        };

        Budget created = await client.CreateBudgetAsync(billingAccountName, budget);

        Console.WriteLine($"Budget created: {created.Name}");
        Console.WriteLine($"  Display name: {created.DisplayName}");
        Console.WriteLine($"  Amount: ${created.Amount?.SpecifiedAmount?.Units:F2} {created.Amount?.SpecifiedAmount?.CurrencyCode}");
        Console.WriteLine($"  Thresholds: {string.Join(", ", created.ThresholdRules.Select(r => $"{r.ThresholdPercent * 100:F0}%"))}");
    }
}
