# infra/kubernetes

OpenTofu module that provisions the app's Kubernetes-side resources on the
k3s cluster running on the 2 Raspberry Pi nodes: a namespace, a single-replica
Postgres `StatefulSet` (PVC-backed), and the `web-api` `Deployment` (2
replicas, spread across both nodes via pod anti-affinity) + its `Service`.

## What this module does *not* do

- **Doesn't create the Cloudflare Tunnel.** It already exists and runs
  replicated as pods on the Pis, managed outside this repo. This module just
  outputs `web_api_internal_url` — point the tunnel's public hostname at that
  address, then set that public hostname as `API_ORIGIN` in
  `frontend/wrangler.jsonc`.
- **Doesn't create an Ingress.** Traffic reaches `web-api` straight from the
  tunnel to the in-cluster `Service`, no ingress controller involved.
- **Doesn't build or push the API image.** That's `.github/workflows/docker-publish.yml`,
  which publishes to GHCR on every push to `main`.

## Prerequisites

- `tofu` (OpenTofu) installed locally.
- A kubeconfig for the k3s cluster, reachable from wherever you run `tofu apply`.

## Usage

```bash
cd infra/kubernetes
cp terraform.tfvars.example terraform.tfvars   # fill in real values, never commit this file
tofu init
tofu plan
tofu apply
```

State is local for now (no backend configured). Once this is applied for
real, consider moving state somewhere durable (e.g. a bucket) instead of a
local `.tfstate` file living only on one machine.
