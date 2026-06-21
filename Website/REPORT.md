# Fuse-Inventory Deep Dive Report

> Generated: 2026-06-21
> Branch: `website`
> Purpose: Context reference for building the Fuse-Inventory marketing website

---

## What It Is

**Fuse-Inventory** is a self-hosted Configuration Management Database (CMDB) / IT asset management tool. It's a "single pane of glass" for DevOps and development teams to track everything they deploy — where it runs, what it depends on, what credentials it needs, and what happens if it breaks.

**Target audience:** DevOps engineers, system administrators, architects — anyone who needs visibility into a complex infrastructure without spending weeks setting up a traditional CMDB.

---

## Tech Stack

| Layer | Technology |
|---|---|
| **Backend** | .NET 10, ASP.NET Core |
| **Storage** | In-memory + JSON files on disk + LiteDB (audit) |
| **Frontend (app)** | Vue 3 + Quasar (Material) + TypeScript + Vite |
| **State** | Pinia + TanStack Vue Query |
| **Graph** | Cytoscape (force-directed layout) |
| **Deploy** | Single Docker container (Nginx serves static + Kestrel serves API) |

The UI and API are bundled in one image — no separate web server needed.

---

## Core Data Model

The application models infrastructure in a natural hierarchy:

1. **Environments** — top-level groupings (dev, test, production, etc.)
2. **Applications** — codebases or hosted products with name, version, owner, framework, etc.
3. **Instances** — an application deployed into a specific environment, with URLs, health checks, and deployed version
4. **Dependencies** — links between instances and other resources (another instance, database, external service, message broker)
5. **Accounts** — credentials (API keys, passwords, certs) scoped to targets with DB grants
6. **Identities** — managed identities, service accounts, IAM roles
7. **Data Stores** — SQL, Redis, etc.
8. **External Resources** — third-party services (Stripe, mail providers, etc.)
9. **Message Brokers** — RabbitMQ, Service Bus, etc.
10. **Platforms** — servers, clusters, serverless, container hosts
11. **Tags** — colored labels for filtering across everything
12. **Positions, Responsibilities, Risks** — governance/compliance layer

---

## Key Features (from screenshots + code)

### Dashboard / Home
Cards showing aggregate counts (applications, platforms, environments, external resources), then a grid of instance cards with URI, platform, health status, and dependencies.

### Dependency Graph
Interactive node-link diagram with color-coded nodes (blue=services, purple=databases, green=external), environment containers, focus modes (direct/full chain/critical path), and risk overlay.

### Blast Radius
"What-if" failure simulation. Pick a resource → see exactly what breaks (direct and indirect impact) → specific affected systems listed with view buttons.

### Documentation Mode
Read-only, shareable view without login required. Global search across all entity types.

### SQL Integration
Connect to SQL databases, detect permission drift (expected vs actual), create missing accounts, bulk-resolve drift.

### Azure Key Vault & App Configuration
Browse/create/rotate secrets, reference secrets from accounts, browse key-values.

### Health Monitoring
Uptime Kuma integration with real-time health status on dashboard and dedicated Kuma Dashboard page.

### Audit Logs
100+ distinct audit action types, searchable with date range / area / action / user filters.

### Risk Management
Track risks with impact/likelihood ratings, status lifecycle, mitigation plans, ownership, and approval workflow.

### Import / Export
Full config as JSON or YAML, merge imports.

### Security
Three modes (open, read-only, locked-down), JWT auth, PBKDF2/SHA-256 password hashing, role-based access control with 30+ granular permissions, custom roles.

---

## Screenshots Available

The `Screenshots/fuse_screenshots/` directory has 25 high-res screenshots covering every feature area:

| Screenshot | Description |
|---|---|
| `home.png` | Dashboard |
| `dependency_graph.png` | Full dependency graph view |
| `blast_radius.png` | Failure simulation |
| `azure_integration.png` | Azure Key Vault / App Config |
| `documentation_mode.png` | Read-only public docs view |
| `configuration.png` | App config explorer |
| `audit.png` | Audit logs |
| `security.png` | Security settings and users |
| `account_edit.png` | Account credentials editing |
| `application.png` | Application detail |
| `application_instance.png` | Instance detail with deps |
| `document_completeness.png` | Documentation gap analysis |
| `data_stores.png` | Data store management |
| `message_brokers_edit.png` | Message broker editing |
| `password_generator.png` | Password generator |
| `platform.png` | Platform management |
| `environments.png` | Environment management |
| `risks.png` | Risk tracking |
| `positions.png` | Position management |
| `responsibilities.png` | Responsibility assignments |
| `roles.png` | Role management |
| `identity_edit.png` | Identity editing |
| `external_resources.png` | External resources |
| `settings.png` | App settings |
| `activities.png` | Activity tracking |

Plus two top-level README hero images: `Home.png` and `Graph.png`.

---

## What This Means for the Marketing Site

The marketing site should communicate:

1. **The problem:** Infrastructure docs are scattered, outdated, and nobody knows what depends on what.
2. **The solution:** Fuse-Inventory gives you a single, self-hosted, living map of your entire tech estate.
3. **Key capabilities to highlight:**
   - **Dashboard** — "Explore your estate" at a glance
   - **Graph** — Visualize dependencies with color-coded nodes
   - **Blast Radius** — "What breaks if X dies?" — chaos engineering without the chaos
   - **Azure Integration** — Key Vault, App Configuration out of the box
   - **SQL Drift Detection** — compare expected vs actual permissions
   - **Documentation Mode** — read-only, shareable, no login needed
   - **Self-hosted** — one Docker container, data stays on your disk
4. **Technical credibility:** .NET 10 + Vue 3, open source, Docker deployment, MIT license
5. **Call to action:** Self-host with one `docker run` command, or view on GitHub