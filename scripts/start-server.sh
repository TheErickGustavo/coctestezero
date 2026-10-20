#!/bin/bash
# Sobe o servidor UCS Zero em primeiro plano (log no ucs-zero.log).
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MR="${MR:-/home/eg/Downloads/coc-2015-blue-walls-lab/tools/mono-root}"
RUN="$ROOT/Ultrapowa Clash Server/bin/Release"
cd "$RUN" || exit 1
export PATH="$MR/usr/bin:$PATH"
export LD_LIBRARY_PATH="$MR/usr/lib/x86_64-linux-gnu"
export MONO_PATH="$RUN/packages"
exec mono ucs.exe >> "$ROOT/ucs-zero.log" 2>&1 < /dev/null
