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

variable "image_tag" {
  description = "Container image tag to deploy."
  type        = string
  default     = "latest"
}

variable "migration_revision" {
  description = <<-EOT
    Opaque value that forces the migration Job to be recreated. Kubernetes Jobs are
    immutable, so without this a deploy that reuses the same image tag (the default
    is 'latest') would silently keep the old, already-completed Job and skip the
    migration. infra-ci.yml passes the commit SHA; set it by hand for a local apply.
  EOT
  type        = string
  default     = "manual"
}

variable "api_replicas" {
  description = "Number of web-api replicas. Set to 2 to spread across both Pi nodes via pod anti-affinity."
  type        = number
  default     = 2
}
