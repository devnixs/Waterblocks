#!/bin/sh
set -eu

API_BASE_URL="${API_BASE_URL:-}"
if [ -z "$API_BASE_URL" ]; then
  API_BASE_URL="http://localhost:5671"
fi

if [ -f /usr/share/nginx/html/config.js.template ]; then
  envsubst '${API_BASE_URL}' < /usr/share/nginx/html/config.js.template > /usr/share/nginx/html/config.js
fi
