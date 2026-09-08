#!/usr/bin/env bash
set -euo pipefail

base_url="${1:-${BIRDIEBUDDY_PUBLIC_BASE_URL:-}}"
if [[ -z "$base_url" ]]; then
  echo "Usage: $0 https://your-service.onrender.com" >&2
  exit 2
fi

if [[ "$base_url" != https://* ]]; then
  echo "Deployment URL must use HTTPS." >&2
  exit 2
fi

base_url="${base_url%/}"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

check_status() {
  local path="$1"
  local expected="${2:-200}"
  local status
  status="$(curl --fail-with-body --silent --show-error --location --connect-timeout 10 --max-time 60 \
    --output "$tmp_dir/body" --write-out '%{http_code}' "$base_url$path")"
  if [[ "$status" != "$expected" ]]; then
    echo "$path returned HTTP $status (expected $expected)." >&2
    cat "$tmp_dir/body" >&2 || true
    exit 1
  fi
  echo "PASS $path ($status)"
}

check_status "/health/live"
check_status "/health/ready"

curl --fail-with-body --silent --show-error --location --connect-timeout 10 --max-time 60 \
  --dump-header "$tmp_dir/headers" --output "$tmp_dir/login.html" "$base_url/login.html"

grep -Fiq "Content-Security-Policy:" "$tmp_dir/headers" || {
  echo "login.html is missing Content-Security-Policy." >&2
  exit 1
}
grep -Fiq "script-src 'self'" "$tmp_dir/headers" || {
  echo "CSP does not restrict scripts to the application origin." >&2
  exit 1
}
if grep -Eq "cdn\.jsdelivr\.net|fonts\.googleapis\.com|fonts\.gstatic\.com" "$tmp_dir/headers"; then
  echo "CSP still allows a removed third-party asset origin." >&2
  exit 1
fi
echo "PASS login.html security headers"

check_status "/vendor/chart.js/chart.umd.min.js?v=4.4.4"
echo "Deployment smoke checks passed for $base_url"
