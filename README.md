# 🧩 Fuse-Inventory

[![CI Pipeline](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml/badge.svg)](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml)
[![Docker Image](https://ghcr-badge.egpl.dev/lazerduck/fuse-inventory/latest_tag?trim=major&label=latest)](https://github.com/lazerduck/fuse-inventory/pkgs/container/fuse-inventory)

<img width="512" height="512" alt="Fuse-inventory" src="https://github.com/user-attachments/assets/2b4cd430-ed63-4f62-af85-3e89470bd0aa" />

**Fuse-Inventory** is a self-hosted application inventory and environment tracker designed for teams that want visibility without overhead.
It helps you describe your applications, infrastructure, and permissions in one place — without needing a full CMDB or enterprise stack.

---

## ✨ Overview

Fuse-Inventory lets development and DevOps teams:
- Map applications, environments, and platforms (servers, clusters, ACA, etc.)
- Record dependencies, databases, and accounts with linked grants and roles
- Capture how systems actually work — not just where they run
- Import/export everything as simple YAML or JSON
- Validate and reverse-import SQL permissions (Coming soon)
- Generate GRANT / REVOKE scripts for restores or audits (Coming soon)
- Compare environments and highlight drift at a glance (Coming soon)
- Optionally integrate with Uptime-Kuma for live health info (Coming soon)

Fuse-Inventory treats applications as first-class objects, with environments, dependencies, and infrastructure supporting them.
It aims to bridge the gap between documentation, DevOps, and runtime state — helping you keep human knowledge in sync with live systems.

---

## 🚀 Quick Start

### Using Docker (Recommended)

Pull and run the latest image from GitHub Container Registry:

```bash
# Pull the latest image
docker pull ghcr.io/lazerduck/fuse-inventory:latest

# Run the container
docker run -d \
  --name fuse-inventory \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  ghcr.io/lazerduck/fuse-inventory:latest
```

The application will be available at `http://localhost:8080`

### Using Docker Compose

```bash
docker-compose up -d
```

See [DOCKER.md](DOCKER.md) for more detailed Docker instructions.

---

## 🧩 Current Status
Fuse-Inventory is in early development but probably ready for use with the core data models unlikely to change and migration supports.

---

## 🛣️ Roadmap

- Uptime Kuma integration
- Azure KV integration
- QOL improvements
