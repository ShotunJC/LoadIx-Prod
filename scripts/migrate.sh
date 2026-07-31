#!/usr/bin/env sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$repository_root"
dotnet tool restore
dotnet ef database update \
  --project src/LoadIx.Infrastructure \
  --startup-project src/LoadIx.Api
dotnet run \
  --project src/LoadIx.Api \
  --no-launch-profile \
  -- --environment Development --seed-development
