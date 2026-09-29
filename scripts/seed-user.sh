#!/usr/bin/env bash
# Seeds a single staff user into the docker-composed MariaDB.
#
#   scripts/seed-user.sh --email a@b.com --password 'Secret123!' [options]
#
# Options:
#   --first-name NAME     (default: Admin)
#   --last-name NAME      (default: User)
#   --phone 08031234567   digits only, max 11 — stored as +234 with the leading 0 dropped
#   --role SLUG           super_admin | controller | director | manager | marketer (default: super_admin)
#   --class CLASS         initiator | authorizer | reviewer (default: authorizer)
#   --office-id ID        optional offices.id
#   --must-change         force a password change on first login
#
# The role must already exist — the API's IdentityBootstrapSeeder creates the RBAC roles on
# startup, so run the api at least once first. The user is created Approved (self-approved).
# The password is BCrypt-hashed locally (htpasswd, else python3 bcrypt, else an httpd container).
set -euo pipefail

cd "$(dirname "$0")/.."

EMAIL="" PASSWORD="" FIRST_NAME="Admin" LAST_NAME="User" PHONE="" ROLE="super_admin"
USER_CLASS="authorizer" OFFICE_ID="" MUST_CHANGE=0
while [[ $# -gt 0 ]]; do
    case "$1" in
        --email) EMAIL="$2"; shift 2 ;;
        --password) PASSWORD="$2"; shift 2 ;;
        --first-name) FIRST_NAME="$2"; shift 2 ;;
        --last-name) LAST_NAME="$2"; shift 2 ;;
        --phone) PHONE="$2"; shift 2 ;;
        --role) ROLE="$2"; shift 2 ;;
        --class) USER_CLASS="$2"; shift 2 ;;
        --office-id) OFFICE_ID="$2"; shift 2 ;;
        --must-change) MUST_CHANGE=1; shift ;;
        -h|--help) sed -n '2,18p' "$0"; exit 0 ;;
        *) echo "Unknown argument: $1 (see --help)" >&2; exit 1 ;;
    esac
done

[[ -n "$EMAIL" && -n "$PASSWORD" ]] || { echo "--email and --password are required (see --help)" >&2; exit 1; }
[[ "$USER_CLASS" =~ ^(initiator|authorizer|reviewer)$ ]] || { echo "Invalid --class: $USER_CLASS" >&2; exit 1; }
[[ -z "$OFFICE_ID" || "$OFFICE_ID" =~ ^[0-9]+$ ]] || { echo "Invalid --office-id: $OFFICE_ID" >&2; exit 1; }

PHONE_SQL=NULL
if [[ -n "$PHONE" ]]; then
    [[ "$PHONE" =~ ^[0-9]{1,11}$ ]] || { echo "--phone must be digits only, max 11 (e.g. 08031234567)" >&2; exit 1; }
    PHONE_SQL="'+234${PHONE#0}'"
fi

[[ -f .env ]] || { echo "No .env found — copy .env.example to .env first." >&2; exit 1; }
env_value() { grep -E "^$1=" .env | tail -1 | cut -d= -f2- || true; }
DB_NAME=$(env_value DB_NAME)
[[ -n "$DB_NAME" ]] || { echo "DB_NAME is not set in .env" >&2; exit 1; }

sql() { docker compose exec -T db sh -c 'mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" "$@"' mariadb "$DB_NAME" "$@"; }
esc() { printf '%s' "$1" | sed "s/\\\\/\\\\\\\\/g; s/'/''/g"; }

bcrypt_hash() {
    if command -v htpasswd >/dev/null; then
        htpasswd -nbBC 11 "" "$1" | tr -d ':\n'
    elif python3 -c 'import bcrypt' 2>/dev/null; then
        python3 -c 'import bcrypt,sys; print(bcrypt.hashpw(sys.argv[1].encode(), bcrypt.gensalt(11)).decode(), end="")' "$1"
    else
        docker run --rm httpd:2.4-alpine htpasswd -nbBC 11 "" "$1" | tr -d ':\n'
    fi
}

docker compose up -d --wait db >/dev/null

ROLE_ID=$(sql -N -e "SELECT id FROM roles WHERE slug = '$(esc "$ROLE")' LIMIT 1")
[[ -n "$ROLE_ID" ]] || { echo "Role '$ROLE' not found — start the api once so it seeds the RBAC roles." >&2; exit 1; }

EXISTING=$(sql -N -e "SELECT COUNT(*) FROM users WHERE email = '$(esc "$EMAIL")'")
[[ "$EXISTING" -eq 0 ]] || { echo "A user with email $EMAIL already exists." >&2; exit 1; }

HASH=$(bcrypt_hash "$PASSWORD")

sql <<SQL
START TRANSACTION;
INSERT INTO users (email, password, first_name, last_name, phone, gender, office_id,
                   enable_google2fa, blocked, time_limit, user_class, onboarding_status,
                   onboarding_approved_date, must_change_password, created_at, updated_at)
VALUES ('$(esc "$EMAIL")', '$(esc "$HASH")', '$(esc "$FIRST_NAME")', '$(esc "$LAST_NAME")', $PHONE_SQL,
        'unspecified', ${OFFICE_ID:-NULL}, 0, 0, 0, '$USER_CLASS', 'approved',
        UTC_DATE(), $MUST_CHANGE, UTC_TIMESTAMP(), UTC_TIMESTAMP());
SET @uid = LAST_INSERT_ID();
UPDATE users SET onboarding_approved_by_id = @uid WHERE id = @uid;
INSERT INTO role_users (user_id, role_id, created_at, updated_at)
VALUES (@uid, $ROLE_ID, UTC_TIMESTAMP(), UTC_TIMESTAMP());
COMMIT;
SQL

# The API caches every EF query in Redis for 5 minutes, and this raw insert bypasses EF's cache
# invalidation — without a flush, a login tried before seeding keeps "not finding" the user.
REDIS_PASSWORD=$(env_value REDIS_PASSWORD)
docker compose exec -T redis redis-cli -a "$REDIS_PASSWORD" --no-auth-warning FLUSHALL >/dev/null \
    || echo "Warning: couldn't flush the Redis query cache — restart the api if login can't find the user." >&2

echo "Seeded $EMAIL (role: $ROLE, class: $USER_CLASS) into $DB_NAME"
