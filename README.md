<a href="https://fuse-inventory.dev/">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="UI/Fuse.Web/public/brand/fuse-inventory-logo-reverse.svg">
    <source media="(prefers-color-scheme: light)" srcset="UI/Fuse.Web/public/brand/fuse-inventory-logo-colour.svg">
    <img src="UI/Fuse.Web/public/brand/fuse-inventory-logo-colour.svg" alt="Fuse Inventory" width="340" height="144">
  </picture>
</a>

# Fuse Inventory

[![CI Pipeline](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml/badge.svg)](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml)
[![Docker Image](https://ghcr-badge.egpl.dev/lazerduck/fuse-inventory/latest_tag?trim=major\&label=latest)](https://github.com/lazerduck/fuse-inventory/pkgs/container/fuse-inventory)

[Website](https://fuse-inventory.dev/) · [Features](https://fuse-inventory.dev/features) · [Screenshots](https://fuse-inventory.dev/screenshots) · [Support & licensing](https://fuse-inventory.dev/licensing)

**Fuse Inventory** is a self-hosted application inventory and environment tracker designed for teams that want visibility without overhead.
It helps you describe what you have deployed, where it is deployed, and what dependencies, permissions, and accounts each system needs.

Part of [Fuse Industries](https://fuse-industries.dev/), a family of software and hardware projects built around your control. Explore our sibling project, [Fuse Vault](https://fuse-vault.dev/), a dedicated security device currently in development.

---

## ✨ Overview

Fuse Inventory lets development and DevOps teams:

* Map applications, environments, and platforms (servers, clusters, ACA, etc.)
* Record dependencies, databases, and accounts with linked grants and roles
* Capture how systems actually work — not just where they run
* Import/export everything as simple YAML or JSON
* Visualise applications and dependencies in a graph view with highlightable nodes
* Explore the blast radius of a change across connected systems
* Track risks, responsibilities, and documentation completeness
* Browse inventory in read-only documentation mode
* Track changes with comprehensive audits
* Detect account and permissions drift with SQL integration
* Optionally resolve account drift by creating accounts and applying permissions

Fuse Inventory can also integrate with:

* **Uptime-Kuma** to display health information
* **Azure Key Vault** so you can assign, create, update, and view secrets based on your permissions
* **Azure App Configuration** to explore configuration alongside your inventory

Fuse Inventory treats applications as first-class objects, with environments, dependencies, and infrastructure supporting them.

It aims to bridge the gap between documentation, DevOps, and runtime state — keeping human knowledge aligned with live systems.

---
## Screenshots

See the [screenshot gallery on the Fuse Inventory website](https://fuse-inventory.dev/screenshots) for a tour of the interface and features.

---

## 🧠 Philosophy

Fuse Inventory is built to enable you to work the way *you* want.
It doesn’t enforce “best practices” or block actions — even if they might be risky.

Plain-text secret references are intended for URLs or pointers to secrets stored elsewhere, but Fuse won’t stop you from storing whatever you choose.

Similarly, if you configure full Azure Key Vault integration (read/create/update), you are responsible for securing the container and its volume, because the Key Vault client secret must be stored locally.

---

## 🚀 Quick Start

### 🐳 Using Docker (Recommended)

Pull and run the latest image from GitHub Container Registry:

```bash
# Pull the latest image
docker pull ghcr.io/lazerduck/fuse-inventory:latest

# Run the container with a persistent data volume
docker run -d \
  --name fuse-inventory \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  ghcr.io/lazerduck/fuse-inventory:latest
```

The application will be available at:
**[http://localhost:8080](http://localhost:8080)**

Published containers check GitHub Container Registry at startup and every six hours for a newer image on their own channel (`:latest` for releases or `:pre-release` for pre-releases). When the **About Fuse Inventory** badge appears, open it to see the available version. Deploy the updated image using your usual process. Source builds without a release channel skip the check.

---

### 📦 Using Docker Compose with demo data

The included `docker-compose.yml` builds the application from source and mounts `test-data/` as its persistent data directory. From a checkout of this repository, run:

```bash
docker compose up -d --build
```

Open **[http://localhost:8080](http://localhost:8080)** and sign in with the demo account:

- **Username**: Admin
- **Password**: Password

The demo environment includes sample configurations for accounts, applications, environments, datastores, and more. Changes made in the app are saved back into `test-data/`. For a fresh inventory, use the Docker command above with a separate data directory.

---

## 🔐 Security

Fuse runs entirely inside your environment — so you are responsible for securing the host, container, and data volume.

When given secrets or integration keys, Fuse stores them **on disk** so the system can use them when needed.
Most integration secrets are stored as supplied. The Entra client secret uses ASP.NET Data Protection with keys stored in the same data volume; protect and back up that entire volume.

Creating the first administrator account signs you in. Access defaults to **Fully Restricted**; expand **Change access level** during setup if you want anonymous reading or editing instead. Your choice applies when the account is created. The setup wizard then creates your first environment and opens a four-step guide to documenting an application and its deployed instance. Existing installations keep their chosen security posture.

You can change the access model in Security settings:

* **Unrestricted** — anonymous inventory reading and editing
* **Restricted Editing** — anonymous reading; changes require sign-in and the appropriate permissions
* **Fully Restricted** — sign-in required; roles control read and write permissions

Use roles to limit inventory editing and access to secret values.

Microsoft Entra sign-in supports group-derived roles alongside direct Fuse assignments, explicit account linking, and session revocation. See the [Entra setup and validation guide](docs/entra.md) for configuration, refresh behaviour, limitations, and validation results.

---

## MCP workflows

Enable the MCP server in **App Settings**, then connect a Streamable HTTP client to
`https://<your-fuse-host>/api/mcp`. Authenticate with an `x-api-key` header or a Fuse
session bearer token. MCP always requires authentication, regardless of the UI's
anonymous-access setting. API keys use their assigned roles; tool discovery and
execution both enforce those permissions. Generic inventory readers additionally
check the permission for the requested entity type.

Secret creation and account creation are separate operations:

1. Use `inventory_list_secret_providers` to find a Key Vault provider and its
   capabilities. Use `inventory_list_secrets` to inspect existing names without
   retrieving values. Both require `secretproviders:read`; listing secrets also
   requires the provider's **Check** capability.
2. Call `inventory_create_secret` with `providerId` and `secretName`. Omitting
   `secretValue` uses Fuse's configured secure password generator. Supplying a
   value stores that value instead. Names must contain 1–127 letters, digits or
   hyphens. This requires `secretproviders:secrets:create` and the provider's
   **Create** capability. The result contains a `secretBinding`, never the value.
3. Pass that binding to `inventory_create_account`, or use
   `inventory_patch_account` to attach it to an existing account. These require
   `accounts:create` or `accounts:update`, respectively. Reading an account to
   obtain its current `updatedAt` for patching requires `accounts:read`.

Creation rejects an existing secret name, including disabled secrets, rather than
rotating it. The vault identity needs metadata-list and secret-set access; a failed
metadata check stops creation. Concurrent creates through one Fuse process are
serialized for the same vault/name, including provider aliases. Key Vault's set
operation is not an atomic create-only write, so this cannot prevent a simultaneous
write from another application or Fuse process. Explicit rotation remains a
separate REST/UI operation.

Named accounts conflict when target, authentication kind and exact, case-sensitive
username match. The check runs inside the store's write lock, including clones and
identity-changing edits. Existing duplicate records remain editable; unnamed
credentials are not deduplicated. A conflict identifies the existing account so a
client can inspect it instead of repeating a create. Accounts document inventory;
creating one does not provision a SQL principal.

Account creation, editing, deletion and cloning write audit events without secret
bindings or parameter values. Version history attributes requests to the signed-in
user or API key. With a shared key, attribution identifies the key rather than the
individual operating the client.

---

## 🧬 Core Model

Fuse Inventory’s model reflects how real systems are structured:

* **Environments** — top-level groupings (e.g., `dev`, `test`, `live`)
* **Applications** — codebases or hosted products
* **Instances** — an application deployed into an environment, with URLs and dependencies
* **Dependencies** — links to other applications, datastores, or external services
* **Accounts** — credentials associated with dependencies (API keys, SQL users, etc.)
* **Datastores** — SQL, Redis, RabbitMQ, etc., scoped to an environment
* **External Resources** — third-party services (payment providers, email platforms, etc.)
* **Platforms** — optional; servers or container platforms an instance runs on
* **Tags** — flexible labels that can be applied to almost anything

---

## 🛠️ Tech Stack

Fuse Inventory is built with:

* **.NET 10** for the API
* **Vue 3 + Quasar** for the UI
* In-memory data model with write-back to disk as JSON files
* **LiteDB** for audit storage
* Single-container deployment with persistent data volume

---

## 🧪 Test Commands

Use the current .NET 10 SDK. Use test categories to split fast unit tests from Docker-backed integration tests.

```bash
# Fast unit tests (excludes integration tests that need Docker)
dotnet test API/Fuse.Tests/Fuse.Tests.csproj --filter "Category!=Integration"

# Integration tests only (requires Docker)
dotnet test API/Fuse.Tests/Fuse.Tests.csproj --filter "Category=Integration"
```

Integration tests currently include API auth middleware end-to-end tests and SQL inspector tests that run against Testcontainers SQL Server.

To generate the same coverage report as CI, run from the repository root with Docker running:

```bash
dotnet tool restore
dotnet test --configuration Release /p:AltCover=true '/p:AltCoverAssemblyFilter=Fuse\.Tests'
dotnet tool run reportgenerator -- -reports:API/Fuse.Tests/coverage.xml -targetdir:coverage -reporttypes:Cobertura '-assemblyfilters:+Fuse.*'
```

Coverage tools are pinned in `.config/dotnet-tools.json`. CI installs an isolated .NET 10 SDK and runs on Ubuntu 26.04, including a native ARM64 container smoke check. Browser test setup is documented in [Tests/Playwright](Tests/Playwright/README.md).

Dependabot checks npm, NuGet, GitHub Actions and container image references weekly. Compatible minor and patch version updates are grouped; major version updates remain separate PRs for compatibility review. npm and NuGet security fixes are grouped separately.

---

## 📌 Current Status

Fuse Inventory is under active development, but the core data model is stable and backward-compatible across minor versions.

---

## 🛣️ Roadmap

See [the feature backlog](IMPROVEMENTS.md) for pending work and completed improvements, and [the Scrum Poker plan](SCRUM_POKER_PLAN.md) for its remaining verification tasks.

Check out the [Project Board](https://github.com/lazerduck/fuse-inventory/projects?query=is%3Aopen).

