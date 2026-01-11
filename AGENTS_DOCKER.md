# AGENTS_DOCKER.md

Date: 2026-01-11

Work summary:
- Added three docker-compose files for full stack, backend-only (database), and frontend work (database + API).
- Added helper scripts to select the right compose file via parameters (bash and PowerShell).
- Updated compose port mappings and frontend API base URL to use port 5671.

Files touched:
- `docker-compose.full.yml`
- `docker-compose.backend.yml`
- `docker-compose.frontend.yml`
- `run-compose.sh`
- `run-compose.ps1`
- `docker-compose.yml`
- `fireblocksreplacement-admin/.env`
