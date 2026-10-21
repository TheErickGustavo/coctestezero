#!/bin/bash
# Compila o servidor UCS Zero (fork do Ultrapowa Clash Server) no Linux.
#
# IMPORTANTE (Linux):
#  - use ESTE script, nunca xbuild/msbuild: a tarefa MSBuild "EntityDeploy" (que embute
#    o metadata do Entity Framework) nao funciona no Mono/Linux. Aqui os recursos do EF
#    sao embutidos na mao via -resource:.
#  - o compilador (mcs) vem do "mono-root" customizado, nao do mono do sistema.
#  - todo .cs novo precisa estar em scripts/sources.rsp (casing exato do arquivo),
#    senao ele simplesmente nao e compilado.
#
# Uso: bash scripts/build-server.sh
set -e

# raiz do repositorio (pasta que contem "Ultrapowa Clash Server/" e "packages/")
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LAB="$ROOT/Ultrapowa Clash Server"
# mono-root customizado (mcs + libs do mono). Ajuste MR se estiver em outro lugar.
MR="${MR:-/home/eg/Downloads/coc-2015-blue-walls-lab/tools/mono-root}"

export PATH="$MR/usr/bin:$PATH"
export LD_LIBRARY_PATH="$MR/usr/lib/x86_64-linux-gnu"

cd "$LAB"
mcs -target:exe -out:bin/Release/ucs.exe -debug -unsafe \
  -r:../packages/EntityFramework.dll -r:../packages/EntityFramework.SqlServer.dll \
  -r:../packages/Newtonsoft.Json.dll -r:../packages/MySql.Data.dll -r:../packages/Ionic.Zip.dll \
  -r:../packages/System.Data.SQLite.dll -r:../packages/System.Data.SQLite.EF6.dll -r:../packages/System.Data.SQLite.Linq.dll \
  -r:System.Data.Linq -r:System.Web.Extensions -r:System.Runtime.Serialization -r:System.Security \
  -r:System.Data.DataSetExtensions -r:Microsoft.CSharp -r:WindowsBase -r:System.ComponentModel.DataAnnotations -r:System.Configuration \
  -resource:Database/ucsdb.sqlite.csdl,Database.ucsdb.sqlite.csdl \
  -resource:Database/ucsdb.sqlite.ssdl,Database.ucsdb.sqlite.ssdl \
  -resource:Database/ucsdb.sqlite.msl,Database.ucsdb.sqlite.msl \
  "@$ROOT/scripts/sources.rsp"
echo "BUILD OK: $(ls -la bin/Release/ucs.exe | awk '{print $5}') bytes"
