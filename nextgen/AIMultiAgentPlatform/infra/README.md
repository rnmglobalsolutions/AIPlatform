# Bicep Infrastructure

This folder defines the Azure infrastructure for `AIMultiAgentPlatform` from scratch.

It supports two deployment modes:

- `lean`
- `production`

## Target Topology

The deployment always creates:

- resource group
- Log Analytics workspace
- Application Insights
- storage account
- user-assigned managed identity
- Key Vault
- low-cost or dedicated compute, depending on mode
- Web App for the HTTP API
- Function App for workers/background processing

The `production` mode additionally creates:

- Azure SQL logical server
- Azure SQL database
- Azure Service Bus namespace
- Service Bus queues

## Structure

- `main.subscription.bicep`
  Subscription-scope entrypoint. Creates the resource group and deploys the environment module into it.
- `main.subscription.dev.bicepparam`
  Example dev parameters.
- `main.subscription.prod.bicepparam`
  Example prod parameters.
- `modules/resource-group.bicep`
  Resource group creation.
- `modules/environment.bicep`
  Resource-group-scope composition module.
- `modules/monitoring.bicep`
  Log Analytics + App Insights.
- `modules/storage.bicep`
  Storage account and blob containers.
- `modules/identity.bicep`
  User-assigned managed identity.
- `modules/key-vault.bicep`
  Key Vault and RBAC wiring.
- `modules/sql.bicep`
  Azure SQL server and database.
- `modules/service-bus.bicep`
  Service Bus namespace and queues.
- `modules/compute.bicep`
  App Service plan, Web App, and Function App.

## Deploy

Examples:

```bash
az deployment sub create \
  --location eastus \
  --template-file infra/main.subscription.bicep \
  --parameters infra/main.subscription.dev.bicepparam
```

```bash
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.subscription.bicep \
  --parameters infra/main.subscription.prod.bicepparam
```

## GitHub Actions

Repository workflows live at the repo root:

- `.github/workflows/aimap-infra-dev.yml`
- `.github/workflows/aimap-infra-prod.yml`
- `.github/workflows/aimap-app-dev.yml`
- `.github/workflows/aimap-app-prod.yml`

Recommended GitHub environments:

- `dev`
- `prod`

Recommended environment variables for both environments:

- `AZURE_CLIENT_ID`
- `AZURE_TENANT_ID`
- `AZURE_SUBSCRIPTION_ID`

Recommended production-only variable:

- `AIMAP_SQL_ADMIN_LOGIN`

Recommended production-only secret:

- `AIMAP_SQL_ADMIN_PASSWORD`

The app deployment workflows expect infrastructure deployments to use these fixed Azure deployment names:

- `aimap-dev-infra`
- `aimap-prod-infra`

That allows the app workflows to resolve the API and worker app names from Bicep outputs before deploying code.

## Notes

- This infrastructure assumes the application code is deployed separately through GitHub Actions or another CI/CD pipeline.
- The current codebase still runs as ASP.NET Core API + worker host, so `lean` mode minimizes cost through resource omission and low-cost SKUs while preserving the current delivery shape.
- The app settings now distinguish current runtime modes from target infrastructure intent so the platform can evolve toward Table/Queue/Functions Consumption later without rewriting the business layer.
- See [infrastructure-modes.md](../docs/architecture/infrastructure-modes.md) for the runtime-vs-target model.
- App settings include placeholders for external providers such as OpenAI, ManyChat, Calendly, HeyGen, and ElevenLabs. Store the actual secrets in Key Vault.
- The first production hardening step after this baseline should be to add:
  - private endpoints / networking controls
  - SQL firewall tightening
  - Service Bus authorization rules if needed
  - deployment slots / release strategy
  - alert rules and diagnostic settings expansion
