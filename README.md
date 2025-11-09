# 🧩 Fuse-Inventory

[![CI Pipeline](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml/badge.svg)](https://github.com/lazerduck/fuse-inventory/actions/workflows/ci.yml)
[![Docker Image](https://ghcr-badge.egpl.dev/lazerduck/fuse-inventory/latest_tag?trim=major&label=latest)](https://github.com/lazerduck/fuse-inventory/pkgs/container/fuse-inventory)

<img width="512" height="512" alt="Fuse-inventory" src="https://github.com/user-attachments/assets/2b4cd430-ed63-4f62-af85-3e89470bd0aa" />

**Fuse-Inventory** is a lightweight, self-hosted service inventory and permissions tracker.
It's designed to be self-contained, fast, and frictionless — letting you get set up in seconds.

---

## ✨ Overview

Fuse-Inventory lets small to medium sized development or DevOps teams:

- Track **applications**, their **hosts**, and **dependencies**
- Record **databases, accounts, and permissions**
- Present clear, living documentation of your application setup
- Import and export data as **YAML or JSON**
- Validate and **reverse-import** live SQL permissions
- Generate **SQL GRANT / REVOKE** scripts for restores
- Compare **environments** and see drift at a glance
- Run entirely from a **single Docker container**

It treats applications as first class objects with platforms, dependencies and environments expected to facilitate applications.

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
Fuse-Inventory is in early development.
The core data model and API are established, and a simple UI is present

---

## 🛣️ Roadmap
- Security
  - Authentication & roles
  - Encrypted secret storage
- Deployments
  - Container image and version tracking
  - Environment promotion flows
- Support
  - Schema documentation
  - Data import/export tools
