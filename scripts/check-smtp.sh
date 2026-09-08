#!/usr/bin/env bash
set -euo pipefail

smtp_host="${1:-${BIRDIEBUDDY_SMTP_HOST:-}}"
smtp_port="${2:-${BIRDIEBUDDY_SMTP_PORT:-587}}"

if [[ -z "$smtp_host" ]]; then
  echo "Usage: $0 smtp.example.com [port]" >&2
  exit 2
fi
if [[ ! "$smtp_port" =~ ^[0-9]+$ ]] || (( smtp_port < 1 || smtp_port > 65535 )); then
  echo "SMTP port must be between 1 and 65535." >&2
  exit 2
fi
command -v openssl >/dev/null 2>&1 || { echo "openssl is required." >&2; exit 2; }

transcript="$(mktemp)"
trap 'rm -f "$transcript"' EXIT

printf 'EHLO birdiebuddy-health-check\r\nQUIT\r\n' | \
  openssl s_client -quiet -starttls smtp -connect "$smtp_host:$smtp_port" \
    -servername "$smtp_host" -verify_return_error >"$transcript" 2>&1

grep -Eq '^250[ -]' "$transcript" || {
  echo "SMTP TLS connected but the server did not accept EHLO." >&2
  exit 1
}

echo "SMTP TLS and EHLO check passed for $smtp_host:$smtp_port"
echo "No credentials were submitted and no email was sent."
