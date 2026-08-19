terraform {
  required_version = ">= 1.8.0"

  required_providers {
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.33"
    }
  }

  # State lives in Cloudflare R2 (S3-compatible), not in this cluster — so a
  # dead/rebuilt Pi doesn't take the record of what was provisioned down with
  # it. `bucket` and `endpoints` are deliberately left out here (they'd need
  # the Cloudflare account ID, which we don't hardcode into the repo) and are
  # supplied at `tofu init -backend-config=backend.hcl` time instead — see
  # backend.hcl.example and README.md.
  backend "s3" {
    key                         = "kubernetes.tfstate"
    region                      = "auto"
    skip_credentials_validation = true
    skip_region_validation      = true
    skip_requesting_account_id  = true
    skip_s3_checksum            = true
    use_path_style              = true
  }
}

provider "kubernetes" {
  config_path = var.kubeconfig_path
}
