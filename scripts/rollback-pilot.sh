#!/usr/bin/env sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
environment_file=${LOADIX_ENV_FILE:-/etc/loadix/pilot.env}
image_tag=${1:-}

if [ -z "$image_tag" ] || ! printf '%s' "$image_tag" | grep -Eq '^[0-9a-f]{40}$'; then
    echo "Usage: $0 <40-character-lowercase-commit-sha>" >&2
    exit 2
fi

if [ ! -f "$environment_file" ]; then
    echo "Protected environment file not found: $environment_file" >&2
    exit 1
fi

compose() {
    docker compose --env-file "$environment_file" \
        -f "$repository_root/compose.pilot.yaml" "$@"
}

temporary_file=$(mktemp "${environment_file}.backup.XXXXXX")
cp --preserve=mode,ownership "$environment_file" "$temporary_file"
restore_previous_release() {
    cp --preserve=mode,ownership "$temporary_file" "$environment_file"
    compose up -d api web worker caddy >/dev/null 2>&1 || true
    rm -f "$temporary_file"
}
trap restore_previous_release INT TERM HUP EXIT

next_environment=$(mktemp "${environment_file}.tmp.XXXXXX")
grep -v '^LOADIX_IMAGE_TAG=' "$environment_file" > "$next_environment" || true
printf 'LOADIX_IMAGE_TAG=%s\n' "$image_tag" >> "$next_environment"
chmod 600 "$next_environment"
mv "$next_environment" "$environment_file"

compose config --quiet
compose pull api web worker
compose up -d api web worker caddy
compose ps

rm -f "$temporary_file"
trap - INT TERM HUP EXIT
trap - EXIT
echo "LoadIx pilot application release $image_tag is restored."
