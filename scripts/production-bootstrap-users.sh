#!/usr/bin/env bash
set -euo pipefail

# Bootstrap production users without committing passwords.
# Requires:
#   PROD_ADMIN_PASSWORD
#   PROD_ACCOUNTANT_PASSWORD
# Optional:
#   PROD_ADMIN_EMAIL (default shauninfomediaopc@gmail.com)
#   PROD_ACCOUNTANT_EMAIL (default arokiabakkianathanl@gmail.com)
#   ConnectionStrings__DefaultConnection

if [[ -z "${PROD_ADMIN_PASSWORD:-}" || -z "${PROD_ACCOUNTANT_PASSWORD:-}" ]]; then
  echo "Set PROD_ADMIN_PASSWORD and PROD_ACCOUNTANT_PASSWORD in the environment."
  exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${ROOT}"

echo "Running ShaunERP.Tools bootstrap-users..."
dotnet run --project src/ShaunERP.Tools -- bootstrap-users \
  --admin-email "${PROD_ADMIN_EMAIL:-shauninfomediaopc@gmail.com}" \
  --admin-password "${PROD_ADMIN_PASSWORD}" \
  --accountant-email "${PROD_ACCOUNTANT_EMAIL:-arokiabakkianathanl@gmail.com}" \
  --accountant-password "${PROD_ACCOUNTANT_PASSWORD}"
