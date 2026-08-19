# infra/kubernetes

OpenTofu module that provisions the app's Kubernetes-side resources on the
k3s cluster running on the 2 Raspberry Pi nodes: a namespace, a single-replica
Postgres `StatefulSet` (PVC-backed), and the `web-api` `Deployment` (2
replicas, spread across both nodes via pod anti-affinity) + its `Service`.

## Which image runs

The Deployment is pinned to an image **digest**, never a tag.

A tag is not a version. Pinned to `:latest`, the Deployment spec was byte-for-byte
identical between builds, so Tofu saw no change and Kubernetes never recycled the
pods — an `apply` reported success while the API kept serving the previous build,
and every release needed a manual `kubectl rollout restart`. That happened twice
before it was fixed. A digest names exactly one image, so a new build *is* a spec
change, `tofu output deployed_image` answers "what is actually running", and a
rollback is just applying an older digest.

`infra-ci.yml` resolves the digest for you: dispatch it with an `image_tag`
(default `latest`, or `sha-<short-sha>` to go back to a specific build) and the
`resolve-image` job turns that into a digest before anything is applied. If the
tag doesn't exist the run fails there, before touching the cluster.

Note that `docker-publish.yml` only builds when `backend/src` changes, so a commit
that touched only the frontend or infra has no image of its own — deploy `latest`,
or the `sha-` tag of the last backend build.

To resolve one by hand for a local apply:

```bash
docker login ghcr.io                       # username + a PAT with read:packages
docker buildx imagetools inspect \
  ghcr.io/spicyferret/poker-game-manager-api:latest \
  --format '{{.Manifest.Digest}}'
```

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

Kubernetes Jobs are immutable, so the Job name embeds part of the image digest: a
new image gets a new Job, and re-applying the same image reuses the existing one.
Re-running is harmless either way — EF skips whatever is already in the history
table.

Finished Jobs are reaped after 24 hours. Within a day of iterating, re-applying
the same image finds the Job still there and plans clean; after that the next
apply recreates it and the migration re-runs, which costs seconds and changes
nothing.

To read what a migration did:

```bash
kubectl logs -n poker-game-manager job/web-api-migrate-<hash>
```

## What this module does *not* do

- **Doesn't create the Cloudflare Tunnel, or its routes.** The tunnel already
  exists, runs replicated as pods on the Pis, and takes its configuration
  remotely from Cloudflare. This module only outputs `web_api_internal_url`.

  This one is a deliberate limit rather than an omission. The Cloudflare
  provider has **no additive resource** for a tunnel's public hostnames:
  `cloudflare_zero_trust_tunnel_cloudflared_route` handles private network CIDRs,
  and the only ingress resource,
  `cloudflare_zero_trust_tunnel_cloudflared_config`, replaces the *entire* rule
  list. Declaring our single route here would therefore make this repo the owner
  of every other route on that tunnel — `portainer`, `grafana`, `prometheus`,
  `k8s` and the `personal-website` API — and a bad apply from a poker deploy
  would take them down.

  So the route is added by hand (Zero Trust → Networks → Tunnels → published
  application routes → `HTTP`, `web-api.poker-game-manager.svc.cluster.local:80`;
  HTTP, not HTTPS, because the API listens in plain text in-cluster), and
  `.github/workflows/tunnel-route-check.yml` verifies weekly that it is still
  there and still points at the right Service. That check is **read-only** — see
  the header of `infra/scripts/verify-tunnel-route.sh`. It needs the API token to
  carry `Account > Cloudflare Tunnel > Read`.
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
