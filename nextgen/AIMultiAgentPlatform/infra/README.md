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
- Function App for workers/background processing

The `production` mode additionally creates:

- dedicated API App Service plan
- Web App for the HTTP API
- Azure SQL logical server
- Azure SQL database
- Azure Service Bus namespace
- Service Bus queues

The `lean` mode uses:

- a single Azure Functions Flex Consumption app
- a Functions-based HTTP entrypoint instead of the ASP.NET Core Web App
- no dedicated App Service plan override in the `dev` parameter file because lean no longer uses that resource

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
  Production App Service compute plus the lean/prod Function App topology.

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

You can also override operational alerting at deploy time without editing the base template:

```bash
az deployment sub create \
  --location eastus2 \
  --template-file infra/main.subscription.bicep \
  --parameters infra/main.subscription.prod.bicepparam \
  --parameters enableOperationalAlerts=true \
  --parameters alertEmailReceivers=\"['alerts-platform@rnmglobalsolutions.com']\"
```

## GitHub Actions

Repository workflows live at the repo root:

- `.github/workflows/aimap-pr-validation.yml`
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

Recommended git flow:

- work in `feature/*` branches
- open a pull request into `main`
- let `.github/workflows/aimap-pr-validation.yml` run build and test only
- merge into `main` to trigger automatic `dev` deployment
- use manual workflow dispatch plus environment approval for `prod`

## Notes

- This infrastructure assumes the application code is deployed separately through GitHub Actions or another CI/CD pipeline.
- `lean` now uses a Functions-based HTTP entrypoint so the low-cost environment can run as a single serverless Function App.
- In `lean`, GitHub deploys the Functions entrypoint project to the single Function App. In `production`, GitHub deploys the ASP.NET Core API to the Web App and the worker host to the Function App.
- The app settings now distinguish current runtime modes from target infrastructure intent so the platform can evolve toward Table/Queue/Functions Flex Consumption later without rewriting the business layer.
- See [infrastructure-modes.md](../docs/architecture/infrastructure-modes.md) for the runtime-vs-target model.
- App settings include placeholders for external providers such as OpenAI, ManyChat, Calendly, HeyGen, and ElevenLabs. Store the actual secrets in Key Vault.
- The compute layer now injects `TallyWebhook__SigningSecret` as a Key Vault reference. Create the secret in Key Vault with the exact name `tally-webhook-signing-secret`.
- The first production hardening step after this baseline should be to add:
  - private endpoints / networking controls
  - SQL firewall tightening
  - Service Bus authorization rules if needed
  - deployment slots / release strategy
  - alert rules and diagnostic settings expansion
