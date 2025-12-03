# 🧩 Fuse-Inventory

[![CI Pipeline](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml/badge.svg)](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml)
[![Docker Image](https://ghcr-badge.egpl.dev/lazerduck/fuse-inventory/latest_tag?trim=major\&label=latest)](https://github.com/lazerduck/fuse-inventory/pkgs/container/fuse-inventory)

<img width="512" height="512" alt="ChatGPT Image Nov 20, 2025, 09_34_32 PM" src="https://github.com/user-attachments/assets/f21dc1c0-1211-4473-8984-188e69d6a8fe" />


**Fuse-Inventory** is a self-hosted application inventory and environment tracker designed for teams that want visibility without overhead.
It helps you describe what you have deployed, where it is deployed, and what dependencies, permissions, and accounts each system needs.

---

## ✨ Overview

Fuse-Inventory lets development and DevOps teams:

* Map applications, environments, and platforms (servers, clusters, ACA, etc.)
* Record dependencies, databases, and accounts with linked grants and roles
* Capture how systems actually work — not just where they run
* Import/export everything as simple YAML or JSON
* Visualise applications and dependencies in a graph view with highlightable nodes
* Track changes with comprehensive audits
* Detect account and permissions drift with SQL integration
* Optionally resolve account drift by creating accounts and applying permissions

Fuse-Inventory can also integrate with:

* **Uptime-Kuma** to display health information
* **Azure Key Vault** so you can assign, create, update, and view secrets based on your permissions

Fuse-Inventory treats applications as first-class objects, with environments, dependencies, and infrastructure supporting them.

It aims to bridge the gap between documentation, DevOps, and runtime state — keeping human knowledge aligned with live systems.

---
## Screenshots
Overview Screen
<img width="2877" height="1658" alt="Home" src="https://github.com/user-attachments/assets/27313b21-b623-4f7b-b802-bbccc802443b" />

SQL integration
<img width="2879" height="1640" alt="SQLIntegration" src="https://github.com/user-attachments/assets/dca56aba-0c30-474f-a3d9-1761939a131e" />

Graph
<img width="2879" height="1624" alt="Graph" src="https://github.com/user-attachments/assets/2c4d3baa-0d70-463a-a54f-07b2b20d6296" />

---

## 🧠 Philosophy

Fuse-Inventory is built to enable you to work the way *you* want.
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

---

### 📦 Using Docker Compose

```bash
docker-compose up -d
```

---

## 🔐 Security

Fuse runs entirely inside your environment — so you are responsible for securing the host, container, and data volume.

When given secrets or integration keys, Fuse stores them **on disk** so the system can use them when needed.
(No local encryption is performed; encrypting locally would still require storing a reversible key on disk.)

Fuse includes built-in authentication via JWT-based login:

* **Open mode** — anyone can view and edit
* **Read-only mode** — only admins can make changes
* **Locked-down mode** — login required to view anything

Some privileged actions (such as retrieving values from Azure Key Vault) **always** require admin access.

---

## 🧬 Core Model

Fuse-Inventory’s model reflects how real systems are structured:

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

Fuse-Inventory is built with:

* **.NET 10** for the API
* **Vue 3 + Quasar** for the UI
* In-memory data model with write-back to disk as JSON files
* **LiteDB** for audit storage
* Single-container deployment with persistent data volume

---

## 📌 Current Status

Fuse-Inventory is under active development, but the core data model is stable and backward-compatible across minor versions.

---

## 🧪 UI Testing

Fuse-Inventory includes a Playwright-based UI test suite for automated end-to-end testing.

### Running UI Tests Locally

```bash
# Navigate to the UI directory
cd UI/Fuse.Web

# Install dependencies
npm ci

# Install Playwright browsers (first time only)
npx playwright install --with-deps chromium

# Run tests (starts dev server automatically)
npm run test:ui

# Run tests with browser visible
npm run test:ui:headed

# Run tests in debug mode
npm run test:ui:debug
```

### Test Coverage

The UI test suite covers:

- **Environments** — Create, edit, delete operations
- **Applications** — Create, delete operations
- **Platforms** — Create, edit, delete operations
- **Data Stores** — Create, edit, delete operations
- **Accounts** — Page navigation and form access
- **Graph** — Page rendering and component visibility

### Writing New Tests

Tests are located in `UI/Fuse.Web/tests/ui/`. Use the helper functions in `tests/ui/helpers/` for common operations:

```typescript
import { test, expect } from '@playwright/test';
import { navigateTo, NavDestinations, waitForPageLoad } from './helpers';

test('example test', async ({ page }) => {
  await page.goto('/');
  await waitForPageLoad(page);
  await navigateTo(page, NavDestinations.environments);
  // Your test assertions here
});
```

### CI Integration

UI tests run automatically on push and pull requests via GitHub Actions. Test results and screenshots (on failure) are uploaded as artifacts.

---

## 🛣️ Roadmap

* Additional Azure Key Vault tooling
* Usability improvements and guided setup flows

Check out the [Project Board](https://github.com/lazerduck/fuse-inventory/projects?query=is%3Aopen).

