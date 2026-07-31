#!/usr/bin/env sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from .env.example."
fi

docker compose up --build --detach --wait
docker compose ps
printf '%s\n' \
  "LoadIx web:  http://localhost:5173" \
  "LoadIx API:  http://localhost:8080" \
  "OpenAPI:     http://localhost:8080/openapi/v1.json"
