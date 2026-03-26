# GCP Cost API Tests

A .NET 8 console application that demonstrates how to interact with four Google Cloud Platform billing and cost APIs using their official C# SDKs.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Google Cloud SDK](https://cloud.google.com/sdk/docs/install) (`gcloud` CLI)
- A GCP account with at least one billing account

### Authentication

All phases use [Application Default Credentials (ADC)](https://cloud.google.com/docs/authentication/application-default-credentials). Run this once before executing any phase:

```bash
gcloud auth application-default login
```

---

## NuGet Packages

| Package | Version | Used by |
|---|---|---|
| `Google.Cloud.Billing.V1` | 3.10.0 | Phase 1, Phase 3 |
| `Google.Cloud.BigQuery.V2` | 3.11.0 | Phase 2 |
| `Google.Cloud.Billing.Budgets.V1` | 2.7.0 | Phase 4 |

Install all at once:

```bash
dotnet add package Google.Cloud.Billing.V1
dotnet add package Google.Cloud.BigQuery.V2
dotnet add package Google.Cloud.Billing.Budgets.V1
```

---

## Project Structure

```
GcpCostApiTests/
├── Program.cs               # Entry point — edit to choose which phase to run
├── Phase1BillingDemo.cs     # Phase 1: Cloud Billing API (account metadata)
├── Phase2BigQueryDemo.cs    # Phase 2: BigQuery API (actual spend)
├── Phase3CatalogDemo.cs     # Phase 3: Cloud Catalog API (list prices)
└── Phase4BudgetDemo.cs      # Phase 4: Billing Budgets API (spend thresholds)
```

---

## Phase 1 — Cloud Billing API (Account Metadata)

**File:** `Phase1BillingDemo.cs`  
**IAM required:** Billing Account Viewer  
**NuGet:** `Google.Cloud.Billing.V1`

Lists all billing accounts you have access to, then lists every GCP project linked to the first billing account found.

**Sample output:**
```
Cloud Billing API test

Billing accounts:
- My Company Billing (billingAccounts/012345-6789AB-CDEF01)

Using first billing account: My Company Billing (billingAccounts/012345-6789AB-CDEF01)
Projects linked to this billing account:
- my-project-id | Billing enabled: True | Billing account: billingAccounts/012345-6789AB-CDEF01
```

**To run**, update `Program.cs`:
```csharp
await Phase1BillingDemo.RunAsync();
```

---

## Phase 2 — BigQuery Billing Export (Actual Spend)

**File:** `Phase2BigQueryDemo.cs`  
**IAM required:** BigQuery Data Viewer on the export dataset  
**NuGet:** `Google.Cloud.BigQuery.V2`

Queries your BigQuery billing export table to calculate total cost (cost + credits) for the **current month**, grouped by GCP service.

### Setup

Enable billing export in the GCP Console:  
**Billing → Billing Export → Standard usage cost export**

Note your **Project ID**, **Dataset ID**, and **Table name**, then update the constant in `Phase2BigQueryDemo.cs`:

```csharp
const string billingExportTable = "your-project-id.your_dataset.gcp_billing_export_v1_xxx";
```

**Sample output:**
```
Querying billing export table: my-project.billing_export.gcp_billing_export_v1_ABC123

Current month cost by service:
- Compute Engine: 142.37
- Cloud Storage: 18.04
- BigQuery: 4.91
```

**To run**, update `Program.cs`:
```csharp
await Phase2BigQueryDemo.RunAsync();
```

---

## Phase 3 — Cloud Catalog API (List Prices)

**File:** `Phase3CatalogDemo.cs`  
**IAM required:** None (public pricing data)  
**NuGet:** `Google.Cloud.Billing.V1`

Fetches the full public service catalog, locates **Compute Engine**, then paginates through its SKUs to find an **N1 Standard Core (Americas)** SKU and prints its pricing tiers. Falls back to any Compute core SKU if the preferred one is not found.

**Sample output:**
```
Finding Compute Engine service...
Compute Engine service ID: services/6F81-5844-456A

Fetching SKUs...
Selected SKU: N1 Standard Instance Core running in Americas
SKU ID: services/6F81-5844-456A/skus/0048-21CE-74C0

Pricing tiers:
- Start usage amount: 0, unit price: $0.031611
```

**To run**, update `Program.cs`:
```csharp
await Phase3CatalogDemo.RunAsync();
```

---

## Phase 4 — Billing Budgets API (Spend Thresholds)

**File:** `Phase4BudgetDemo.cs`  
**IAM required:** Billing Account Administrator  
**NuGet:** `Google.Cloud.Billing.Budgets.V1`

Two operations:

1. **`RunAsync`** — lists all existing budgets for a billing account (called from `Main` by default).
2. **`CreateTestBudgetAsync`** — creates a **$50 USD** budget scoped to a specific project, with alert thresholds at **50%, 90%, and 100%** of actual spend. Not called by default — uncomment when ready to test.

### Setup

Update the constants in `Program.cs`:

```csharp
const string billingAccountId = "012345-6789AB-CDEF01"; // from Phase 1 output
```

To also create a test budget, uncomment:

```csharp
await Phase4BudgetDemo.CreateTestBudgetAsync(billingAccountId, "your-project-id");
```

**Sample output (listing):**
```
Listing budgets for billing account: billingAccounts/012345-6789AB-CDEF01

Budget #1
  Name:         billingAccounts/012345-6789AB-CDEF01/budgets/abc-123
  Display name: Monthly cap
  Amount:       $500.00 USD
  Thresholds:   50% (CurrentSpend), 90% (CurrentSpend), 100% (CurrentSpend)
```

**To run** (already the default entry point):
```csharp
await Phase4BudgetDemo.RunAsync(billingAccountId);
```

---

## Running the App

1. Authenticate:
   ```bash
   gcloud auth application-default login
   ```

2. Edit `Program.cs` to call the phase you want to test.

3. Run:
   ```bash
   dotnet run
   ```
