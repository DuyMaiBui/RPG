#!/usr/bin/env bash
set -euo pipefail

# Configure OpenCodeX with OpenCode API Key as a provider for Codex CLI
# Usage: ./scripts/login-opencode-codex.sh

OPENCODEX_HOME="${OPENCODEX_HOME:-$HOME/.opencodex}"
CONFIG_FILE="${OPENCODEX_HOME}/config.json"
AUTH_FILE="${OPENCODEX_HOME}/auth.json"
CODEX_AUTH_FILE="${HOME}/.codex/auth.json"

OPENCODE_BASE_URL="https://api.opencode.ai/v1"
PROXY_PORT="${PROXY_PORT:-10100}"

echo "=== OpenCodeX - Configure OpenCode as Provider ==="
echo ""

# Check if opencodex is installed
if ! command -v ocx &>/dev/null; then
  echo "Error: opencodex (ocx) is not installed."
  echo "Install: npm install -g @ingwannu/opencodex"
  exit 1
fi

# Prompt for API Key
read -rp "Enter your OpenCode API Key: " OPENCODE_API_KEY

if [[ -z "${OPENCODE_API_KEY}" ]]; then
  echo "Error: API Key cannot be empty"
  exit 1
fi

# Prompt for model (optional, with default)
read -rp "Default model [deepseek/deepseek-chat]: " MODEL
MODEL="${MODEL:-deepseek/deepseek-chat}"

# Create config directory
mkdir -p "${OPENCODEX_HOME}"

# Configure OpenCodeX config.json
cat > "${CONFIG_FILE}" <<EOF
{
  "proxy": {
    "port": ${PROXY_PORT},
    "host": "127.0.0.1"
  },
  "providers": {
    "opencode": {
      "name": "OpenCode",
      "type": "openai-chat",
      "baseURL": "${OPENCODE_BASE_URL}",
      "apiKey": "${OPENCODE_API_KEY}",
      "adapter": "openai-chat",
      "models": ["${MODEL}"]
    }
  },
  "defaultProvider": "opencode"
}
EOF

echo "✓ OpenCodeX config written to ${CONFIG_FILE}"

# Configure Codex CLI auth.json
mkdir -p "${HOME}/.codex"
cat > "${CODEX_AUTH_FILE}" <<EOF
{
  "auth_mode": "apikey",
  "OPENAI_API_KEY": "${OPENCODE_API_KEY}"
}
EOF

echo "✓ Codex auth written to ${CODEX_AUTH_FILE}"

# Test connection
echo ""
echo "Testing OpenCode API connection..."
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer ${OPENCODE_API_KEY}" \
  -H "Content-Type: application/json" \
  "${OPENCODE_BASE_URL}/models" 2>/dev/null || echo "000")

if [[ "${HTTP_CODE}" == "200" ]]; then
  echo "✓ Connection successful!"
elif [[ "${HTTP_CODE}" == "401" ]]; then
  echo "⚠ Invalid API Key (401). Please check your key."
elif [[ "${HTTP_CODE}" == "000" ]]; then
  echo "⚠ Could not connect to ${OPENCODE_BASE_URL}"
else
  echo "⚠ Unexpected response: HTTP ${HTTP_CODE}"
fi

echo ""
echo "=== Setup Complete ==="
echo ""
echo "Next steps:"
echo "  1. Start OpenCodeX proxy:  ocx start"
echo "  2. Use Codex CLI:          codex \"your prompt\""
echo ""
echo "Or test directly:"
echo "  curl -H 'Authorization: Bearer ${OPENCODE_API_KEY}' \\"
echo "    ${OPENCODE_BASE_URL}/models"
