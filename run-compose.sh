#!/usr/bin/env bash
set -euo pipefail

stack="${1:-}"
action="${2:-up}"
shift $(( $# > 0 ? 1 : 0 ))
shift $(( $# > 0 ? 1 : 0 ))

case "$stack" in
  full) compose_file="docker-compose.full.yml" ;;
  backend) compose_file="docker-compose.backend.yml" ;;
  frontend) compose_file="docker-compose.frontend.yml" ;;
  *)
    echo "Usage: ./run-compose.sh <full|backend|frontend> [action] [extra args...]"
    echo "Example: ./run-compose.sh full up --build"
    exit 1
    ;;
esac

if [[ "$action" == "up" ]]; then
  docker compose -f "$compose_file" up -d "$@"
else
  docker compose -f "$compose_file" "$action" "$@"
fi
