# Copilot Instructions for Fuse-Inventory

## Project Overview

**Fuse-Inventory** is a self-hosted application inventory and environment tracker designed for teams that want visibility without overhead. It helps teams describe what they have deployed, where it is deployed, and what dependencies, permissions, and accounts each system needs.

### Core Purpose
- Map applications, environments, and platforms
- Record dependencies, databases, and accounts with linked grants and roles
- Capture how systems actually work — not just where they run
- Import/export everything as simple YAML or JSON
- Visualize applications and dependencies in a graph view
- Track changes with comprehensive audits

### Integration Capabilities
- **Uptime-Kuma**: Display health information
- **Azure Key Vault**: Assign, create, update, and view secrets based on permissions

## Tech Stack

### Backend (API)
- **.NET 10**: Modern C# API
- **LiteDB**: Audit storage
- In-memory data model with write-back to disk as JSON files
- RESTful API architecture

### Frontend (UI)
- **Vue 3**: Modern JavaScript framework
- **Quasar**: Component library and build framework
- **Vue Router**: Client-side routing
- **Pinia**: State management
- **TanStack Query**: Data fetching and caching
- **Axios**: HTTP client
- **Zod**: Schema validation

### Infrastructure
- Single-container Docker deployment
- Persistent data volume for JSON files and audit database
- Port 8080 default

## Repository Structure

```
fuse-inventory/
├── API/                      # Backend .NET solution
│   ├── Fuse.API/            # Web API project
│   ├── Fuse.Core/           # Core business logic
│   ├── Fuse.Data/           # Data access layer
│   └── Fuse.Tests/          # Unit and integration tests
├── UI/                      # Frontend Vue application
│   └── Fuse.Web/            # Vue 3 + Quasar web UI
├── test-data/               # Demo/testing data (JSON files)
│   ├── accounts.json        # Mock accounts with credentials
│   ├── applications.json    # Sample applications
│   ├── environments.json    # Sample environments
│   └── ...                  # Other mock entities
├── .github/
│   └── workflows/
│       └── ci.yml           # CI/CD pipeline
├── Dockerfile               # Container build configuration
├── docker-compose.yml       # Local development orchestration
├── fuse-invetory.sln       # .NET solution file
└── nswag.json              # API client generation config
```

## Development Workflow

### Building the Backend
1. Restore dependencies: `dotnet restore`
2. Build solution: `dotnet build --configuration Release`
3. Run tests: `dotnet test --configuration Release --verbosity normal`

### Building the Frontend
1. Navigate to UI directory: `cd UI/Fuse.Web`
2. Install dependencies: `npm install`
3. Development server: `npm run dev`
4. Build for production: `npm run build`

### Running with Docker
- Development: `docker-compose up -d`
- Production: Use the prebuilt image from `ghcr.io/lazerduck/fuse-inventory:latest`

### Testing
- Backend tests are in `API/Fuse.Tests/`
- Run with code coverage: `dotnet test --collect:"XPlat Code Coverage"`
- Coverage reports are generated in `./coverage/`
- CI pipeline enforces test success before merging

## Coding Standards and Best Practices

### General Principles
- **Minimal changes**: Make surgical, precise modifications
- **No unnecessary refactoring**: Don't fix unrelated issues
- **Security first**: Always validate changes don't introduce vulnerabilities
- **Test coverage**: Maintain or improve existing test coverage
- **Documentation**: Update docs when they're directly related to changes

### Backend (.NET)
- Follow standard C# conventions (PascalCase for public members, camelCase for private)
- Use async/await for I/O operations
- Implement proper error handling and logging
- Keep business logic in `Fuse.Core`, data access in `Fuse.Data`
- Use dependency injection for services
- Write unit tests for new functionality

### Frontend (Vue)
- Use Composition API (not Options API)
- TypeScript-style code with proper type checking via Zod where needed
- Component file naming: PascalCase (e.g., `MyComponent.vue`)
- Keep components focused and single-responsibility
- Use Pinia for shared state
- Use TanStack Query for server state management
- Follow Quasar component patterns and conventions

### Git Commit Messages
- Use conventional commits format: `type: description`
- Types: `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`
- Keep messages concise but descriptive

## Security Considerations

### Important Security Notes
- Fuse runs entirely inside your environment — security is the operator's responsibility
- Secrets are stored on disk (no local encryption)
- Built-in JWT-based authentication with three modes:
  - Open mode: anyone can view and edit
  - Read-only mode: only admins can make changes
  - Locked-down mode: login required to view anything
- Privileged actions (e.g., Azure Key Vault access) always require admin access

### When Making Changes
- Never commit secrets or credentials
- Validate that changes don't expose sensitive data
- Be cautious with authentication and authorization logic
- Consider the security implications of file I/O operations
- Test security-related changes thoroughly

## Data Model

Key entities in Fuse-Inventory:
- **Environments**: Top-level groupings (dev, test, live)
- **Applications**: Codebases or hosted products
- **Instances**: An application deployed into an environment
- **Dependencies**: Links to other applications, datastores, or external services
- **Accounts**: Credentials associated with dependencies
- **Datastores**: SQL, Redis, RabbitMQ, etc., scoped to an environment
- **External Resources**: Third-party services
- **Platforms**: Servers or container platforms (optional)
- **Tags**: Flexible labels applicable to most entities

## Common Tasks

### Adding a New API Endpoint
1. Define the model in `Fuse.Core`
2. Implement the endpoint in `Fuse.API/Controllers`
3. Add data access logic in `Fuse.Data` if needed
4. Write tests in `Fuse.Tests`
5. Update NSwag config if needed for client generation

### Adding a New UI Feature
1. Create component in `UI/Fuse.Web/src/components` or page in `pages`
2. Add route if needed in router configuration
3. Use TanStack Query for API calls
4. Follow Quasar component patterns
5. Test in development mode

### Fixing a Bug
1. Write a failing test that reproduces the bug
2. Make the minimal fix to pass the test
3. Verify no regressions with full test suite
4. Update documentation if behavior changed

## CI/CD Pipeline

The GitHub Actions workflow (`.github/workflows/ci.yml`) automatically:
1. Runs tests with code coverage on every PR
2. Posts coverage reports as PR comments
3. Builds and pushes Docker images on main branch pushes
4. Publishes to GitHub Container Registry

## Philosophy

Fuse-Inventory is built to enable users to work the way *they* want. It doesn't enforce "best practices" or block risky actions. This philosophy should guide development:
- Don't add unnecessary validation that restricts user choice
- Provide warnings and information, but allow users to proceed
- Focus on flexibility and power-user features
- Document risks, but don't prevent actions

## Test Data

The `test-data/` directory contains a complete mock environment for demonstration and testing:
- **Demo Account**: Username `Admin`, Password `Password`
- Includes sample configurations for all major entities: accounts, applications, environments, datastores, external resources, identities, platforms, risks, etc.
- Can be used for manual testing, demos, and developing new features
- JSON files in this directory are automatically loaded when the application starts

## Notes for Copilot

- This is an active project with a stable core data model
- The backend targets .NET 10 (very recent, ensure SDK is available)
- The UI uses Vue 3 Composition API exclusively
- The project values simplicity and self-hosting over complex architectures
- When in doubt, favor minimal changes that maintain backward compatibility
- Test data with demo credentials (Admin/Password) is available in the `test-data/` folder for testing and demos
