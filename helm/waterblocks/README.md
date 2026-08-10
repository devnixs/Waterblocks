# Waterblocks Helm chart

Deploys PostgreSQL, the .NET API, and the Admin UI on Kubernetes.

## Prerequisites

- Kubernetes cluster and Helm 3
- API and Admin images from GHCR (after a push to `main`) or built locally:
  - `ghcr.io/devnixs/waterblocks/api:latest`
  - `ghcr.io/devnixs/waterblocks/admin:latest`

Local images (kind example):

```bash
docker build -t ghcr.io/devnixs/waterblocks/api:latest -f Waterblocks.Api/Dockerfile .
docker build -t ghcr.io/devnixs/waterblocks/admin:latest -f waterblocks-admin/Dockerfile waterblocks-admin
kind load docker-image ghcr.io/devnixs/waterblocks/api:latest ghcr.io/devnixs/waterblocks/admin:latest
```

## Install from Git (local chart)

```bash
helm lint helm/waterblocks
helm upgrade --install waterblocks helm/waterblocks \
  --namespace waterblocks \
  --create-namespace
```

## Install from GHCR (OCI)

On each push to `main` that changes `helm/`, CI packages the chart and publishes it to:

`oci://ghcr.io/devnixs/Waterblocks/charts/waterblocks`

Check the chart version in `helm/waterblocks/Chart.yaml`, then:

```bash
helm upgrade --install waterblocks \
  oci://ghcr.io/devnixs/waterblocks/charts/waterblocks \
  --version 0.1.1 \
  --namespace waterblocks \
  --create-namespace
```

GHCR chart URLs are case-insensitive; use the repository owner/name that matches your fork.

If the package is private, run `helm registry login ghcr.io` with a token that has `read:packages`. For public installs, set the GitHub package visibility to **public** (Packages → waterblocks chart → Package settings).

When `namespace.create` is `false` (default), create the target namespace with Helm’s `--create-namespace` flag. Set `namespace.create: true` only if you want the chart to render a Namespace object (do not flip back to `false` on upgrade — Helm may delete the namespace).

## Configuration

| Value | Description |
|-------|-------------|
| `namespace.create` | Render a Namespace resource (default `false`; use `--create-namespace` instead) |
| `postgres.enabled` | Deploy bundled PostgreSQL (default `true`) |
| `postgres.auth.*` | Bundled DB credentials (**dev defaults only** in the public chart) |
| `postgres.auth.existingSecret` | Use a pre-created Secret instead of chart-generated credentials |
| `externalDatabase.*` | Required when `postgres.enabled: false` — prefer `existingSecret` over inline passwords |
| `api.image.*` / `admin.image.*` | Container images and tags |
| `admin.config.apiBaseUrl` | Browser-visible API URL; defaults to `http://<ingress.hosts.api>` when Ingress is enabled |
| `ingress.enabled` | Enable HTTP Ingress (default `true`) |
| `ingress.hosts.admin` / `ingress.hosts.api` | Ingress hostnames |

Port-forward without Ingress:

```bash
helm upgrade --install waterblocks helm/waterblocks \
  --namespace waterblocks --create-namespace \
  --set ingress.enabled=false \
  --set admin.config.apiBaseUrl=http://localhost:5671
```

External PostgreSQL (no bundled Postgres). Avoid `--set externalDatabase.password` in shared shells; use a Secret:

```bash
kubectl -n waterblocks create secret generic my-waterblocks-db \
  --from-literal=DefaultConnection='Host=...;Port=5432;...'
helm upgrade --install waterblocks helm/waterblocks \
  --namespace waterblocks --create-namespace \
  --set postgres.enabled=false \
  --set externalDatabase.existingSecret=my-waterblocks-db
```

```bash
helm upgrade --install waterblocks helm/waterblocks \
  --namespace waterblocks --create-namespace \
  --set postgres.enabled=false \
  --set externalDatabase.existingSecret=my-waterblocks-db
```

## Release

Bump `version` in `Chart.yaml` before merging chart changes to `main` (GHCR rejects overwriting an existing chart version).

CI workflow `.github/workflows/helm-chart.yml` runs `helm lint` on pull requests and publishes to GHCR on `main`. You can also trigger **Helm chart** manually via workflow_dispatch.

```bash
helm package helm/waterblocks
```
