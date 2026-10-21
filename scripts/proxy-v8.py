#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Proxy/sniffer do protocolo do Clash of Clans entre o client v8 (no celular) e o
MAGIC-SERVER embutido no app "clash v8 server" (tambem no celular).

PARA QUE:
  O client v8 (2016) TEM o castelo do cla funcionando, e o nosso servidor
  (UCS 0.6.1, 2016) e da geracao v8. Este proxy deixa o client falar com o
  servidor v8 REAL e grava tudo que trafega - assim conseguimos ver EXATAMENTE o
  que o servidor v8 manda para o client no castelo do cla (e aplicar no 7.65).

COMO USAR (no PC):
    adb forward  tcp:9339 tcp:9339      # celular:9339 (servidor magic) -> PC:9339
    adb reverse tcp:9338 tcp:9340      # celular:9338 (client v8)     -> PC:9340
    python3 scripts/proxy-v8.py 9338 9339

  O client v8 aponta para 127.0.0.1:9338? NAO - ele aponta para 127.0.0.1:9339
  (a porta do servidor magic). Entao o reverse tem que ser:
    adb reverse tcp:9339 tcp:9340      # celular:9339 -> PC:9340 (o proxy)

  e o forward:
    adb forward tcp:9339 tcp:9339      # PC:9339 -> celular:9339 (servidor magic)
  O proxy escuta em 9340 (PC) e conecta em 127.0.0.1:9339 (PC, via forward).

GRAVACAO: cada frame (hex) vai para /tmp/opencode/v8/captura-v8.bin (com os
tamanhos) e o trafego e repassado intacto.
"""
import socket
import struct
import sys
import threading
import time

LISTEN = int(sys.argv[1]) if len(sys.argv) > 1 else 9340
TARGET = int(sys.argv[2]) if len(sys.argv) > 2 else 9339
OUT = "/tmp/opencode/v8/captura-v8.bin"

lock = threading.Lock()
out = open(OUT, "ab")
seq = [0]


def log(tag, data):
    with lock:
        seq[0] += 1
        out.write(b"--- #%d %s len=%d\n" % (seq[0], tag.encode(), len(data)))
        out.write(b"".join(b"%02x " % c for c in data[:512]))
        out.write(b"\n")
        out.flush()


def pump(src, dst, tag):
    try:
        while True:
            data = src.recv(65536)
            if not data:
                break
            log(tag, data)
            dst.sendall(data)
    except Exception as e:
        print("pump", tag, "erro:", e)
    finally:
        try:
            dst.shutdown(socket.SHUT_WR)
        except Exception:
            pass


def handle(client):
    print("conexao do client v8")
    try:
        server = socket.create_connection(("127.0.0.1", TARGET), timeout=10)
    except Exception as e:
        print("nao conectou no servidor v8:", e)
        client.close()
        return
    t1 = threading.Thread(target=pump, args=(client, server, "C->S"), daemon=True)
    t2 = threading.Thread(target=pump, args=(server, client, "S->C"), daemon=True)
    t1.start()
    t2.start()
    t1.join()
    t2.join()
    client.close()
    server.close()
    print("conexao encerrada")


def main():
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    s.bind(("127.0.0.1", LISTEN))
    s.listen(5)
    print("proxy v8 escutando em 127.0.0.1:%d -> 127.0.0.1:%d | captura em %s"
          % (LISTEN, TARGET, OUT))
    while True:
        c, _ = s.accept()
        threading.Thread(target=handle, args=(c,), daemon=True).start()


if __name__ == "__main__":
    main()
