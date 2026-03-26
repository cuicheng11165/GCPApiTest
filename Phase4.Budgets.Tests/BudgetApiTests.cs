using GcpApi.TestCommon;
using Google.Cloud.Billing.Budgets.V1;
using Google.Type;

namespace Phase4.Budgets.Tests;

/// <summary>
/// Integration tests for the Cloud Billing Budgets API.
/// Requires: gcloud auth application-default login
///           Billing Account Administrator IAM role
///           GCP_BILLING_ACCOUNT_ID (or BillingAccountId in testsettings.json) for account-scoped tests.
/// </summary>
public sealed class BudgetApiTests(GcpClientFixture fixture) : IClassFixture<GcpClientFixture>
{
    // ── Budget listing ────────────────────────────────────────────────────────

    [SkippableFact]
    public async Task ListBudgets_WithConfiguredBillingAccount_ReturnsWithoutError()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        var budgets = new List<Budget>();

        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
            budgets.Add(budget);

        // A billing account may legitimately have zero budgets; we just verify the call succeeds.
        Assert.All(budgets, b => Assert.False(string.IsNullOrWhiteSpace(b.Name)));
    }

    [SkippableFact]
    public async Task ListBudgets_EachBudget_HasValidAmountOrLastPeriodDemand()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";

        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
        {
            // A budget must have either a specified amount or use last-period demand.
            Assert.True(
                budget.Amount?.SpecifiedAmount != null || budget.Amount?.LastPeriodAmount != null,
                $"Budget '{budget.Name}' has no recognisable amount type.");
        }
    }

    // ── Budget object construction (no API call needed) ───────────────────────

    [Fact]
    public void BuildTestBudget_HasCorrect50DollarUsdAmount()
    {
        Budget budget = BuildTestBudget("my-project");

        Assert.Equal(50L,   budget.Amount.SpecifiedAmount.Units);
        Assert.Equal("USD", budget.Amount.SpecifiedAmount.CurrencyCode);
    }

    [Fact]
    public void BuildTestBudget_HasExactlyThreeThresholdRules()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.Equal(3, budget.ThresholdRules.Count);
    }

    [Fact]
    public void BuildTestBudget_ThresholdPercentsAre50_90_100()
    {
        Budget budget = BuildTestBudget("my-project");
        double[] percents = budget.ThresholdRules.Select(r => r.ThresholdPercent).OrderBy(p => p).ToArray();
        Assert.Equal([0.50, 0.90, 1.00], percents);
    }

    [Fact]
    public void BuildTestBudget_AllThresholdsUseCurrentSpend()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.All(budget.ThresholdRules,
            r => Assert.Equal(ThresholdRule.Types.Basis.CurrentSpend, r.SpendBasis));
    }

    [Fact]
    public void BuildTestBudget_FilterContainsCorrectProject()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.Contains("projects/my-project", budget.BudgetFilter.Projects);
    }

    [Fact]
    public void BuildTestBudget_DisplayNameContainsProjectId()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.Contains("my-project", budget.DisplayName);
    }

    // ── GetBudget by name ─────────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetBudget_FirstListedBudget_MatchesListData()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        Budget? firstFromList = null;

        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
        {
            firstFromList = budget;
            break;
        }

        Skip.If(firstFromList is null, "No budgets exist on this billing account — nothing to Get.");

        Budget fetched = await fixture.BudgetClient.GetBudgetAsync(firstFromList!.Name);

        Assert.Equal(firstFromList.Name,        fetched.Name);
        Assert.Equal(firstFromList.DisplayName, fetched.DisplayName);
    }

    // ── Budget name and parent validation ─────────────────────────────────────

    [SkippableFact]
    public async Task ListBudgets_AllBudgets_NamesStartWithBillingAccountPrefix()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
        {
            Assert.StartsWith(parent + "/budgets/", budget.Name, StringComparison.Ordinal);
        }
    }

    [SkippableFact]
    public async Task ListBudgets_AllThresholdRules_PercentsAreInValidRange()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
        {
            foreach (ThresholdRule rule in budget.ThresholdRules)
            {
                Assert.True(rule.ThresholdPercent > 0,
                    $"Budget '{budget.Name}': threshold percent must be > 0.");
                Assert.True(rule.ThresholdPercent <= 2.0,
                    $"Budget '{budget.Name}': threshold percent {rule.ThresholdPercent} should be <= 2.0 (200%).");
            }
        }
    }

    [SkippableFact]
    public async Task ListBudgets_BudgetsWithSpecifiedAmount_HaveKnownCurrencyCode()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string parent = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        await foreach (Budget budget in fixture.BudgetClient.ListBudgetsAsync(parent))
        {
            Money? amount = budget.Amount?.SpecifiedAmount;
            if (amount is null) continue;

            Assert.False(string.IsNullOrWhiteSpace(amount.CurrencyCode),
                $"Budget '{budget.Name}': SpecifiedAmount must have a currency code.");
            Assert.True(amount.Units >= 0,
                $"Budget '{budget.Name}': SpecifiedAmount.Units must be non-negative.");
        }
    }

    // ── Pure-logic: BuildTestBudget additional guards ─────────────────────────

    [Fact]
    public void BuildTestBudget_SpecifiedAmountUnitsArePositive()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.True(budget.Amount.SpecifiedAmount.Units > 0);
    }

    [Fact]
    public void BuildTestBudget_AllThresholdPercentsArePositive()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.All(budget.ThresholdRules, r => Assert.True(r.ThresholdPercent > 0));
    }

    [Fact]
    public void BuildTestBudget_AllThresholdPercentsAreWithin200Percent()
    {
        Budget budget = BuildTestBudget("my-project");
        Assert.All(budget.ThresholdRules, r => Assert.True(r.ThresholdPercent <= 2.0));
    }

    // ── Budget creation (destructive — disabled by default) ───────────────────

    // To test budget creation: change [Fact(Skip=...)] to [SkippableFact] and ensure
    // the configured billing account has the Billing Account Administrator role.
    [Fact(Skip = "Destructive test — enable manually and supply BillingAccountId + ProjectId.")]
    public async Task CreateTestBudget_CreatesSuccessfully_ThenDeletes()
    {
        string parent    = $"billingAccounts/{fixture.Settings.RequiredBillingAccountId}";
        string projectId = fixture.Settings.RequiredProjectId;

        Budget created = await fixture.BudgetClient.CreateBudgetAsync(
            new CreateBudgetRequest { Parent = parent, Budget = BuildTestBudget(projectId) });

        try
        {
            Assert.False(string.IsNullOrWhiteSpace(created.Name));
            Assert.Equal(50L, created.Amount.SpecifiedAmount.Units);
        }
        finally
        {
            // Always clean up the test budget.
            await fixture.BudgetClient.DeleteBudgetAsync(new DeleteBudgetRequest { Name = created.Name });
        }
    }

    // ── Budget builder (mirrors Phase4BudgetDemo.CreateTestBudgetAsync) ───────

    private static Budget BuildTestBudget(string projectId) => new Budget
    {
        DisplayName = $"Test budget - {projectId}",
        BudgetFilter = new Filter { Projects = { $"projects/{projectId}" } },
        Amount = new BudgetAmount
        {
            SpecifiedAmount = new Money { CurrencyCode = "USD", Units = 50, Nanos = 0 }
        },
        ThresholdRules =
        {
            new ThresholdRule { ThresholdPercent = 0.50, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend },
            new ThresholdRule { ThresholdPercent = 0.90, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend },
            new ThresholdRule { ThresholdPercent = 1.00, SpendBasis = ThresholdRule.Types.Basis.CurrentSpend }
        }
    };
}
