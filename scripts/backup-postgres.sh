#!/usr/bin/env bash
set -euo pipefail

# Backup ShaunERP PostgreSQL (Docker or host).
# Preferred production path uses docker exec against shaunerp-db.
#
# Usage:
#   ./scripts/backup-postgres.sh
# Env:
#   USE_DOCKER=1 (default when shaunerp-db is running)
#   POSTGRES_DB / POSTGRES_USER / POSTGRES_PASSWORD
#   BACKUP_ROOT (default /backups/shaunerp or ./backups/shaunerp)
#   COMPOSE_FILE / COMPOSE_DIR for loading .env

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
COMPOSE_DIR="${COMPOSE_DIR:-/opt/shaunerp}"
if [[ -f "${COMPOSE_DIR}/.env" ]]; then
  # shellcheck disable=SC1091
  set -a
  # Load only expected keys
  while IFS= read -r line; do
    case "$line" in
      POSTGRES_*=*|PGPASSWORD=*) export "$line" ;;
    esac
  done < <(grep -E '^(POSTGRES_|PGPASSWORD=)' "${COMPOSE_DIR}/.env" || true)
  set +a
fi

DB="${POSTGRES_DB:-shaunerp}"
USER="${POSTGRES_USER:-shaunerp}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"
BACKUP_ROOT="${BACKUP_ROOT:-/backups/shaunerp}"
if [[ ! -d "$(dirname "${BACKUP_ROOT}")" ]] && [[ "$(id -u)" -ne 0 ]]; then
  BACKUP_ROOT="${ROOT}/backups/shaunerp"
fi
DB_DIR="${BACKUP_ROOT}/database"
CFG_DIR="${BACKUP_ROOT}/configs"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
OUT="${DB_DIR}/${DB}_${STAMP}.sql.gz"

mkdir -p "${DB_DIR}" "${CFG_DIR}"

USE_DOCKER="${USE_DOCKER:-}"
if [[ -z "${USE_DOCKER}" ]]; then
  if docker ps --format '{{.Names}}' 2>/dev/null | grep -qx 'shaunerp-db'; then
    USE_DOCKER=1
  else
    USE_DOCKER=0
  fi
fi

echo "Backing up ${DB} -> ${OUT} (docker=${USE_DOCKER})"
if [[ "${USE_DOCKER}" == "1" ]]; then
  docker exec -e PGPASSWORD="${PGPASSWORD}" shaunerp-db \
    pg_dump -U "${USER}" -d "${DB}" --no-owner --format=plain \
    | gzip > "${OUT}"
else
  HOST="${POSTGRES_HOST:-localhost}"
  PORT="${POSTGRES_PORT:-5432}"
  pg_dump -h "${HOST}" -p "${PORT}" -U "${USER}" -d "${DB}" --no-owner --format=plain | gzip > "${OUT}"
fi

# Config snapshot (no secrets if .env is mode 600 — still store privately)
if [[ -f "${COMPOSE_DIR}/docker-compose.prod.yml" ]]; then
  cp "${COMPOSE_DIR}/docker-compose.prod.yml" "${CFG_DIR}/docker-compose.prod.yml.${STAMP}"
fi
if [[ -f "${COMPOSE_DIR}/.env" ]]; then
  cp "${COMPOSE_DIR}/.env" "${CFG_DIR}/env.${STAMP}.restricted"
  chmod 600 "${CFG_DIR}/env.${STAMP}.restricted" || true
fi

# Verify gzip integrity
gzip -t "${OUT}"
BYTES="$(wc -c < "${OUT}" | tr -d ' ')"
echo "Verified backup: ${OUT} (${BYTES} bytes)"
echo "Retention tip: keep last 14 daily + monthly; never store under public web roots."
