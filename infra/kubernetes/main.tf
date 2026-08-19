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

resource "kubernetes_deployment_v1" "web_api" {
  metadata {
    name      = "web-api"
    namespace = kubernetes_namespace.app.metadata[0].name
  }

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
          image = "${var.image_repository}:${var.image_tag}"

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
