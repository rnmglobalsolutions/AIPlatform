# Infrastructure Modes

`AIMultiAgentPlatform` now supports two infrastructure modes:

- `Lean`
- `Production`

The important design rule is that the domain and application layers do not change behavior based on these modes. The mode only affects:

- deployed Azure resources
- dependency injection wiring
- target infrastructure capabilities

## Lean

Lean mode is the low-cost starting point for development and early validation.

Target direction:

- Azure Functions Consumption
- Blob Storage
- Table Storage
- Queue Storage
- Application Insights
- Key Vault

Current implementation status:

- infrastructure can be deployed in a reduced footprint
- application wiring records the environment as `Lean`
- persistence still uses in-memory adapters today
- messaging still uses in-memory/runtime placeholders today

This is intentional. We are keeping the business layer stable while the real Table/Queue adapters are implemented later.

## Production

Production mode is the full infrastructure footprint for serious operations.

Target direction:

- dedicated compute
- Azure SQL
- Azure Service Bus
- Blob Storage
- Application Insights
- Key Vault
- managed identity

Current implementation status:

- infrastructure can deploy the heavier production resources
- application wiring records the environment as `Production`
- persistence still uses in-memory adapters today
- messaging still uses in-memory/runtime placeholders today

The next production-hardening step is to implement the real SQL and Service Bus adapters behind the existing application abstractions.

## Current Runtime vs Target Infrastructure

The codebase distinguishes between:

- current runtime mode
- target infrastructure mode

This avoids lying to the system.

Example:

- `Lean` deployment may target `Table` and `Queue`
- but the current app can still run with `InMemory` persistence until those adapters are built

That keeps the architecture honest and avoids sprinkling `if lean` or `if production` checks across use cases.

## Design Rule

Use cases must never branch on infrastructure mode.

Variation belongs in:

- Bicep deployment
- configuration
- dependency injection
- infrastructure adapter implementations
