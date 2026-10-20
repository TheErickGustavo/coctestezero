#!/bin/bash
# Wrapper de supervisao do UCS Zero: reinicia o servidor se morrer e registra o codigo de saida.
# Uso: nohup bash scripts/watch-server.sh >/dev/null 2>&1 &
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MR="${MR:-/home/eg/Downloads/coc-2015-blue-walls-lab/tools/mono-root}"
RUN="$ROOT/Ultrapowa Clash Server/bin/Release"
LOG="$ROOT/ucs-zero.log"
cd "$RUN" || exit 1
export PATH="$MR/usr/bin:$PATH"
export LD_LIBRARY_PATH="$MR/usr/lib/x86_64-linux-gnu"
export MONO_PATH="$RUN/packages"
while true; do
    mono ucs.exe >> "$LOG" 2>&1 < /dev/null
    CODE=$?
    echo "[WATCH] $(date '+%d/%m %H:%M:%S') mono saiu com codigo $CODE — reiniciando em 3s" >> "$LOG"
    if [ "$CODE" -eq 137 ]; then
        echo "[WATCH] codigo 137 = SIGKILL (provavel OOM killer / earlyoom)" >> "$LOG"
    elif [ "$CODE" -eq 139 ]; then
        echo "[WATCH] codigo 139 = SIGSEGV (segfault)" >> "$LOG"
    fi
    sleep 3
done
