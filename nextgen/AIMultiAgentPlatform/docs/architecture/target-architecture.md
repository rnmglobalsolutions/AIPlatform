# AI Multi-Agent Platform

## Current Scope

The new solution is being built side-by-side with the legacy RNM platform.

The first implemented vertical slice is:

`Tally Intake -> Tenant Setup -> Strategy Plan -> Editorial Backlog Seed`

The second implemented vertical slice is:

`DailyContentRequest -> DailyContentBrief -> PrimaryAsset -> Caption -> Repurposing Bundle`

The third implemented vertical slice is:

`Compliance -> Quality -> Approval -> Scheduling`

The fourth implemented vertical slice is:

`ManyChat Event Intake -> Lead Generation Agent -> Lead State Update`

The fifth implemented vertical slice is:

`Booking Agent -> Calendly Integration -> Reminder / Follow-Up orchestration`

The sixth implemented vertical slice is:

`Report Agent -> Monthly performance snapshots`

The seventh implemented vertical slice is:

`Voice Agent -> ElevenLabs Integration`

## Architectural Direction

The target platform is organized around these bounded contexts:

- `Intake`
- `Tenant Management`
- `Strategy`
- `Editorial Planning`
- `Daily Production`
- `Review & Approval`
- `Publishing`
- `Lead Orchestration`
- `Booking`
- `Reminders`
- `Follow-Ups`
- `Voice Orchestration`
- `Reporting & Memory`

## Core Principles

- Clean Architecture with the domain at the center
- Explicit use cases in the application layer
- In-memory infrastructure for the first slice, replaceable later with Azure SQL/Service Bus/Blob adapters
- Idempotent and observable intake workflows
- Strong naming and explicit workflow state

## First Slice Decisions

For the first slice, the platform does not yet call LLM providers.
Instead, it seeds:

- a normalized `Tenant`
- a deterministic `StrategyPlan`
- a deterministic 14-day `EditorialBacklog`

This gives us a stable vertical slice and lets the rest of the platform build on clean contracts.

## Next Planned Slices

1. `Provider-backed adapters for Azure SQL, Service Bus, Blob Storage, and real ElevenLabs transport`
