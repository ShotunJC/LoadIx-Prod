#!/usr/bin/env sh
set -eu

# Run on the dedicated host from the checked-out release. The application
# images are immutable commit-SHA tags; the protected env file contains only
# host-managed configuration and secrets.

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

set_image_tag() {
    temporary_file=$(mktemp "${environment_file}.tmp.XXXXXX")
    if [ -f "$environment_file" ]; then
        grep -v '^LOADIX_IMAGE_TAG=' "$environment_file" > "$temporary_file" || true
    fi
    printf 'LOADIX_IMAGE_TAG=%s\n' "$image_tag" >> "$temporary_file"
    chmod 600 "$temporary_file"
    mv "$temporary_file" "$environment_file"
}

backup_file=$(mktemp "${environment_file}.backup.XXXXXX")
cp --preserve=mode,ownership "$environment_file" "$backup_file"
restore_previous_release() {
    cp --preserve=mode,ownership "$backup_file" "$environment_file"
    compose up -d api web worker caddy >/dev/null 2>&1 || true
    rm -f "$backup_file"
}
trap restore_previous_release INT TERM HUP EXIT

set_image_tag
compose config --quiet
compose --profile tools pull api web worker migrate

# Migrations run before application containers change. The migration image is
# supplied the same immutable tag as the application images.
compose --profile tools run --rm migrate
compose up -d --remove-orphans api web worker caddy
compose ps

rm -f "$backup_file"
trap - INT TERM HUP EXIT
trap - EXIT
echo "LoadIx pilot release $image_tag is running."
