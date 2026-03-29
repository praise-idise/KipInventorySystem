# KIP Inventory Management System

KIP Inventory Management System is a layered ASP.NET Core backend for running inventory operations with business discipline, not just stock CRUD. It models how stock is procured, received, reserved, transferred, adjusted, fulfilled, and audited across warehouses, while enforcing the rules that keep those movements financially and operationally credible.

The project is built to show two things clearly:
- technical depth in architecture, reliability, and operational safeguards
- business thinking in procurement, warehouse control, approval flow, stock valuation, and low-stock response

## What The System Solves

This system is designed for organizations that need more than a product catalog and quantity counter. It supports the operational lifecycle around inventory:
- product setup with structured SKU generation and variant attributes
- warehouse management and warehouse-specific inventory balances
- supplier and customer management
- purchase ordering and goods receipt
- opening balance initialization for clean go-live scenarios
- internal stock issues
- inter-warehouse transfers
- stock adjustments with approval and drift protection
- sales order reservation and fulfillment
- low-stock monitoring, notification, and auto-reorder drafting
- approval history for controlled inventory decisions

## Business Rules Encoded In The Application

This project deliberately pushes important inventory rules into the application layer instead of leaving them to convention.

- A purchase order must move through a lifecycle: `Draft -> PendingApproval -> Approved -> PartiallyReceived/Received -> Cancelled`.
- Goods can only be received against approved or partially received purchase orders.
- A sales order reserves stock at confirmation time and consumes reserved stock during fulfillment.
- A transfer request reserves stock in the source warehouse on approval, moves it out on dispatch, and lands it in the destination warehouse on completion.
- A stock adjustment is drafted against a captured quantity snapshot and cannot be applied if live stock has drifted since the draft was created.
- Opening balances are only allowed when a product has no stock history in that warehouse and the starting inventory position is still zero.
- Duplicate products are blocked where a document should have one authoritative line per product.
- Low-stock handling is not passive: the system can notify responsible users and auto-create or update draft purchase orders when a default supplier is available.
- Products without a default supplier are escalated for manual procurement review instead of being silently ignored.
- Inventory movements always leave an auditable trail through `StockMovement` records and document references.

These rules are what turn the codebase from "inventory endpoints" into an actual operations system.

## Technical Highlights

- Clean layered structure with separate API, Application, Domain, Infrastructure, and Shared projects
- PostgreSQL via EF Core with explicit indexes, precision settings, and domain-focused model configuration
- Global soft-delete filtering for `BaseEntity` records
- JWT authentication with Redis-backed session tracking and token version revocation
- Role-based authorization across admin, approver, procurement, warehouse, and standard user roles
- Idempotent write operations using Redis, payload hashing, and replay-safe response caching
- Serializable transaction execution with retry handling for PostgreSQL concurrency conflicts
- Background processing with Hangfire for emails and low-stock automation
- Structured logging with Serilog
- Rate limiting, health checks, API versioning, and Swagger/OpenAPI support
- Case-insensitive search behavior using PostgreSQL-aware querying where appropriate

## Architecture

### Projects

- `KipInventorySystem`: API surface, middleware, controllers, auth setup, Swagger, versioning, rate limiting, and startup pipeline
- `KipInventorySystem.Application`: business workflows, DTOs, validators, mappers, and orchestration logic
- `KipInventorySystem.Domain`: entities, enums, and core interfaces
- `KipInventorySystem.Infrastructure`: EF Core persistence, repositories, integrations, background infrastructure, and seeding
- `KipInventorySystem.Shared`: shared response models, enums, context helpers, and common abstractions

### Core Design Choices

- Repositories and unit of work keep persistence explicit and consistent across workflows.
- Business services carry the real domain behavior instead of hiding it behind generic abstractions.
- Mutating inventory operations run inside a dedicated transaction runner with serializable isolation and retry handling.
- API controllers stay thin and mostly coordinate request validation, authorization, idempotency header extraction, and response shaping.

## Inventory And Operations Design

### Product Modeling

- Products carry category code, brand code, item code, unit of measure, reorder threshold, and reorder quantity.
- SKU generation is deterministic from business fields and variant attributes, which makes product identity meaningful instead of random.
- Variant attributes allow products to represent practical warehouse distinctions such as size, color, or packaging variations.

### Warehouse Control

- Warehouses have generated codes and track inventory per product.
- `WarehouseInventory` separates `QuantityOnHand` from `ReservedQuantity`, exposing `AvailableQuantity` for operational decisions.
- Reorder thresholds can be set globally at product level and overridden per warehouse inventory record.

### Inventory Valuation

- Inbound stock updates average unit cost and inventory value.
- Outbound stock consumes value from current average cost.
- Costing logic is centralized so receipts, issues, transfers, and adjustments stay financially consistent.

### Approvals And Governance

- Purchase orders, transfer requests, and stock adjustments can enter an approval queue.
- Approval history is retained per document, making business decisions traceable.
- Comments on "return for changes" workflows force actionable feedback instead of opaque rejection.

## Reliability And Safeguards

### Idempotency

High-risk write operations use `X-Idempotency-Key` with Redis-backed request tracking. This protects the system from duplicate submissions caused by retries, browser refreshes, or unstable networks.

### Concurrency

Inventory workflows run through a serializable transaction runner with retry logic for PostgreSQL serialization and deadlock failures. This matters in domains where two users receiving, reserving, or adjusting stock at the same time can otherwise create silent data corruption.

### Auditability

Each important stock-changing workflow records movement type, reference type, reference id, creator, timestamps, and notes. That makes it possible to answer operational questions like:
- why stock changed
- who triggered the change
- which business document caused it
- what valuation was applied

### Operational Visibility

- `/health` exposes application health
- Hangfire dashboard provides visibility into background jobs
- Serilog captures request and application events
- Swagger gives a browsable contract for the API

## Security Model

- JWT access tokens with configurable issuer, audience, and lifetime
- refresh-token sessions stored in Redis
- session revocation through token versioning
- role-based endpoint protection through custom role attributes
- custom authorization and error middleware for consistent API responses
- rate limiting to reduce abuse and accidental request storms

## API Surface

The API is versioned under `api/v1/...` and currently exposes modules for:

- authentication
- products
- suppliers
- warehouses
- purchase orders
- goods receipts
- opening balances
- stock issues
- transfer requests
- stock adjustments
- approvals
- customers
- sales orders

## Background Automation

Background jobs are used as part of the business design, not as a side feature.

- welcome, password-reset, password-change, low-stock, procurement-review, and purchase-order approval emails
- low-stock evaluation after stock-affecting operations
- draft purchase order auto-creation or update for reorder scenarios

This gives the system a practical operational loop: detect risk, alert people, and prepare the next procurement action.

## Configuration

The application expects configuration for:

- PostgreSQL connection
- Redis connection
- JWT settings
- frontend URLs for password reset flows
- seeded admin credentials
- SMTP email settings
- Cloudinary file storage
- Stripe
- Hangfire dashboard credentials
- CORS origins

Development startup currently auto-applies migrations and seeds roles plus the initial admin user.

## Getting Started

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- Redis

### Run Locally

```bash
dotnet restore
dotnet build .\KipInventorySystem.slnx
dotnet run --project .\KipInventorySystem\KipInventorySystem.API.csproj
```

### First-Run Notes

- Update `appsettings.json` or user secrets with your real infrastructure values.
- In development, the app can apply migrations and seed roles/admin automatically at startup.
- Swagger is enabled in development.
- Hangfire dashboard is exposed at `/hangfire`.
- Health checks are exposed at `/health`.

## Design Philosophy

The main idea to notice is this: the code is intentionally written to show not just how inventory data is stored, but how inventory decisions are controlled. It shows deliberate thought about how procurement and warehouse teams actually operate, where approvals should exist, how stock should be reserved versus physically moved, how to prevent double submission and concurrency corruption, and how to automate low-stock response without removing human oversight.

## Future Direction

The repository already includes notes around query filters and multitenancy evolution. A strong next phase would be:

- deeper reporting and inventory analytics
- richer procurement KPIs
- stricter financial reconciliation workflows
- tenant isolation for SaaS scenarios
- automated test coverage around critical stock flows