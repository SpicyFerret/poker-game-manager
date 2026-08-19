locals {
  # Pinned by digest, never by tag. Republishing a tag leaves this string
  # unchanged, so Tofu would see no change and the pods would keep running the
  # old image — see variable "image_digest".
  image = "${var.image_repository}@${var.image_digest}"
}

resource "kubernetes_namespace" "app" {
  metadata {
    name = var.namespace
  }
}

# --- Postgres -----------------------------------------------------------

resource "kubernetes_secret" "postgres" {
  metadata {
    name      = "postgres-credentials"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  data = {
    POSTGRES_DB       = var.postgres_db
    POSTGRES_USER     = var.postgres_user
    POSTGRES_PASSWORD = var.postgres_password
  }
}

resource "kubernetes_service" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  spec {
    cluster_ip = "None" # headless, so the StatefulSet pod gets a stable DNS name
    selector = {
      app = "postgres"
    }

    port {
      port        = 5432
      target_port = 5432
    }
  }
}

resource "kubernetes_stateful_set_v1" "postgres" {
  metadata {
    name      = "postgres"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  spec {
    service_name = kubernetes_service.postgres.metadata[0].name
    replicas     = 1

    selector {
      match_labels = {
        app = "postgres"
      }
    }

    template {
      metadata {
        labels = {
          app = "postgres"
        }
      }

      spec {
        # Pins Postgres to the node with real disk. The 'local-path' provisioner
        # stores the volume on the node's own root filesystem, so the database
        # competes with container images for the same space — and rb02 is a
        # ~7GB SD card that has already evicted a pod for running out. rb01 has
        # ~29GB. A required (not preferred) rule: scheduling the database
        # anywhere else silently puts the data back on the small disk.
        affinity {
          node_affinity {
            required_during_scheduling_ignored_during_execution {
              node_selector_term {
                match_expressions {
                  key      = "kubernetes.io/hostname"
                  operator = "In"
                  values   = [var.postgres_node]
                }
              }
            }
          }
        }

        container {
          name  = "postgres"
          image = "postgres:17"

          env_from {
            secret_ref {
              name = kubernetes_secret.postgres.metadata[0].name
            }
          }

          port {
            container_port = 5432
          }

          volume_mount {
            name       = "data"
            mount_path = "/var/lib/postgresql/data"
            sub_path   = "postgres" # avoids postgres writing into the lost+found dir some CSI drivers create at the volume root
          }

          readiness_probe {
            exec {
              command = ["pg_isready", "-U", var.postgres_user]
            }
            initial_delay_seconds = 5
            period_seconds        = 10
          }
        }
      }
    }

    volume_claim_template {
      metadata {
        name = "data"
      }

      spec {
        access_modes       = ["ReadWriteOnce"]
        storage_class_name = var.storage_class_name

        resources {
          requests = {
            storage = var.postgres_storage_size
          }
        }
      }
    }
  }
}

# --- API ------------------------------------------------------------------

resource "kubernetes_secret" "web_api" {
  metadata {
    name      = "web-api-secrets"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  data = {
    "ConnectionStrings__Database" = "Host=${kubernetes_service.postgres.metadata[0].name};Port=5432;Database=${var.postgres_db};Username=${var.postgres_user};Password=${var.postgres_password}"
    "Jwt__Secret"                 = var.jwt_secret
  }
}

# Applies EF Core migrations before any API replica serves traffic. The app image
# is reused with --migrate-only, which runs the migrations and exits.
#
# A Job rather than migrating on API startup: a broken or slow migration fails
# the apply loudly here, instead of showing up as replicas that never turn ready
# while the old ones keep serving against a schema that no longer matches.
#
# Kubernetes Jobs are immutable, so the name carries part of the image digest: a
# new image produces a new Job rather than a conflict with the old one, and the
# same image reuses it. Re-running is harmless either way — EF skips migrations
# already in the history table.
resource "kubernetes_job_v1" "migrate" {
  metadata {
    name      = "web-api-migrate-${substr(replace(var.image_digest, "sha256:", ""), 0, 10)}"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  spec {
    backoff_limit = 2

    # Keeps a finished Job around long enough to read its logs, then reaps it so
    # they don't pile up one per deployed image.
    # 24h rather than an hour: within a normal day of iterating, re-applying the
    # same image finds the Job still there and plans clean. Once it is reaped,
    # the next apply recreates and re-runs it, which costs seconds and changes
    # nothing.
    ttl_seconds_after_finished = 86400

    template {
      metadata {
        labels = {
          app = "web-api-migrate"
        }
      }

      spec {
        restart_policy = "Never"

        container {
          name  = "migrate"
          image = local.image
          args  = ["--migrate-only"]

          env {
            name  = "ASPNETCORE_ENVIRONMENT"
            value = "Production"
          }

          env_from {
            secret_ref {
              name = kubernetes_secret.web_api.metadata[0].name
            }
          }
        }
      }
    }
  }

  wait_for_completion = true

  timeouts {
    create = "10m"
  }

  depends_on = [kubernetes_stateful_set_v1.postgres]
}

resource "kubernetes_deployment_v1" "web_api" {
  metadata {
    name      = "web-api"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  # No replica starts against a schema the migration Job has not finished applying.
  depends_on = [kubernetes_job_v1.migrate]

  spec {
    replicas = var.api_replicas

    selector {
      match_labels = {
        app = "web-api"
      }
    }

    template {
      metadata {
        labels = {
          app = "web-api"
        }
      }

      spec {
        # Spreads the replicas across the two Pi nodes instead of stacking them on one.
        affinity {
          pod_anti_affinity {
            preferred_during_scheduling_ignored_during_execution {
              weight = 100

              pod_affinity_term {
                topology_key = "kubernetes.io/hostname"

                label_selector {
                  match_labels = {
                    app = "web-api"
                  }
                }
              }
            }
          }
        }

        container {
          name  = "web-api"
          image = local.image

          env {
            name  = "ASPNETCORE_ENVIRONMENT"
            value = "Production"
          }

          env_from {
            secret_ref {
              name = kubernetes_secret.web_api.metadata[0].name
            }
          }

          port {
            container_port = 8080
          }

          readiness_probe {
            http_get {
              path = "/health"
              port = 8080
            }
            initial_delay_seconds = 10
            period_seconds        = 10
          }

          liveness_probe {
            http_get {
              path = "/health"
              port = 8080
            }
            initial_delay_seconds = 20
            period_seconds        = 20
          }
        }
      }
    }
  }
}

resource "kubernetes_service" "web_api" {
  metadata {
    name      = "web-api"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

  spec {
    type = "ClusterIP"
    selector = {
      app = "web-api"
    }

    port {
      port        = 80
      target_port = 8080
    }
  }
}
