# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) insofar as version numbers are concerned.

---

## [Unreleased]

### Added
- Health check endpoints: `/api/health/live` (liveness), `/api/health/ready` (readiness), and `/api/health/status` (detailed report)
- License management: validation, storage, and UI components with customer name support
- Button to direct users to the licensing page when license is invalid

### Changed
- Updated license validation logic to include 'valid' status

### Fixed
- Simplified LiteDB health check from magic-byte inspection to file accessibility check

---

## [v0.1.0] — 2026-06-28

*First release of Fuse-Inventory.*

The following is a reverse-chronological list of major features and changes as they were merged.

### Added

**SQL Permission Management & Drift Detection**
- SQL permission overview page with drift detection between expected (grant) and actual database permissions
- Batch SQL permissions inspection with single round-trip for performance
- Bulk resolve: apply all outstanding permission changes in one action
- Resolve drift for individual accounts with confirmation dialogs
- Automatic SQL account creation from Fuse when missing in the database
- SQL status visible on the account edit screen
- SQL permissions caching with background worker service
- Import permissions from existing database users into Fuse accounts
- Import orphan principals as new Fuse accounts
- Connection testing before saving SQL integration configuration
- Multi-architecture Docker builds (amd64 + arm64)

**Read-Only / Documentation Views**
- Full read-only documentation mode (no login required when security allows)
- Read-only pages for: Home, Applications, Instances, Data Stores, Accounts, Identities, External Resources, Message Brokers, Platforms, Dependencies, Risks, Tags
- Global search across all entity types from a single search bar
- Read-only shell layout with three-column structure
- Read-only placeholder content component

**Azure Integrations**
- Azure Key Vault integration: browse, create, rotate, and read secrets
- Secret explorer with reveal values (admin-only)
- Secret operations: create, rotate, reveal, and reference from Account secret bindings
- Azure App Configuration integration: browse key-values, search by prefix and label
- App Configuration entry editor with create/edit UI
- Azure Integration Manager for shared client secret credentials management
- Centralized management of Azure integration client secrets across multiple vaults

**Security Overhaul**
- Role-based access control system with built-in Admin and Reader roles
- 30+ individual permission types for fine-grained access control
- Custom roles with permission assignment via UI
- User account management with role assignment UI
- Permission-based security middleware with `[RequirePermission]` attributes
- Decentralized security model across all controller actions
- Permission catalog validation on startup
- API key management for programmatic access (create, regenerate, delete)
- API key audit logging
- Password reset with audit logging
- Password generator with configurable charset and length (default 32 chars)
- AppSettings management with API endpoints and permissions
- Documentation Completeness page showing entities with missing critical info
- Site-wide security level override (Open / Read-Only / Locked-Down)

**Blast Radius & Risk**
- Blast Radius Preview page showing the impact surface of a single application
- Risk tracking: title, description, impact (Low → Critical), likelihood (Low → High)
- Risk status lifecycle: Identified → Assessed → Mitigated → Accepted → Closed
- Risk ownership and approval workflow
- Risk linkable to: Application, Instance, Dependency, Data Store, Account, External Resource
- Risk View and Tag View components for detailed risk information

**Activity & Undo**
- Activity history tracking (all CRUD operations logged)
- Undo functionality with permission checks
- Version history tracking with SnapshotChangeTracker and LiteDB integration
- Comprehensive audit log viewer with filtering by date, action type, area, username, entity ID

**Message Brokers**
- Message broker model, service, controller, and tests
- Frontend: page, form, readonly view, graph support, navigation
- Queue and topic documentation with subscriber lists
- Default kind options: RabbitMQ, Azure Service Bus

**Organizational Ownership**
- Positions (job titles / roles) model, service, controller
- Responsibility Types (categories like "On-Call", "Deployment Owner")
- Responsibility Assignments: assign positions to types for applications
- Tag support across the board

**Onboarding & UX**
- Driver.js integration for guided onboarding tour
- Onboarding store and tour controls
- Onboarding cheat sheet dialog and tour anchors
- Dark mode toggle with styling overhaul
- Custom application icons (8 preset icons + upload)
- Environment URI templates for automatic instance URL generation
- Auto-create instances when new applications are added

**Graph & Visualization**
- Interactive dependency graph with Cytoscape + force-directed layout
- Transitive dependency highlighting and risk overlay
- Double-click navigation to dependency details
- Click-to-highlight relationships
- Graph view node filtering with interactive selection

**Uptime-Kuma Integration**
- Kuma health check monitoring system with URL-persisted filters
- Kuma Service Health Dashboard with 20s auto-refresh
- Multiple Kuma instance support with environment scoping
- Real-time health status on home dashboard

**Data Management**
- Import/export configuration as JSON or YAML
- Merge import: existing records updated, new records added
- Blank YAML/JSON templates for starting fresh

**Infrastructure**
- Promotional website with Vue 3, Quasar, and Pinia
- Website pages: Home, Features, Screenshots
- SEO: meta tags, sitemap.xml, Open Graph, Twitter cards, robots.txt
- Website deployment via Docker + nginx
- Demo environment with pre-configured test data

**Dependency & Navigation**
- Severity field and reverse dependencies in dependency view
- Transitive dependency highlighting
- Inline editable dependency table (replaces modal)
- Reusable InventoryNavigator component for fast app instance navigation
- "Add another" checkbox for rapid data entry
- Database selection when adding accounts to datastores

**Data Model**
- Identities model: Azure Managed Identity, Kubernetes Service Account, AWS IAM Role, Custom
- Clone identities across environments or instances
- Clone accounts to different targets
- Dependency management in identity assignments
- Environment-level environment filtering on dashboard
- Environment automation: apply URI templates to existing instances

**UI & Development**
- Playwright UI test automation suite with CI workflow
- Comprehensive service-level unit tests
- Cobertura code coverage reporting in CI with PR comments
- NSwag auto-generated TypeScript and C# API clients
- Copilot instructions and setup steps for AI-assisted development
- Multiple refactoring passes for code structure and readability

### Changed
- Refactored "Server" model to "Platform" across the application
- Refactored "Secret Provider" terminology to "Azure Integration" across components and permissions
- Updated permission constants to use lowercase and consistent naming conventions
- Upgraded from .NET 9 to .NET 10
- Improved performance: optimized store operations, batched SQL queries

### Fixed
- XSS vulnerability in confirmation dialog
- SQL injection in SQL account creation (parameterized passwords)
- Tempdb LOB allocation in SQL permissions batch query
- Collation conflict in UNION ALL queries
- Key Vault Check capability always false (string enum mismatch)
- Key Vault rotate error (SecretName from route vs body)
- Auto-create instances for new applications
- Redeploy logout issue (sessions persisted to store)
- Site-wide security level not overriding user permissions
- Race condition and safety checks in security middleware
- Test isolation issues with consistent admin credentials
- Instance creation bug preventing immediate navigation to edit page

### Deprecated / Removed
- Removed the marketing website from the main codebase (moved to private branch)
- Legacy role checks removed from business logic (migrated to role-based system)
- Unused files removed from multiple areas