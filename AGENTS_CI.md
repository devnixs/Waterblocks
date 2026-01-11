# AGENTS_CI.md

Date: 2026-01-11

Work summary:
- Added GitHub Actions CI workflow to run .NET tests when test projects exist (otherwise build the API), build the frontend, and build Docker images for backend and frontend.
- Added frontend Dockerfile for building and serving the Vite app via nginx.
- Added frontend .dockerignore to keep images lean.
- Added nginx config for SPA routing in the admin UI image.

Files touched:
- `.github/workflows/ci.yml`
- `fireblocksreplacement-admin/Dockerfile`
- `fireblocksreplacement-admin/.dockerignore`
- `fireblocksreplacement-admin/nginx.conf`
