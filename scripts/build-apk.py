#!/usr/bin/env python3
import zipfile, struct, os, io

ORIG = "/home/eg/Downloads/coc-2015-blue-walls-lab/downloads/oldclash-7.65.apk"
BASE_SO = "/home/eg/Downloads/coctestezero/apk-work/base-libg-lan.so"
ROOT = "/home/eg/Downloads/coctestezero"
WORK = os.path.join(ROOT, "apk-work")
ICONS = os.path.join(WORK, "icons")
OUT = os.path.join(WORK, "coctestezero-unsigned.apk")

os.makedirs(ICONS, exist_ok=True)

so = bytearray(open(BASE_SO, "rb").read())
assert so.count(b"9340") == 1, so.count(b"9340")
i = so.find(b"9340")
so[i:i + 4] = b"9341"
assert so.count(b"127.0.0.1") >= 4
print("libg.so: porta 9340->9341 ok, 127.0.0.1 x", so.count(b"127.0.0.1"))

z = zipfile.ZipFile(ORIG)
m = bytearray(z.read("AndroidManifest.xml"))

total_size = struct.unpack("<I", m[4:8])[0]
assert total_size == len(m), (total_size, len(m))
pool = 8
chunk_size = struct.unpack("<I", m[pool + 4:pool + 8])[0]
sc = struct.unpack("<I", m[pool + 8:pool + 12])[0]
strstart = struct.unpack("<I", m[pool + 20:pool + 24])[0]
flags = struct.unpack("<I", m[pool + 16:pool + 20])[0]
assert flags == 0
offs = struct.unpack("<%dI" % sc, m[pool + 28:pool + 28 + 4 * sc])
data_base = pool + strstart

def sdata(idx):
    o = data_base + offs[idx]
    ln = struct.unpack("<H", m[o:o + 2])[0]
    return o, ln, bytes(m[o + 2:o + 2 + ln * 2]).decode("utf-16-le")

o35, l35, s35 = sdata(35)
assert s35 == "com.supercell.clashofclant", s35
new35 = "com.supercell.coctestezero".encode("utf-16-le")
m[o35 + 2:o35 + 2 + len(new35)] = new35
print("manifest: pacote idx35 ->", sdata(35)[2])

o51, l51, s51 = sdata(51)
assert s51 == "com.supercell.clashofclant.permission.C2D_MESSAGE", s51
new51 = "com.supercell.coctestezero.permission.C2D_MESSAGE".encode("utf-16-le")
assert len(new51) == l51 * 2
m[o51 + 2:o51 + 2 + len(new51)] = new51
print("manifest: permission idx51 ->", sdata(51)[2])

o98, l98, s98 = sdata(98)
assert s98 == "OldClash", s98
assert o98 + 2 + 16 <= data_base + chunk_size
pad = data_base + chunk_size - (o98 + 2 + 16)
new98 = "coctestezero".encode("utf-16-le")
m = m[:o98] + struct.pack("<H", 12) + new98 + m[o98 + 2 + 16:]
struct.pack_into("<I", m, pool + 4, chunk_size + 8)
struct.pack_into("<I", m, 4, total_size + 8)
print("manifest: label idx98 -> coctestezero (pad pos-pool era %d bytes)" % pad)
assert bytes(m).count("OldClash".encode("utf-16-le")) == 0

open(os.path.join(WORK, "AndroidManifest-patched.xml"), "wb").write(m)

from PIL import Image, ImageDraw, ImageFont
icon_files = {}
for n in z.namelist():
    if "ic_launcher" in n and n.endswith(".png"):
        img = Image.open(io.BytesIO(z.read(n)))
        w, h = img.size
        im = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        p = max(2, w // 16)
        rad = max(4, w // 6)
        d.rounded_rectangle([p, p, w - 1 - p, h - 1 - p], radius=rad,
                            fill=(20, 60, 30, 255), outline=(255, 255, 255, 255), width=max(2, w // 32))
        f1 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", int(h * 0.33))
        t1 = "CoC"
        bb = d.textbbox((0, 0), t1, font=f1)
        d.text(((w - (bb[2] - bb[0])) / 2 - bb[0], h * 0.10), t1, font=f1, fill=(255, 255, 255, 255))
        f2 = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", int(h * 0.26))
        t2 = "ZERO"
        bb = d.textbbox((0, 0), t2, font=f2)
        d.text(((w - (bb[2] - bb[0])) / 2 - bb[0], h * 0.56), t2, font=f2, fill=(90, 230, 120, 255))
        buf = io.BytesIO()
        im.save(buf, "PNG")
        icon_files[n] = buf.getvalue()
print("icones gerados:", len(icon_files))

zo = zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED)
replaced = 0
for item in z.infolist():
    if item.filename.startswith("META-INF/"):
        continue
    data = z.read(item.filename)
    if item.filename == "AndroidManifest.xml":
        data = bytes(m)
    elif item.filename == "classes.dex":
        dex_path = os.path.join(WORK, "aptools", "classes-patched.dex")
        assert os.path.exists(dex_path), dex_path
        data = open(dex_path, "rb").read()
        replaced += 1
    elif item.filename.endswith("libg.so"):
        data = bytes(so)
        replaced += 1
    elif item.filename in icon_files:
        data = icon_files[item.filename]
        replaced += 1
    zi = zipfile.ZipInfo(item.filename, date_time=item.date_time)
    zi.compress_type = item.compress_type
    zi.external_attr = item.external_attr
    zo.writestr(zi, data)
zo.close()
print("reempacotado (so+icones):", replaced, "|", OUT, os.path.getsize(OUT), "bytes")
