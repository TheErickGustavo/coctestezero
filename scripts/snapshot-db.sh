#!/bin/bash
# Snapshot (backup) do banco do UCS Zero. Seguro com o servidor NO AR
# (usa a API de backup do SQLite, que funciona com o banco aberto).
# Uso: bash scripts/snapshot-db.sh [nome]
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIVE="$ROOT/Ultrapowa Clash Server/bin/Release/ucsdb"
DEST_DIR="${BKDIR:-$ROOT/backups}"
NAME="${1:-ucsdb-zero-$(date +%Y-%m-%d-%H%M)-snapshot}"
DEST="$DEST_DIR/$NAME"
mkdir -p "$DEST_DIR"
python3 - "$LIVE" "$DEST" <<'PY'
import sqlite3, sys
src = sqlite3.connect(sys.argv[1])
dst = sqlite3.connect(sys.argv[2])
with dst:
    src.backup(dst)
src.close(); dst.close()
print("snapshot OK:", sys.argv[2])
PY
