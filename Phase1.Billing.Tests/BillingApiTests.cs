using GcpApi.TestCommon;
using Google.Cloud.Billing.V1;
using Google.Cloud.Iam.V1;

namespace Phase1.Billing.Tests;

/// <summary>
/// Integration tests for the Cloud Billing API.
/// Requires: gcloud auth application-default login
///           Billing Account Viewer IAM role
/// </summary>
public sealed class BillingApiTests(GcpClientFixture fixture) : IClassFixture<GcpClientFixture>
{
    // ── Billing Accounts ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListBillingAccounts_ReturnsAtLeastOneAccount()
    {
        var accounts = new List<BillingAccount>();
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
            accounts.Add(account);

        Assert.NotEmpty(accounts);
    }

    [Fact]
    public async Task ListBillingAccounts_EachAccount_HasValidNameAndDisplayName()
    {
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
        {
            Assert.False(string.IsNullOrWhiteSpace(account.Name),        "Account.Name should not be empty");
            Assert.False(string.IsNullOrWhiteSpace(account.DisplayName), "Account.DisplayName should not be empty");
            Assert.StartsWith("billingAccounts/", account.Name);
        }
    }

    // ── Linked Projects ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListProjectsForFirstBillingAccount_ReturnsWithoutError()
    {
        BillingAccount? first = null;
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
        {
            first = account;
            break;
        }

        Skip.If(first is null, "No billing accounts found — skipping linked-project test.");

        await foreach (ProjectBillingInfo project in fixture.BillingClient.ListProjectBillingInfoAsync(first!.Name))
        {
            Assert.False(string.IsNullOrWhiteSpace(project.Name), "ProjectBillingInfo.Name should not be empty");
        }
    }

    [Fact]
    public async Task ListProjectsForFirstBillingAccount_BillingAccountName_MatchesParent()
    {
        BillingAccount? first = null;
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
        {
            first = account;
            break;
        }

        Skip.If(first is null, "No billing accounts found — skipping this test.");

        await foreach (ProjectBillingInfo project in fixture.BillingClient.ListProjectBillingInfoAsync(first!.Name))
        {
            if (project.BillingEnabled)
                Assert.Equal(first.Name, project.BillingAccountName);
        }
    }

    // ── Specific account by ID (requires testsettings.json / env var) ─────────

    [SkippableFact]
    public async Task GetBillingAccount_WithConfiguredId_ReturnsAccount()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string accountName = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        BillingAccount account = await fixture.BillingClient.GetBillingAccountAsync(accountName);

        Assert.Equal(accountName, account.Name);
        Assert.False(string.IsNullOrWhiteSpace(account.DisplayName));
    }

    [SkippableFact]
    public async Task GetBillingAccount_WithConfiguredId_IsOpen()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string accountName = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        BillingAccount account = await fixture.BillingClient.GetBillingAccountAsync(accountName);

        Assert.True(account.Open, $"Billing account '{account.DisplayName}' should be open.");
    }

    // ── GetProjectBillingInfo ─────────────────────────────────────────────────

    [SkippableFact]
    public async Task GetProjectBillingInfo_WithConfiguredProject_ReturnsBillingInfo()
    {
        Skip.If(fixture.Settings.ProjectId is null,
            "Set GCP_PROJECT_ID or ProjectId in testsettings.json to run this test.");

        string resourceName = $"projects/{fixture.Settings.ProjectId}";
        ProjectBillingInfo info = await fixture.BillingClient.GetProjectBillingInfoAsync(resourceName);

        Assert.Equal(resourceName, info.Name);
    }

    [SkippableFact]
    public async Task GetProjectBillingInfo_WithConfiguredProject_BillingIsEnabled()
    {
        Skip.If(fixture.Settings.ProjectId is null,
            "Set GCP_PROJECT_ID or ProjectId in testsettings.json to run this test.");

        string resourceName = $"projects/{fixture.Settings.ProjectId}";
        ProjectBillingInfo info = await fixture.BillingClient.GetProjectBillingInfoAsync(resourceName);

        Assert.True(info.BillingEnabled,
            $"Project '{fixture.Settings.ProjectId}' should have billing enabled.");
        Assert.False(string.IsNullOrWhiteSpace(info.BillingAccountName),
            "BillingAccountName should be set when billing is enabled.");
    }

    // ── Filtering ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListBillingAccounts_WithOpenFilter_ReturnsOnlyOpenAccounts()
    {
        var request = new ListBillingAccountsRequest { Filter = "open=true" };
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync(request))
        {
            Assert.True(account.Open,
                $"Account '{account.DisplayName}' should be open when filter is open=true.");
        }
    }

    [Fact]
    public async Task ListBillingAccounts_OpenAndAllCounts_OpenCountLessOrEqualTotal()
    {
        int totalCount = 0;
        int openCount  = 0;

        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
            totalCount++;

        var openRequest = new ListBillingAccountsRequest { Filter = "open=true" };
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync(openRequest))
            openCount++;

        Assert.True(openCount <= totalCount,
            $"Open account count ({openCount}) should not exceed total ({totalCount}).");
    }

    // ── IAM permissions ───────────────────────────────────────────────────────

    [SkippableFact]
    public async Task TestIamPermissions_ViewerPermissionsOnBillingAccount_AreGranted()
    {
        Skip.If(fixture.Settings.BillingAccountId is null,
            "Set GCP_BILLING_ACCOUNT_ID or BillingAccountId in testsettings.json to run this test.");

        string resource     = $"billingAccounts/{fixture.Settings.BillingAccountId}";
        var    permissions  = new[] { "billing.accounts.get", "billing.accounts.list" };

        TestIamPermissionsResponse response =
            await fixture.BillingClient.TestIamPermissionsAsync(resource, permissions);

        // At least one viewer permission should be granted since the account is accessible.
        Assert.NotEmpty(response.Permissions);
    }

    // ── Field validation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ListBillingAccounts_MasterBillingAccount_WhenSet_HasCorrectPrefix()
    {
        await foreach (BillingAccount account in fixture.BillingClient.ListBillingAccountsAsync())
        {
            if (!string.IsNullOrEmpty(account.MasterBillingAccount))
                Assert.StartsWith("billingAccounts/", account.MasterBillingAccount,
                    StringComparison.Ordinal);
        }
    }
}
