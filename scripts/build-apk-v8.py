#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Patch do APK "clash v8 server" (CoC v8, 2016) para apontar ao NOSSO servidor
privado UCS Zero, do mesmo jeito que o scripts/build-apk.py faz com o client 7.65.

POR QUE:
  O client 7.65 (2015) nao tem no protocolo os dados do castelo do cla
  (castle_lvl / castle_total / alliance_unit_visit_capacity) - eles so existem no
  save local do client - entao o "escudo do cla" na barra de ataque nao aparece.
  Ja o client v8 (2016) TEM a feature, e o nosso servidor (UCS 0.6.1, 2016) e da
  geracao v8. Entao: patchar o APK v8 para o 127.0.0.1:9341 e usar o nosso
  servidor = laboratorio para ver o que o client v8 espera do castelo do cla.

O QUE O PATCH FAZ (no libg.so de todas as ABIs):
  clashofclansgamea.kunlun.com  ->  127.0.0.1\0\0\0...   (mesmo tamanho, C string)
  gamea.clashofclans.com        ->  127.0.0.1\0\0\0...   (fallback do app)
  9339                          ->  9341                 (porta)

USO:
  1) extrair o APK do celular (o app instalado "clash v8 server"):
     adb shell pm path com.supercell.clashofclans.v8.server
     adb pull <caminho> /home/eg/Downloads/clash-v8-server.apk
  2) python3 scripts/build-apk-v8.py
  3) instalar no celular:
     adb uninstall com.supercell.clashofclans.v8.server   # o app tem o MESMO package
     adb install /home/eg/Downloads/coctestezero/apk-work/clash-v8-server.apk
  4) adb reverse tcp:9341 tcp:9340   (o app aponta para 127.0.0.1:9341)
  5) abrir o app "clash v8 server" e logar no servidor privado
"""
import os
import shutil
import struct
import subprocess
import sys
import zipfile

ROOT = "/home/eg/Downloads/coctestezero/server"
WORK = os.path.join(ROOT, "apk-work")
BT = "/home/eg/Downloads/coc-2015-lab/tools/bt/android-14"
JDK = "/home/eg/Downloads/coc-2015-lab/tools/jdk"
SRC = "/home/eg/Downloads/coctestezero/downloads/clash-v8-server.apk"
UNSIGNED = os.path.join(WORK, "clash-v8-server-unsigned.apk")
ALIGNED = os.path.join(WORK, "clash-v8-server-aligned.apk")
OUT = "/home/eg/Downloads/coctestezero/clash-v8-server.apk"

HOSTS = [b"clashofclansgamea.kunlun.com", b"gamea.clashofclans.com"]
NEW_HOST = b"127.0.0.1"
PORT_OLD = b"9339"
PORT_NEW = b"9341"


def patch_so(data):
    n = 0
    for h in HOSTS:
        if h in data:
            # mesma largura: escreve o host novo e preenche o resto com \0
            data = data.replace(h, NEW_HOST + b"\x00" * (len(h) - len(NEW_HOST)))
            n += 1
    if PORT_OLD in data:
        data = data.replace(PORT_OLD, PORT_NEW)
        n += 1
    return data, n


def main():
    if not os.path.exists(SRC):
        print("APK de origem nao encontrado:", SRC)
        print("Siga o passo 1 do cabecalho (adb pull do app 'clash v8 server').")
        sys.exit(1)
    os.makedirs(WORK, exist_ok=True)
    zin = zipfile.ZipFile(SRC)
    patched = 0
    with zipfile.ZipFile(UNSIGNED, "w", zipfile.ZIP_DEFLATED) as zout:
        for item in zin.infolist():
            data = zin.read(item.filename)
            if item.filename.endswith("libg.so"):
                data, n = patch_so(data)
                patched += n
                print("  patch", item.filename, "->", n, "alteracoes")
            zout.writestr(item, data)
    zin.close()
    if patched == 0:
        print("AVISO: nada foi patchado (strings do host nao encontradas)")
    else:
        print("patch total:", patched, "alteracoes")

    subprocess.check_call([os.path.join(BT, "zipalign"), "-f", "-p", "4", UNSIGNED, ALIGNED])
    env = dict(os.environ)
    env["PATH"] = os.path.join(JDK, "bin") + ":" + env["PATH"]
    subprocess.check_call([
        os.path.join(BT, "apksigner"), "sign",
        "--ks", "/home/eg/Downloads/coctestezero/scripts/debug.keystore",
        "--ks-pass", "pass:android", "--key-pass", "pass:android",
        "--ks-key-alias", "androiddebugkey",
        "--out", OUT, ALIGNED,
    ], env=env)
    print("OK:", OUT, os.path.getsize(OUT), "bytes")
    print("Instale com: adb uninstall com.supercell.clashofclans.v8.server && adb install", OUT)
    print("E: adb reverse tcp:9341 tcp:9340")


if __name__ == "__main__":
    main()
