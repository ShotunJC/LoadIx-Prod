#!/usr/bin/env sh
set -eu

command="--seed-demo"
if [ "${1:-}" = "--reset" ]; then
  command="--reset-demo"
fi

ASPNETCORE_ENVIRONMENT=Development \
LOADIX_DEMO_SEED_ENABLED=true \
dotnet run --project src/LoadIx.Api --no-launch-profile -- "$command"
