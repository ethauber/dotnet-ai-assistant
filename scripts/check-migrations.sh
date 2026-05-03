#!/usr/bin/env bash
# Pre-commit guard: fail if EF Core model changes are not covered by a migration.
#
# Requires EF Core 8+.
#
# Install as a git pre-commit hook:
#   cp scripts/check-migrations.sh .git/hooks/pre-commit
#   chmod +x .git/hooks/pre-commit

set -u
set -o pipefail

PROJECT="src/Infrastructure"
STARTUP="src/Api"

# Set this if your solution has more than one DbContext.
# Example:
#   CONTEXT="AppDbContext"
CONTEXT="${EF_MIGRATIONS_CONTEXT:-}"

cd "$(git rev-parse --show-toplevel)"

echo "[migration-guard] Checking for pending EF Core model changes..."

# Restore local dotnet tools when the repo uses a tool manifest.
if [ -f ".config/dotnet-tools.json" ]; then
  dotnet tool restore >/dev/null
fi

ARGS=(
  ef migrations has-pending-model-changes
  --project "$PROJECT"
  --startup-project "$STARTUP"
)

if [ -n "$CONTEXT" ]; then
  ARGS+=(--context "$CONTEXT")
fi

# In pre-commit, do not use --no-build by default.
# A stale build can produce a false result. Use this only in CI after a build.
if [ "${EF_MIGRATIONS_NO_BUILD:-0}" = "1" ]; then
  ARGS+=(--no-build)
fi

OUTPUT_FILE="$(mktemp)"
trap 'rm -f "$OUTPUT_FILE"' EXIT

if dotnet "${ARGS[@]}" >"$OUTPUT_FILE" 2>&1; then
  cat "$OUTPUT_FILE"
  echo "[migration-guard] OK: no pending model changes."
  exit 0
fi

cat "$OUTPUT_FILE"

if grep -qiE 'Changes have been made to the model since the last migration|pending model changes|Add a new migration' "$OUTPUT_FILE"; then
  echo ""
  echo "[migration-guard] FAIL: EF detected model changes not covered by a migration."
  echo ""
  echo "Fix:"
  echo "  dotnet ef migrations add <DescriptiveName> --project $PROJECT --startup-project $STARTUP${CONTEXT:+ --context $CONTEXT}"
  echo ""
  echo "Then commit the entity/model change and the new migration together."
  exit 1
fi

echo ""
echo "[migration-guard] ERROR: EF migration check failed for a reason other than pending model changes."
echo "Fix the error above. If the build is stale, run:"
echo "  dotnet build"

exit 1