#!/usr/bin/env bash
# Seeds the docker-composed MariaDB with the legacy BCKash phpMyAdmin dump.
#
#   scripts/seed-legacy-dump.sh [--force] [path/to/dump.sql]
#
# Dump path: argument, else LEGACY_DUMP_PATH from .env, else ./dbdump/bckbbjxq_real.sql
# (falling back to ../dbdump/). Same layout locally and on the server (repo at ~/bckashproject):
#
#   bckashproject/       <- repo root
#     dbdump/            <- the .sql dump (gitignored + dockerignored)
#       bckbbjxq_real.sql
#
# The dump can't be loaded straight into DB_NAME: EF migrations own that schema (and the dump
# has no DROP TABLEs). So this:
#   1. starts db/redis/api so the API applies its EF migrations to DB_NAME
#   2. stops the api, imports the dump into a throwaway staging database
#   3. copies every table into DB_NAME (see scripts/sql/legacy-copy-generator.sql)
#   4. verifies row counts table-by-table, drops the staging database
#   5. flushes the Redis query cache and starts the api again (its seeders then add the new
#      RBAC roles/permissions alongside the legacy ones)
#
# Refuses to run if DB_NAME already has users, unless --force (which wipes and re-seeds).
set -euo pipefail

cd "$(dirname "$0")/.."

FORCE=0
DUMP_ARG=""
for arg in "$@"; do
    case "$arg" in
        --force) FORCE=1 ;;
        -h|--help) sed -n '2,24p' "$0"; exit 0 ;;
        *) DUMP_ARG="$arg" ;;
    esac
done

[[ -f .env ]] || { echo "No .env found — copy .env.example to .env first." >&2; exit 1; }
env_value() { grep -E "^$1=" .env | tail -1 | cut -d= -f2- || true; }

DB_NAME=$(env_value DB_NAME)
REDIS_PASSWORD=$(env_value REDIS_PASSWORD)
API_PORT=$(env_value API_PORT); API_PORT=${API_PORT:-8080}
DUMP=${DUMP_ARG:-$(env_value LEGACY_DUMP_PATH)}
if [[ -z "$DUMP" ]]; then
    DUMP=./dbdump/bckbbjxq_real.sql
    [[ -f "$DUMP" ]] || DUMP=../dbdump/bckbbjxq_real.sql
fi
STAGING_DB=legacy_import

[[ -n "$DB_NAME" ]] || { echo "DB_NAME is not set in .env" >&2; exit 1; }
[[ -f "$DUMP" ]] || { echo "Dump not found: $DUMP" >&2; exit 1; }

sql() { docker compose exec -T db sh -c 'mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" "$@"' mariadb "$@"; }
step() { printf '\n==> %s\n' "$*"; }

step "Starting db, redis and api (api applies EF migrations to $DB_NAME)"
docker compose up -d --wait db redis
docker compose up -d api
for _ in $(seq 1 90); do
    curl -s -o /dev/null "http://localhost:$API_PORT/" && break
    sleep 2
done
curl -s -o /dev/null "http://localhost:$API_PORT/" || {
    echo "API didn't come up on port $API_PORT — check: docker compose logs api" >&2; exit 1; }

existing_users=$(sql -N -e "SELECT COUNT(*) FROM \`$DB_NAME\`.users")
if [[ "$existing_users" -gt 0 && "$FORCE" -ne 1 ]]; then
    echo "$DB_NAME already has $existing_users users — looks seeded. Re-run with --force to wipe and re-seed." >&2
    exit 1
fi

step "Stopping api during the import"
docker compose stop api

step "Importing $DUMP into staging database $STAGING_DB (a 3GB dump takes several minutes)"
sql -e "DROP DATABASE IF EXISTS \`$STAGING_DB\`; CREATE DATABASE \`$STAGING_DB\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
if command -v pv >/dev/null; then reader=(pv "$DUMP"); else reader=(cat "$DUMP"); fi
"${reader[@]}" | sql --max_allowed_packet=1G \
    --init-command="SET SESSION foreign_key_checks=0; SET SESSION unique_checks=0;" "$STAGING_DB"

step "Copying $STAGING_DB into $DB_NAME"
copy_sql=$({ echo "SET @src='$STAGING_DB'; SET @dst='$DB_NAME';"; cat scripts/sql/legacy-copy-generator.sql; } | sql -N -r)
echo "$copy_sql" | sql -N --show-warnings

step "Verifying row counts"
count_sql=$(sql -N -r -e "
    SELECT CONCAT('SELECT ''', table_name, ''', (SELECT COUNT(*) FROM \`$STAGING_DB\`.\`', table_name,
                  '\`), (SELECT COUNT(*) FROM \`$DB_NAME\`.\`', table_name, '\`);')
    FROM information_schema.tables
    WHERE table_schema = '$STAGING_DB'
      AND table_name IN (SELECT table_name FROM information_schema.tables WHERE table_schema = '$DB_NAME');")
counts=$(echo "$count_sql" | sql -N)
mismatches=$(echo "$counts" | awk '$2 != $3')
if [[ -n "$mismatches" ]]; then
    echo "Row count mismatch (table / staging / $DB_NAME) — staging database $STAGING_DB left in place for inspection:" >&2
    echo "$mismatches" >&2
    exit 1
fi
echo "$counts" | awk '{ rows += $3 } END { printf "%d tables, %d rows copied — all counts match\n", NR, rows }'

step "Dropping staging database, flushing Redis cache, starting api"
sql -e "DROP DATABASE \`$STAGING_DB\`;"
docker compose exec -T redis redis-cli -a "$REDIS_PASSWORD" --no-auth-warning FLUSHALL >/dev/null
docker compose up -d api

step "Done — $DB_NAME is seeded from $DUMP"
