#!/bin/bash
# Restaura um snapshot no banco do UCS Zero.
# Uso: bash scripts/restore-db.sh <nome-no-backups|caminho-completo>
# - Para o servidor automaticamente (porta 9340)
# - Faz backup de seguranca do banco ATUAL antes de sobrescrever
# - NAO sobe o servidor de volta (rode: nohup bash scripts/watch-server.sh &)
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIVE="$ROOT/Ultrapowa Clash Server/bin/Release/ucsdb"
BKDIR="${BKDIR:-$ROOT/backups}"
SRC="$1"
if [ -z "$SRC" ]; then
  echo "uso: $0 <nome-no-backups|caminho>"
  echo "disponiveis (mais novos primeiro):"
  ls -t "$BKDIR" 2>/dev/null | head -12
  exit 1
fi
[ -f "$SRC" ] || SRC="$BKDIR/$SRC"
if [ ! -f "$SRC" ]; then
  echo "nao achei: $1"
  exit 1
fi

PID=$(ss -ltnp 2>/dev/null | grep ':9340' | grep -o 'pid=[0-9]*' | cut -d= -f2 | head -1)
if [ -n "$PID" ]; then
  echo "parando servidor (pid $PID)..."
  kill "$PID"
  sleep 2
fi
if ss -ltnp 2>/dev/null | grep -q ':9340'; then
  echo "ERRO: porta 9340 ainda ocupada; abortando"
  exit 1
fi

mkdir -p "$BKDIR"
SAFE="$BKDIR/ucsdb-zero-$(date +%Y-%m-%d-%H%M)-pre-restore"
python3 - "$LIVE" "$SAFE" <<'PY'
import sqlite3, sys, os
if os.path.exists(sys.argv[1]):
    src = sqlite3.connect(sys.argv[1]); dst = sqlite3.connect(sys.argv[2])
    with dst:
        src.backup(dst)
    src.close(); dst.close()
    print("backup de seguranca do atual:", sys.argv[2])
PY

cp "$SRC" "$LIVE"
echo "RESTAURADO: $SRC"
echo "suba com: nohup bash scripts/watch-server.sh >/dev/null 2>&1 &"
