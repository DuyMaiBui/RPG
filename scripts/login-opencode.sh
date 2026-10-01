#!/usr/bin/env bash
set -euo pipefail

# OpenCode Codex Plugin - API Key Login Script
# Usage: ./scripts/login-opencode.sh

CONFIG_DIR="${HOME}/.config/opencode"
CONFIG_FILE="${CONFIG_DIR}/opencode.json"
AUTH_FILE="${HOME}/.codex/auth.json"

echo "=== OpenCode Codex Plugin - Login ==="

# Prompt for API Key
read -rp "Enter your OpenCode API Key: " API_KEY

if [[ -z "${API_KEY}" ]]; then
  echo "Error: API Key cannot be empty"
  exit 1
fi

# Create config directory if it doesn't exist
mkdir -p "${CONFIG_DIR}"

# Write auth.json for Codex
mkdir -p "${HOME}/.codex"
cat > "${AUTH_FILE}" <<EOF
{
  "auth_mode": "apikey",
  "OPENAI_API_KEY": "${API_KEY}"
}
EOF

echo "✓ Auth config written to ${AUTH_FILE}"

# Test connection (optional)
echo ""
echo "Testing API connection..."
if curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${API_KEY}" \
  "https://api.openai.com/v1/models" | grep -q "200"; then
  echo "✓ Connection successful!"
else
  echo "⚠ Connection test failed. Check your API key."
fi

echo ""
echo "Done. You can now use: codex \"your prompt\""
