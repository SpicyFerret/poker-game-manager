#!/usr/bin/env bash
#
# Verifies that the Cloudflare Tunnel still routes the API's public hostname to
# the in-cluster web-api Service.
#
# READ ONLY, BY DESIGN. This script only ever issues a GET.
#
# The reason is not caution for its own sake: the Cloudflare provider has no
# additive resource for a tunnel's public hostnames. Its only ingress resource,
# cloudflare_zero_trust_tunnel_cloudflared_config, owns the *entire* rule list,
# so managing our one route from here would make this repo the owner of every
# other route on the tunnel too — portainer, grafana, prometheus, k8s and the
# personal-website API, none of which belong to this project. Writing is
# therefore off the table, and what remains worth automating is noticing when
# the route is gone.
#
# Required environment:
#   CLOUDFLARE_API_TOKEN   needs "Account > Cloudflare Tunnel > Read"
#   CLOUDFLARE_ACCOUNT_ID
#   CLOUDFLARE_TUNNEL_ID
#   API_TUNNEL_HOSTNAME    e.g. poker-api-internal.example.com
#   EXPECTED_SERVICE       e.g. http://web-api.poker-game-manager.svc.cluster.local:80

set -euo pipefail

for var in CLOUDFLARE_API_TOKEN CLOUDFLARE_ACCOUNT_ID CLOUDFLARE_TUNNEL_ID API_TUNNEL_HOSTNAME EXPECTED_SERVICE; do
  if [ -z "${!var:-}" ]; then
    echo "::error::$var is not set"
    exit 1
  fi
done

api="https://api.cloudflare.com/client/v4/accounts/${CLOUDFLARE_ACCOUNT_ID}/cfd_tunnel/${CLOUDFLARE_TUNNEL_ID}/configurations"

response="$(curl -sS -X GET "$api" -H "Authorization: Bearer ${CLOUDFLARE_API_TOKEN}")"

if [ "$(jq -r '.success' <<<"$response")" != "true" ]; then
  echo "::error::Cloudflare API call failed."
  # Error bodies carry no secrets — only codes and messages — and without them
  # a 403 here is indistinguishable from a missing route.
  jq -r '.errors // empty' <<<"$response"
  echo "A 403 here usually means the API token lacks 'Account > Cloudflare Tunnel > Read'."
  exit 1
fi

ingress="$(jq -c '.result.config.ingress // []' <<<"$response")"
total="$(jq 'length' <<<"$ingress")"

echo "Tunnel has $total ingress rules (including the catch-all)."

actual="$(jq -r --arg host "$API_TUNNEL_HOSTNAME" \
  'map(select(.hostname == $host)) | first | .service // empty' <<<"$ingress")"

if [ -z "$actual" ]; then
  echo "::error::No tunnel route found for '$API_TUNNEL_HOSTNAME'."
  echo "The public site will answer 530 / NXDOMAIN until it exists."
  echo "Add it under Zero Trust > Networks > Tunnels > (tunnel) > published application routes,"
  echo "pointing at '$EXPECTED_SERVICE' over HTTP (not HTTPS — the API listens in plain text in-cluster)."
  exit 1
fi

if [ "$actual" != "$EXPECTED_SERVICE" ]; then
  echo "::error::Tunnel route for '$API_TUNNEL_HOSTNAME' points at the wrong service."
  echo "  expected: $EXPECTED_SERVICE"
  echo "  actual:   $actual"
  exit 1
fi

echo "OK: '$API_TUNNEL_HOSTNAME' -> '$actual'"
