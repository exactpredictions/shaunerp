#!/usr/bin/env bash
set -euo pipefail

# Restore ShaunERP PostgreSQL database from a gzipped SQL dump.
# Usage:
#   ./scripts/restore-postgres.sh backups/shaunerp_YYYYMMDDTHHMMSSZ.sql.gz
# WARNING: This replaces objects in the target database. Take a fresh backup first.

if [[ $# -lt 1 ]]; then
  echo "Usage: $0 <backup.sql.gz>"
  exit 1
fi

FILE="$1"
HOST="${POSTGRES_HOST:-localhost}"
PORT="${POSTGRES_PORT:-5432}"
DB="${POSTGRES_DB:-shaunerp}"
USER="${POSTGRES_USER:-shaunerp}"
export PGPASSWORD="${PGPASSWORD:-${POSTGRES_PASSWORD:-}}"

if [[ ! -f "${FILE}" ]]; then
  echo "File not found: ${FILE}"
  exit 2
fi

echo "Restoring ${FILE} into ${DB} @ ${HOST}:${PORT}"
gunzip -c "${FILE}" | psql -h "${HOST}" -p "${PORT}" -U "${USER}" -d "${DB}" -v ON_ERROR_STOP=1
echo "Restore complete."
