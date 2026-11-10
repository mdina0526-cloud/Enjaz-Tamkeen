#!/usr/bin/env bash
set -euo pipefail
umask 077

project_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_dir"

if [[ ! -f .env.production ]]; then
  echo 'Missing .env.production' >&2
  exit 1
fi

backup_root="${BACKUP_ROOT:-$project_dir/backups}"
backup_dir="$backup_root/$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$backup_dir"

docker compose --env-file .env.production -f compose.production.yml exec -T db \
  sh -c 'exec pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc' > "$backup_dir/database.dump"
docker compose --env-file .env.production -f compose.production.yml \
  cp backend:/app/App_Data/uploads "$backup_dir/"
docker compose --env-file .env.production -f compose.production.yml \
  cp backend:/app/App_Data/keys "$backup_dir/"

echo "Backup saved to $backup_dir"
