#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-${SMOKE_BASE_URL:-}}"
if [ -z "$base_url" ]; then
  echo "Usage: scripts/smoke-test.sh https://pilot.example.com" >&2
  exit 2
fi

base_url="${base_url%/}"
case "$base_url" in
  https://*) ;;
  *)
    echo "Smoke tests require an HTTPS base URL." >&2
    exit 2
    ;;
esac
temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

request_status() {
  curl --silent --show-error --output /dev/null --write-out '%{http_code}' "$1"
}

echo "Checking web shell and security headers..."
curl --fail --silent --show-error --dump-header "$temporary_directory/web.headers" \
  --output /dev/null "$base_url/"
grep -Fqi 'x-content-type-options: nosniff' "$temporary_directory/web.headers"
grep -Fqi 'strict-transport-security:' "$temporary_directory/web.headers"

echo "Checking API liveness..."
curl --fail --silent --show-error "$base_url/health/live" >/dev/null

echo "Checking API readiness..."
curl --fail --silent --show-error "$base_url/health/ready" >/dev/null

echo "Checking public system metadata..."
system_info="$(curl --fail --silent --show-error "$base_url/api/v1/system/info")"
printf '%s' "$system_info" | grep -Fq '"serviceName"'
printf '%s' "$system_info" | grep -Fq '"apiVersion":"v1"'

echo "Checking protected route authentication..."
status="$(request_status "$base_url/api/v1/edge-devices/?page=1&pageSize=1")"
if [ "$status" != "401" ]; then
  echo "Expected 401 from an unauthenticated protected route, received $status." >&2
  exit 1
fi

echo "LoadIx smoke tests passed for $base_url"
