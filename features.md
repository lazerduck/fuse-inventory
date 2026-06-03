# Fuse-Inventory Features

A comprehensive list of features and capabilities in Fuse-Inventory.

---

## 📦 Inventory Items

### Applications
- Track applications with name, version, description, owner, framework, and repository URI
- Associate custom icons with applications
- Link applications to pipelines and CI/CD information
- Apply tags for organisation and filtering
- Full create, edit, and delete support

### Application Instances
- Represent an application deployed into a specific environment
- Associate instances with a platform (server, cluster, container host, etc.)
- Record base URL, health check URL, and OpenAPI/Swagger URL
- Track the deployed version per instance
- Apply tags to instances independently of the parent application

### Dependencies
- Link an application instance to any of the following target types:
  - Another application instance
  - A data store
  - An external resource
  - A message broker
- Record the port used by the dependency
- Specify authentication method: Account-based, Identity-based, or None
- Link an Account or Identity to a dependency for credential tracking
- View and manage all dependencies from the instance edit page
- Inline dependency table for fast editing without leaving the page
- Navigate directly to any dependency target from the dependency row

### Accounts
- Track credentials and access configurations for dependencies
- Support 8 authentication kinds:
  - API Key
  - Password
  - Bearer Token
  - Basic Auth
  - Connection String
  - Client Credentials
  - Certificate
  - Custom
- Store a username alongside the credential
- Attach arbitrary parameters for additional credential metadata
- Scope accounts to a target type (Application, DataStore, ExternalResource, MessageBroker)
- Associate accounts with a specific target instance
- Apply database-level grants with schema and privilege detail:
  - Privileges: Select, Insert, Update, Delete, Execute, Connect, Alter, Control
- Use secret bindings to store credentials via Azure Key Vault or a plain text reference
- Clone accounts to different targets to quickly replicate configurations

### Identities
- Track non-credential identity types used for access:
  - Azure Managed Identity
  - Kubernetes Service Account
  - AWS IAM Role
  - Custom
- Assign identities to target resources with a role and notes
- Scope identities globally or to a specific application instance
- Clone identities across environments or instances

### Data Stores
- Track databases and storage services used by applications
- Record connection URI, environment, and platform
- Support multiple data store kinds (SQL, Redis, etc.)
- Apply tags for filtering and organisation

### External Resources
- Track third-party services not hosted internally (e.g., payment providers, email platforms)
- Store a resource URI for reference
- Apply tags for filtering and organisation

### Message Brokers
- Track messaging infrastructure such as RabbitMQ, Azure Service Bus, etc.
- Record connection URI and associated environment
- Document individual queues with name and description
- Document topics with name, description, and subscriber list
- Apply tags for filtering and organisation

### Environments
- Top-level groupings for deployments (e.g., `dev`, `test`, `live`)
- Configure URI templates for automatic instance URL generation:
  - Base URI template
  - Health check URI template
  - OpenAPI URI template
- Enable auto-create instances: when a new application is added, instances are automatically created in each environment that has this setting enabled

### Platforms
- Track the infrastructure that instances run on
- Support four platform kinds: Server, Cluster, Serverless, ContainerHost
- Record DNS name, IP address, operating system, and notes
- Apply tags for filtering and organisation

### Tags
- Apply coloured labels to almost any entity type
- 8 colour options for visual categorisation
- Filter pages by tag across the application

### Positions
- Represent organisational roles or job titles
- Used as owners and approvers in responsibility assignments and risk records
- Apply tags for organisation

### Responsibility Types
- Define categories of responsibility (e.g., "On-Call", "Deployment Owner")
- Reference responsibility types when creating responsibility assignments

### Responsibility Assignments
- Assign a position (person/role) a responsibility type for a specific application
- Scope assignments to all environments or a single environment
- Mark a primary assignee
- Add notes for context
- Full audit trail of changes

### Risks
- Track risks associated with specific inventory items
- Record title, description, impact (Low → Critical), likelihood (Low → High)
- Track risk status through its lifecycle: Identified → Assessed → Mitigated → Accepted → Closed
- Assign an owner position and an approver position
- Write mitigation plans and record review and approval dates
- Link risks to specific targets:
  - Application
  - Application Instance
  - Dependency
  - Data Store
  - Account
  - External Resource
- Add notes and apply tags for organisation

---

## 🔍 Navigation & UI Features

### Dashboard / Home Page
- Overview cards showing counts of applications, instances, data stores, and external resources
- Filter the dashboard by tag and environment simultaneously
- Quick-access cards for each application instance and its health status
- Direct navigation from dashboard cards to edit pages

### Faster Navigation on Instance Pages
- Inline dependency table on the instance edit page — add, edit, and remove dependencies without navigating away
- Clickable dependency rows link directly to the target resource edit page
- Quick-access URL links open base, health, and OpenAPI URLs from the instance page

### Grid / Table Views
- Paginated, sortable tables for all major entity types:
  - Applications, Accounts, Identities, Data Stores, External Resources, Message Brokers, Platforms, Environments, Tags, Positions, Risks, Responsibility Types, and more
- Column-based display with action buttons inline
- Text search and filter controls on every list page

### Read-Only / Documentation Mode
- Public-facing documentation pages requiring no login (when security allows)
- Shareable URLs for each entity
- Global search across all entity types from a single search bar
- Safe display that excludes sensitive credential values
- Entity types covered: Applications, Instances, Data Stores, Accounts, Identities, External Resources, Message Brokers, Platforms, Dependencies

### Graph Visualisation
- Interactive dependency graph powered by Cytoscape
- Force-directed layout (fcose algorithm)
- Click nodes to highlight an application and all its relationships
- Pan and zoom controls
- Real-time updates as inventory changes

---

## 🔐 Security & Access Control

### Authentication Modes
- **Open** — anyone can view and edit without logging in
- **Read-Only** — viewers can browse freely; only admins can make changes
- **Locked-Down** — login required to view any data

### JWT-Based Authentication
- Login with username and password to receive a JWT session token
- Automatic session expiry
- Logout endpoint to invalidate sessions

### Password Security
- Passwords hashed with PBKDF2/SHA-256 and 100,000 iterations
- Timing-safe comparison during verification
- Password reset functionality (admin-initiated)

### Roles and Permissions
- Two built-in roles: **Admin** and **Reader**
- Create custom roles with fine-grained permission assignment
- Assign multiple roles to a single user
- 30+ individual permission types including:
  - Read/Create/Update/Delete for each entity type
  - View and manage audit logs
  - Import and export configuration
  - Manage users and roles
  - Access Azure Key Vault secret operations
  - Manage SQL integrations
  - Manage Kuma integrations

---

## 🔗 Integrations

### Uptime Kuma Health Monitoring
- Connect one or more Uptime Kuma instances
- Scope integrations to specific environments
- Associate an account for authenticated access
- Display real-time health status on the home dashboard
- Dedicated Kuma Dashboard page for an at-a-glance health overview
- Health data is cached and refreshed on demand

### Azure Key Vault
- Integrate with one or more Azure Key Vault instances
- Two authentication modes: Managed Identity, Client Secret
- Four configurable capabilities per integration: Check, Create, Rotate, Read
- Browse secrets stored in the vault
- Create new secrets directly from Fuse
- Rotate existing secrets with a generated password
- Reveal secret values (admin-only)
- Reference Key Vault secrets from Account secret bindings

### Azure App Configuration
- Integrate with one or more Azure App Configuration instances
- Two authentication modes: Managed Identity, Client Secret (including shared manager credentials)
- Browse key-values from App Configuration within Fuse
- Search keys and filter by key prefix and label
- View key metadata: key, value/type, label, content type, last modified date, and locked status
- Key Vault reference entries are explicitly identified and not shown as plain values
- App config can be associated with an app instance and key suffix to display the config

### SQL Permission Management
- Connect to SQL databases and analyse current permissions
- Compare expected permissions (from Account grants) against actual database permissions — drift detection
- Detect and display orphan principals (database users with no matching Fuse account)
- Create missing SQL accounts directly from Fuse
- Apply or update permissions to resolve drift for individual accounts
- Bulk resolve: apply all outstanding permission changes in one action
- Import permissions from existing database users into Fuse accounts
- Import orphan principals as new Fuse accounts
- SQL permission status is cached per account and refreshed on demand
- Connection testing before saving integration configuration

---

## ⚙️ Configuration & Data Management

### Export and Import
- Export the full configuration as JSON or YAML with a timestamped filename
- Download a blank YAML/JSON template as a starting point
- Import configuration in JSON or YAML format
- Merge import: existing records are updated and new records are added

### In-Memory + File-Based Storage
- All data is held in memory for fast reads
- Changes are written back to JSON files in `/app/data/` for persistence
- 18 separate JSON files, one per entity type
- Data is loaded from disk on startup and served from memory during runtime
- Thread-safe writes using a semaphore mutex

### Audit Log Storage
- Audit records are stored in an embedded LiteDB database (separate from the JSON files)
- Supports independent querying and pagination without loading into memory

### Password Generator
- Built-in configurable password generator
- Set the allowed character set and desired length (default 32 characters)
- Generate passwords via the UI or API
- Configuration is persisted

---

## 🧪 Audit & Compliance

### Comprehensive Audit Trail
- Every create, update, delete, login, logout, password change, import, export, and secret operation is logged
- 100+ distinct audit action types
- Each record captures: timestamp (UTC), action type, area/category, username, user ID, entity ID, and change details (JSON diff)

### Audit Log Search
- Filter by date range, action type, area, username, entity ID, or free-text search on change details
- Pagination with configurable page size
- View full change details for any audit entry

---

## 🚀 Deployment

### Single-Container Deployment
- The API and the Vue 3 frontend are bundled into a single Docker image
- Multi-stage Dockerfile: Node 20 Alpine builds the frontend, .NET 10 SDK compiles the API, .NET 10 AspNet serves both
- Frontend is served as a static SPA from the API process — no separate web server needed
- Published to GitHub Container Registry (`ghcr.io/lazerduck/fuse-inventory`)

### Docker / Docker Compose
- Pull and run with a single `docker run` command
- Mount a local directory to `/app/data` for persistent storage
- `docker-compose.yml` included for one-command start with volume and restart policy pre-configured
- Demo environment available via the `test-data/` directory

### Demo / Test Data
- A pre-configured demo dataset is included in `test-data/`
- Covers accounts, applications, environments, data stores, and more
- Default demo login: **Admin / Password**

---

## 🛠️ Tech Stack Summary

| Layer | Technology |
|---|---|
| API | .NET 10, ASP.NET Core |
| Storage | In-memory + JSON files + LiteDB (audit) |
| Frontend | Vue 3, Quasar, TypeScript, Vite |
| State management | Pinia, TanStack Vue Query |
| HTTP client | Axios |
| Graph visualisation | Cytoscape, cytoscape-fcose |
| Guided tours | Driver.js |
| Utilities | VueUse, Zod |
| Containerisation | Docker, Docker Compose |
| CI/CD | GitHub Actions |
