#!/usr/bin/env bash
# ──────────────────────────────────────────────────────────────────────────────
# setup.sh — One-shot dev environment bootstrap for dotnet-clean-architecture-base
#
# Usage:
#   bash setup.sh                      # auto-detects provider from appsettings.Development.json
#   bash setup.sh --provider postgres  # force PostgreSQL
#   bash setup.sh --provider sqlserver # force SQL Server
#   bash setup.sh --provider sqlite    # SQLite (no Docker needed)
#   bash setup.sh --skip-docker
#   bash setup.sh --skip-migrate
# ──────────────────────────────────────────────────────────────────────────────
set -euo pipefail

SKIP_DOCKER=false
SKIP_MIGRATE=false
PROVIDER=""

for arg in "$@"; do
  case $arg in
    --skip-docker)   SKIP_DOCKER=true  ;;
    --skip-migrate)  SKIP_MIGRATE=true ;;
    --provider=*)    PROVIDER="${arg#*=}" ;;
    --provider)      ;;   # next arg handled below
  esac
done

# Handle --provider <value> (space-separated)
PREV=""
for arg in "$@"; do
  [ "$PREV" = "--provider" ] && PROVIDER="$arg"
  PREV="$arg"
done

# Auto-detect provider from appsettings.Development.json if not forced
if [ -z "$PROVIDER" ]; then
  SETTINGS="src/EnterpriseApp.API/appsettings.Development.json"
  if command -v grep >/dev/null 2>&1 && [ -f "$SETTINGS" ]; then
    PROVIDER=$(grep -oP '"Provider"\s*:\s*"\K[^"]+' "$SETTINGS" 2>/dev/null || echo "PostgreSQL")
  else
    PROVIDER="PostgreSQL"
  fi
fi

PROVIDER_LC=$(echo "$PROVIDER" | tr '[:upper:]' '[:lower:]')
[ "$PROVIDER_LC" = "sqlite" ] && SKIP_DOCKER=true

echo "╔══════════════════════════════════════════════════════════╗"
echo "║  EnterpriseApp — Clean Architecture — Dev Setup          ║"
echo "║  Provider: $PROVIDER$(printf '%*s' $((48 - ${#PROVIDER})) '')║"
echo "╚══════════════════════════════════════════════════════════╝"

# ── Prerequisites ─────────────────────────────────────────────────────────────
echo ""
echo "▶ Checking prerequisites..."

command -v dotnet >/dev/null 2>&1 || { echo "❌ .NET SDK not found. Install from https://dot.net"; exit 1; }
if [ "$SKIP_DOCKER" = false ]; then
  command -v docker >/dev/null 2>&1 || { echo "⚠️  Docker not found — switching to SQLite."; PROVIDER_LC="sqlite"; SKIP_DOCKER=true; }
fi

DOTNET_MAJOR=$(dotnet --version | cut -d'.' -f1)
[ "$DOTNET_MAJOR" -lt 9 ] && { echo "❌ Requires .NET 9+. Found: $(dotnet --version)"; exit 1; }
echo "   ✅ .NET $(dotnet --version)"

# ── Restore + build ───────────────────────────────────────────────────────────
echo ""
echo "▶ Restoring NuGet packages..."
dotnet restore EnterpriseApp.sln

echo ""
echo "▶ Building solution..."
dotnet build EnterpriseApp.sln -c Debug --no-restore

# ── Docker services ───────────────────────────────────────────────────────────
if [ "$SKIP_DOCKER" = false ]; then
  echo ""
  echo "▶ Starting Docker services..."

  case "$PROVIDER_LC" in
    sqlserver)
      echo "   Starting SQL Server 2022 + Redis..."
      docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml up -d sqlserver redis
      echo "   Waiting for SQL Server (may take ~30 s on first run)..."
      until docker compose -f docker-compose.yml -f docker-compose.sqlserver.yml \
          exec -T sqlserver \
          /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "Your_password123!" \
          -Q "SELECT 1" -b -No >/dev/null 2>&1; do
        sleep 2
      done
      echo "   ✅ SQL Server ready"
      ;;
    *)
      echo "   Starting PostgreSQL + Redis..."
      docker compose up -d postgres redis
      echo "   Waiting for PostgreSQL..."
      until docker compose exec -T postgres pg_isready -U postgres -d EnterpriseAppDb >/dev/null 2>&1; do
        sleep 1
      done
      echo "   ✅ PostgreSQL ready"
      ;;
  esac

  echo "   Waiting for Redis..."
  until docker compose exec -T redis redis-cli ping 2>/dev/null | grep -q PONG; do sleep 1; done
  echo "   ✅ Redis ready"
fi

# ── EF Core migrations ────────────────────────────────────────────────────────
if [ "$SKIP_MIGRATE" = false ]; then
  echo ""
  echo "▶ Applying EF Core migrations (provider: $PROVIDER)..."

  if ! dotnet tool list -g | grep -q dotnet-ef; then
    echo "   Installing dotnet-ef..."
    dotnet tool install -g dotnet-ef
  fi

  export Database__Provider="$PROVIDER"

  dotnet ef database update \
    --project src/EnterpriseApp.Infrastructure \
    --startup-project src/EnterpriseApp.API \
    --verbose

  echo "   ✅ Database migrated"
fi

# ── Tests ─────────────────────────────────────────────────────────────────────
echo ""
echo "▶ Running tests..."
dotnet test EnterpriseApp.sln --no-build --logger "console;verbosity=minimal"

# ── Done ──────────────────────────────────────────────────────────────────────
echo ""
echo "╔══════════════════════════════════════════════════════════╗"
echo "║  Setup complete! 🚀                                       ║"
echo "║  Provider: $PROVIDER$(printf '%*s' $((48 - ${#PROVIDER})) '')║"
echo "║                                                          ║"
echo "║  Start API:  dotnet run --project src/EnterpriseApp.API  ║"
echo "║  Scalar UI:  https://localhost:5001/scalar/v1            ║"
echo "║  Health:     https://localhost:5001/health               ║"
echo "╚══════════════════════════════════════════════════════════╝"
