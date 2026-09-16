#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="$HOME/.dotnet:$HOME/.local/bin:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

mkdir -p "$ROOT/data/uploads" "$ROOT/data/recordings"

if ! curl -sf http://127.0.0.1:7880/ >/dev/null 2>&1; then
  echo "Starting LiveKit SFU on :7880"
  livekit-server --config "$ROOT/infra/livekit/livekit.local.yaml" >/tmp/livekit-server.log 2>&1 &
  echo $! > /tmp/livekit-server.pid
  sleep 1
fi

echo "LiveKit logs: /tmp/livekit-server.log"
echo "Start the API:  cd api/VisitMeet.Api && ASPNETCORE_URLS=http://127.0.0.1:5088 dotnet run"
echo "Start the web:  cd web && npm run dev"
