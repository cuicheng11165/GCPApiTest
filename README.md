# GCP API Integration Test Suite

A .NET 8 solution that tests four Google Cloud Platform billing and cost APIs using their
official C# SDKs. Each phase is a self-contained **xUnit integration test project** that
runs real API calls against your GCP account — no mocks.

---

## Solution structure

```
GCPApi.sln
├── GcpApi.TestCommon/          # Shared auth fixture and settings reader
├── Phase1.Billing.Tests/       # Cloud Billing API  (account metadata)
├── Phase2.BigQuery.Tests/      # BigQuery API        (billing export queries)
├── Phase3.Catalog.Tests/       # Cloud Catalog API   (public pricing / SKUs)
└── Phase4.Budgets.Tests/       # Billing Budgets API (spend thresholds)
```

### NuGet packages used

| Package | Version | Used by |
|---------|---------|---------|
| `Google.Cloud.Billing.V1` | 3.x | Phase 1, Phase 3 |
| `Google.Cloud.BigQuery.V2` | 3.x | Phase 2 |
| `Google.Cloud.Billing.Budgets.V1` | 2.x | Phase 4 |
| `Xunit.SkippableFact` | 1.5.x | All test projects |

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Google Cloud SDK](https://cloud.google.com/sdk/docs/install) (`gcloud` CLI)
- A GCP account with at least one billing account

### Authentication

All phases use [Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials).
Run once before executing any tests:

```bash
gcloud auth application-default login
```

---

## Configuration

API-dependent tests read settings from **environment variables** or a local
`testsettings.json` file (place it at the solution root or any parent folder).

### Environment variables

| Variable | Required for |
|----------|-------------|
| `GCP_BILLING_ACCOUNT_ID` | Phase 1, Phase 4 |
| `GCP_PROJECT_ID` | Phase 1, Phase 4 |
| `GCP_BIGQUERY_TABLE` | Phase 2 |

### testsettings.json

Copy `GcpApi.TestCommon/testsettings.template.json` to `testsettings.json` at the
solution root and fill in your values:

```json
{
  "BillingAccountId": "XXXXXX-XXXXXX-XXXXXX",
  "ProjectId":        "your-gcp-project-id",
  "BigQueryExportTable": "your-project.your_dataset.gcp_billing_export_v1_XXXXXX"
}
```

> `testsettings.json` is listed in `.gitignore` — it will never be committed.

---

## Running the tests

**All tests (skips API tests when config is absent):**
```bash
dotnet test GCPApi.sln
```

**Single phase:**
```bash
dotnet test Phase1.Billing.Tests
dotnet test Phase2.BigQuery.Tests
dotnet test Phase3.Catalog.Tests
dotnet test Phase4.Budgets.Tests
```

**Pure-logic tests only (no credentials or config needed):**
```bash
dotnet test GCPApi.sln --filter "ParseProjectId|BuildCostQuery|BuildTestBudget"
```

---

## Phase overview

### Phase 1 — Cloud Billing API (`Google.Cloud.Billing.V1`)

**IAM role required:** Billing Account Viewer

Tests `CloudBillingClient`:

- List all accessible billing accounts and validate structure
- Filter accounts by `open=true`
- `GetBillingAccountAsync` — fetch a specific account by ID
- `GetProjectBillingInfoAsync` — verify billing is enabled on a project
- `TestIamPermissionsAsync` — confirm viewer permissions are granted
- Field validation: `MasterBillingAccount` prefix, `Open` flag

### Phase 2 — BigQuery Billing Export (`Google.Cloud.BigQuery.V2`)

**Prerequisite:** [Standard usage cost export](https://cloud.google.com/billing/docs/how-to/export-data-bigquery)
enabled in GCP Console → Billing → Billing Export.

Tests `BigQueryClient`:

- `ListDatasetsAsync` — enumerate datasets in the export project
- `GetDatasetAsync` — fetch and validate the export dataset
- `ListTablesAsync` — confirm the export table exists in the dataset
- `GetTableAsync` — verify table schema contains `cost` and `service` fields
- Query: total cost grouped by service for the current month (including credits)
- Query: total cost grouped by project over the last 30 days
- Query: parameterised date-range query using `BigQueryParameter`

### Phase 3 — Cloud Catalog / Pricing API (`Google.Cloud.Billing.V1`)

**IAM role required:** None — catalog data is public.

Tests `CloudCatalogClient`:

- `ListServicesAsync` — verify Compute Engine, Cloud Storage, BigQuery are present
- Validate service structure: non-empty `ServiceId`, `DisplayName`, `Name` prefix
- `ListSkusAsync` (Compute Engine) — description, category, region validation
- N1 Standard Core Americas SKU: tiered pricing tiers with positive unit price
- N1 Standard RAM SKU existence
- `ListSkusAsync` (Cloud Storage) — storage SKU and `UsageUnit` validation
- Pricing expression `UsageUnit` is set for all sampled SKUs with pricing info

### Phase 4 — Billing Budgets API (`Google.Cloud.Billing.Budgets.V1`)

**IAM role required:** Billing Account Administrator

Tests `BudgetServiceClient`:

- `ListBudgetsAsync` — list all budgets on the configured billing account
- Validate budget names contain the billing account prefix
- Validate threshold rules: `0 < percent ≤ 2.0`
- Validate `SpecifiedAmount`: non-empty currency code, non-negative units
- `GetBudgetAsync` — fetch first listed budget and compare with list result
- Pure-logic: `BuildTestBudget` helper — $50 USD, 50/90/100% thresholds, project filter
- Destructive (disabled by default): create a test budget then delete it

---

## GcpApi.TestCommon

Shared library used by all four test projects.

| Class | Purpose |
|-------|---------|
| `GcpTestSettings` | Reads config from env vars with fallback to `testsettings.json` |
| `GcpClientFixture` | `IAsyncLifetime` xUnit fixture; creates all GCP clients once per test class; catches ADC errors gracefully so pure-logic tests always run |

Pure-logic tests that never access a GCP client property pass without any
credentials or configuration.
