#!/bin/bash
# Alinha + assina o APK do cliente (CoC 7.65.5 patcheado para o servidor local).
# Requer: build-apk.py (gera coctestezero-unsigned.apk) e as ferramentas em
# /home/eg/Downloads/coc-2015-lab/tools (bt/android-14, jdk).
set -e
Z="${Z:-/home/eg/Downloads/coctestezero}"
BT="${BT:-/home/eg/Downloads/coc-2015-lab/tools/bt/android-14}"
JDK="${JDK:-/home/eg/Downloads/coc-2015-lab/tools/jdk}"
W="$Z/apk-work"
"$BT/zipalign" -f -p 4 "$W/coctestezero-unsigned.apk" "$W/coctestezero-aligned.apk"
PATH="$JDK/bin:$PATH" "$BT/apksigner" sign --ks "$Z/scripts/debug.keystore" --ks-pass pass:android --key-pass pass:android --ks-key-alias androiddebugkey --out "$Z/coctestezero.apk" "$W/coctestezero-aligned.apk"
PATH="$JDK/bin:$PATH" "$BT/apksigner" verify --print-certs "$Z/coctestezero.apk" | head -2
echo "OK: $Z/coctestezero.apk"
