variable "kubeconfig_path" {
  description = "Path to the kubeconfig file for the k3s cluster running on the Raspberry Pi nodes."
  type        = string
}

variable "namespace" {
  description = "Kubernetes namespace the app is deployed into."
  type        = string
  default     = "poker-game-manager"
}

variable "storage_class_name" {
  description = "StorageClass used for the Postgres PVC. k3s ships 'local-path' by default."
  type        = string
  default     = "local-path"
}

variable "postgres_db" {
  description = "Postgres database name."
  type        = string
  default     = "poker-game-manager"
}

variable "postgres_user" {
  description = "Postgres user."
  type        = string
  default     = "postgres"
}

variable "postgres_password" {
  description = "Postgres password."
  type        = string
  sensitive   = true
}

variable "postgres_node" {
  description = <<-EOT
    Node the Postgres pod is pinned to. 'local-path' volumes live on the node's own
    root filesystem, so this decides which disk the database grows on — and the two
    Pis are not equivalent (rb01 ~29GB, rb02 ~7GB SD card). Changing this after the
    database has data means moving the data too; the PVC does not follow the pod.
  EOT
  type        = string
  default     = "rb01"
}

variable "postgres_storage_size" {
  description = "Size of the Postgres PVC."
  type        = string
  default     = "4Gi"
}

variable "jwt_secret" {
  description = "Signing secret for the API's JWT issuer."
  type        = string
  sensitive   = true
}

variable "image_repository" {
  description = "Container image repository for the API, published by .github/workflows/docker-publish.yml."
  type        = string
  default     = "ghcr.io/spicyferret/poker-game-manager-api"
}

variable "image_digest" {
  description = <<-EOT
    Digest of the API image to run, as "sha256:...".

    A digest rather than a tag, on purpose. A tag is not a version: republishing
    'latest' leaves the Deployment spec identical, so Tofu sees no change and
    Kubernetes never recycles the pods - a deploy that looks successful while the
    old code keeps serving, which needed a manual rollout restart every time. A
    digest names exactly one image, so what runs is identifiable from state alone
    and a rollback is just applying an older one.

    infra-ci.yml resolves this from a tag before applying. To resolve one by hand,
    see the "Which image runs" section of README.md.
  EOT
  type        = string

  validation {
    condition     = can(regex("^sha256:[0-9a-f]{64}$", var.image_digest))
    error_message = "image_digest must be a full digest: sha256: followed by 64 hex characters."
  }
}

variable "api_replicas" {
  description = "Number of web-api replicas. Set to 2 to spread across both Pi nodes via pod anti-affinity."
  type        = number
  default     = 2
}
