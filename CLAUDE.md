# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

FireblocksReplacement is a drop-in replacement for the Fireblocks API designed for testing crypto-trading platforms. It simulates blockchain operations using a fake in-database blockchain for E2E testing without real blockchain interaction.

The system provides:
1. **Fireblocks-compatible API** (22 endpoints) - Identical contract to production Fireblocks
2. **Admin/Test API** - Full control over transaction states, failure simulation, and test automation
3. **Admin UI** - Debugging companion UI for real-time transaction visualization and state management

## Architecture

### Stack
- **Backend**: ASP.NET Core Web API (.NET 8 LTS) with controllers
- **Frontend**: React + TypeScript (Vite)
- **Database**: PostgreSQL with EF Core 10.0.1 (code-first migrations)
- **Deployment**: Docker Compose with per-test database isolation

### Project Structure (Planned)
```
FireblocksReplacement.Api/         # Backend API project
  Controllers/                     # Separate controllers for Fireblocks and Admin APIs
  Services/                        # Business logic layer
  Repositories/                    # Data access layer
  Models/                          # EF Core entities
  Dtos/                           # Data transfer objects

fireblocksreplacement-admin/       # Frontend admin UI (Vite React)
  src/
    components/                    # Reusable UI components
    pages/                         # Top-level page components
    features/                      # Feature-specific modules
    hooks/                         # Custom React hooks
    api/                           # API client code

tests/                            # All tests (not co-located)
  backend/
    unit/
    integration/
  frontend/
    unit/
    e2e/
```

## Common Development Commands

### Backend Commands
```bash
# Build the API
dotnet build

# Run the API locally
dotnet run --project FireblocksReplacement.Api

# Run tests
dotnet test

# Create EF Core migration
dotnet ef migrations add <MigrationName> --project FireblocksReplacement.Api

# Apply migrations
dotnet ef database update --project FireblocksReplacement.Api
```

### Frontend Commands
```bash
cd fireblocksreplacement-admin

# Install dependencies
npm install

# Run dev server
npm run dev

# Build for production
npm run build

# Run tests
npm test
```

### Docker Commands
```bash
# Start full environment
docker-compose up

# Rebuild and start
docker-compose up --build

# Stop and remove containers
docker-compose down
```

## Critical Implementation Rules

### API Contract Compliance
- **Fireblocks-compatible endpoints MUST match the Fireblocks swagger specification exactly**
  - Same HTTP methods, paths, request/response schemas
  - Same error codes and error response structure
  - Same authentication mechanism (API key + JWT signing)
  - Source of truth: `Fireblocks.swagger.yml` in repository root
- Admin API uses a different response format: `{ data, error }` wrapper
- Admin API errors: `{ message, code }` inside `error` field

### Authentication Model
- **Fireblocks-compatible API**: Mirror Fireblocks auth exactly (API key + JWT signing)
- **Admin/Test API**: No authentication (internal test use only)

### Architecture Patterns
- **No internal HTTP calls**: Controllers → Services → Repositories only
- **Single monolith**: Admin and Fireblocks APIs are separate modules within one service
- **Controllers only**: Use traditional controllers, not minimal APIs
- **No WebSockets**: Use polling for UI real-time updates

### Data and Naming Conventions
- **Database**: PascalCase for tables and columns (e.g., `VaultAccount`, `TransactionState`)
- **C# code**: Standard .NET conventions (PascalCase types/methods, camelCase locals/fields)
- **Backend files**: `PascalCase.cs`
- **Frontend files**: `PascalCase.tsx` for components
- **API JSON fields**: camelCase (both Admin API and UI)
- **Foreign keys**: `VaultAccountId`, `TransactionId` (PascalCase, no prefixes)

### State Management
- **Frontend**: React Query for server state, local React state for UI-only concerns
- **No global state libraries**: Avoid Redux, Zustand, etc.
- **Per-view loading states**: No global loading spinner

### Testing Patterns
- **Test location**: All tests under `tests/` directory, not co-located with source
- **Backend tests**: `tests/backend/unit/` and `tests/backend/integration/`
- **Frontend tests**: `tests/frontend/unit/` and `tests/frontend/e2e/`

### Configuration
- Environment variables via `.env` files at repository root
- ASP.NET configuration via `appsettings*.json`
- Use dotenv.net 4.0.0 for .env file loading

## Transaction State Machine

The system implements a complete 11-state transaction lifecycle:

**Success path**:
`SUBMITTED → PENDING_AUTHORIZATION → PENDING_SIGNATURE → QUEUED → BROADCASTING → CONFIRMING → COMPLETED`

**Failure states**:
`FAILED`, `REJECTED`, `CANCELLED`, `TIMEOUT`

Admin API provides explicit state transition endpoints for deterministic testing.

## Key Dependencies

### Backend
- Microsoft.EntityFrameworkCore 10.0.1
- Npgsql.EntityFrameworkCore.PostgreSQL 10.0.0
- Swashbuckle.AspNetCore 10.1.0
- Serilog.AspNetCore 10.0.0
- dotenv.net 4.0.0

### Frontend
- @tanstack/react-query 5.90.16 (server state)
- @tanstack/react-table 8.21.3 (data tables)
- react-router-dom 7.12.0 (routing)
- @radix-ui/react-dialog 1.1.15 (UI primitives)

## Important Design Decisions

### Deterministic Testing Focus
- No rate limits (tests run at maximum speed)
- No caching initially (keeps behavior deterministic)
- Single instance deployment (no horizontal scaling)
- Admin API for scripted test scenarios, not just manual UI

### Error Handling
- Centralized exception middleware
- Maps internal exceptions to Fireblocks error format for compatible endpoints
- Separate error format for Admin API

### Validation
- Data Annotations only (avoid FluentValidation)
- Validate at system boundaries (user input, external APIs)
- Trust internal code and framework guarantees

## Avoid
- Adding unnecessary abstractions or premature optimization
- Global state management libraries
- Rate limiting or caching (unless explicitly approved)
- Co-locating tests with source code
- Minimal APIs (use controllers instead)
- Internal HTTP calls between services
