#!/usr/bin/env bash
set -euo pipefail

source_url="${BIRDIEBUDDY_BACKUP_SOURCE_URL:-}"
target_url="${BIRDIEBUDDY_RESTORE_TARGET_URL:-}"

if [[ -z "$source_url" || -z "$target_url" ]]; then
  echo "Set BIRDIEBUDDY_BACKUP_SOURCE_URL and BIRDIEBUDDY_RESTORE_TARGET_URL." >&2
  exit 2
fi
for command_name in pg_dump pg_restore psql dotnet; do
  command -v "$command_name" >/dev/null 2>&1 || { echo "$command_name is required." >&2; exit 2; }
done

target_database="$(psql "$target_url" -Atqc 'select current_database()')"
target_address="$(psql "$target_url" -Atqc 'select inet_server_addr()::text')"
if [[ "$target_database" != birdiebuddy_restore_test_* ]]; then
  echo "Refusing restore: target database must start with birdiebuddy_restore_test_." >&2
  exit 2
fi
if [[ "$target_address" != "127.0.0.1" && "$target_address" != "::1" ]]; then
  echo "Refusing restore: target PostgreSQL must be local." >&2
  exit 2
fi

dump_file="$(mktemp -t birdiebuddy-backup.XXXXXX.dump)"
migration_file="$(mktemp -t birdiebuddy-migrations.XXXXXX.sql)"
trap 'rm -f "$dump_file" "$migration_file"' EXIT

pg_dump --format=custom --no-owner --no-acl --file="$dump_file" "$source_url"
pg_restore --clean --if-exists --no-owner --no-acl --dbname="$target_url" "$dump_file"
dotnet tool restore
dotnet ef migrations script --idempotent --configuration Release --output "$migration_file"
psql --set=ON_ERROR_STOP=1 --dbname="$target_url" --file="$migration_file" >/dev/null

psql "$target_url" -v ON_ERROR_STOP=1 -Atqc '
select case
  when to_regclass('"Users"') is null then 1 / 0
  when to_regclass('"__EFMigrationsHistory"') is null then 1 / 0
  else 1
end;
select count(*) from "Users";
select count(*) from "Rounds";
select count(*) from "CourseTees";'

echo "Backup restore and latest migration rehearsal passed in $target_database."
