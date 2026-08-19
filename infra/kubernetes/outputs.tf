output "deployed_image" {
  description = <<-EOT
    The exact image the API is running, repository plus digest. Answers "what is
    actually deployed right now" from state, which a mutable tag cannot.
  EOT
  value       = local.image
}

output "namespace" {
  description = "Namespace the app was deployed into."
  value       = kubernetes_namespace.app.metadata[0].name
}

output "web_api_internal_url" {
  description = <<-EOT
    Cluster-internal address of the web-api Service. This is what the
    already-running Cloudflare Tunnel (on the Pi nodes, managed outside this
    module) should route its public hostname to — that public hostname is
    then set as API_ORIGIN in frontend/wrangler.jsonc.
  EOT
  value       = "http://${kubernetes_service.web_api.metadata[0].name}.${kubernetes_namespace.app.metadata[0].name}.svc.cluster.local"
}
