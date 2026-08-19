# infra/kubernetes

OpenTofu module that provisions the app's Kubernetes-side resources on the
k3s cluster running on the 2 Raspberry Pi nodes: a namespace, a single-replica
Postgres `StatefulSet` (PVC-backed), and the `web-api` `Deployment` (2
replicas, spread across both nodes via pod anti-affinity) + its `Service`.

## Where Postgres runs

The two Pis are not interchangeable: **rb01 has ~29GB, rb02 is a ~7GB SD card**.
The `local-path` provisioner stores a volume on the node's own root filesystem,
so the database competes with container images for that space — rb02 has already
evicted a pod for running out of ephemeral storage.

Postgres is therefore pinned to `var.postgres_node` (default `rb01`) with a
**required** node affinity. Changing that variable after the database has data
means moving the data as well: the PVC does not follow the pod, and a
`local-path` volume is bound to the node that provisioned it.

## Database migrations

The API image is also run as a `Job` (`web-api-migrate-*`) with `--migrate-only`,
which applies the EF Core migrations and exits. The `Deployment` `depends_on` that
Job, so no replica serves traffic against a schema that hasn't been migrated.

This is deliberate rather than migrating on API startup: a broken or slow
migration fails the `apply` loudly, instead of surfacing as replicas that never
become ready while the old ones keep serving.

Kubernetes Jobs are immutable, so the Job name embeds a hash of the image
reference plus `migration_revision`. Since `image_tag` defaults to `latest`, a
deploy that reuses the tag would otherwise keep the old completed Job and skip
the migration entirely. `infra-ci.yml` passes the commit SHA via
`TF_VAR_migration_revision`; set it yourself for a local apply. Re-running is
harmless — EF skips whatever is already in the history table.

To read what a migration did:

```bash
kubectl logs -n poker-game-manager job/web-api-migrate-<hash>
```

Finished Jobs are reaped an hour after completion.

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
- The R2 state backend set up (one-time, see below).

## State backend (Cloudflare R2)

State is stored in a Cloudflare R2 bucket, not on the cluster itself —
deliberately, so a dead/rebuilt Pi doesn't take the record of what was
provisioned down with it. R2 is S3-compatible, so this uses OpenTofu's
standard `s3` backend pointed at R2's endpoint (see `versions.tf`).

One-time setup:

1. Create the bucket: Cloudflare dashboard -> R2 -> Create bucket -> name it
   `poker-game-manager-tfstate` (matches `backend.hcl.example` /
   `infra-ci.yml` — change both if you use a different name).
2. Create an R2 API token: R2 -> Manage R2 API Tokens -> Create API Token ->
   "Object Read & Write", scoped to that bucket. This gives you an Access Key
   ID + Secret Access Key — a *different* credential from `CLOUDFLARE_API_TOKEN`
   (R2 speaks S3's auth, not Cloudflare's usual API token auth). Set these as
   `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` GitHub secrets, and export them
   locally as `AWS_ACCESS_KEY_ID` / `AWS_SECRET_ACCESS_KEY` (the S3 backend
   reads the standard AWS SDK env var names — no R2-specific ones exist).
3. Locally: `cp backend.hcl.example backend.hcl`, fill in the real Cloudflare
   account ID (never commit `backend.hcl` — already gitignored).

## Usage

```bash
cd infra/kubernetes
cp terraform.tfvars.example terraform.tfvars   # fill in real values, never commit this file
cp backend.hcl.example backend.hcl             # fill in your Cloudflare account ID
export AWS_ACCESS_KEY_ID=...                   # the R2 API token's Access Key ID
export AWS_SECRET_ACCESS_KEY=...               # the R2 API token's Secret Access Key
tofu init -backend-config=backend.hcl
tofu plan
tofu apply
```

## Running via CI (.github/workflows/infra-ci.yml)

`plan`/`apply` need to reach the k3s API server at `192.168.100.10:6443`, a
private LAN address — GitHub-hosted runners can't route to it. That job
(`plan-or-apply`) is pinned to a **self-hosted runner** registered on `rb02`
(the k3s agent node, chosen over `rb01`/control-plane so the runner doesn't
compete with etcd/kube-apiserver) instead.

Setup (one-time, on rb02):

1. GitHub repo -> Settings -> Actions -> Runners -> "New self-hosted runner"
   -> pick Linux / ARM64 -> copy the generated commands (they embed a
   short-lived registration token and the current runner version).
2. `ssh zion@192.168.100.11`, run the download/extract/`./config.sh`
   commands from step 1, using `--labels self-hosted,linux,arm64,pi` so the
   workflow's `runs-on: [self-hosted, linux, arm64, pi]` can target it.
3. Install as a systemd service so it survives reboots:
   `sudo ./svc.sh install && sudo ./svc.sh start`.

The workflow only ever triggers this runner on manual `workflow_dispatch`
(never on `pull_request` or `push`) — `apply` changes real infrastructure,
so it needs a human pressing the button with `action: apply` selected, and a
self-hosted runner should never be wired to a fork-triggerable event like
`pull_request` since that would let arbitrary PR code execute on the Pi.
