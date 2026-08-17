#!/usr/bin/env python3
"""
Convert PCSX-Redux reference dumps (harness/jm3-refcap.lua) to PNG.

Geometry is in the filename -- frame-NNNN-WxH-bB.bin -- because doing the
BGR555 conversion in Lua costs 76800 iterations a frame and cannot keep up with
the emulator. b0 is BGR555 halfwords, b1 is 24-bit, 3 bytes per pixel.

    python harness/ref2png.py <dir> [--scale N]
"""
import glob
import os
import re
import sys

from PIL import Image

NAME = re.compile(r"frame-(\d+)-(\d+)x(\d+)-b(\d)\.bin$")


def convert(path, scale):
    m = NAME.search(os.path.basename(path))
    if not m:
        return None
    idx, w, h, bpp = int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4))
    if w == 0 or h == 0:
        return None
    data = open(path, "rb").read()
    im = Image.new("RGB", (w, h))
    px = im.load()
    if bpp == 0:
        need = w * h * 2
        if len(data) < need:
            return None
        for y in range(h):
            row = y * w * 2
            for x in range(w):
                v = data[row + x * 2] | (data[row + x * 2 + 1] << 8)
                px[x, y] = ((v & 0x1F) << 3, ((v >> 5) & 0x1F) << 3, ((v >> 10) & 0x1F) << 3)
    else:
        need = w * h * 3
        if len(data) < need:
            return None
        for y in range(h):
            row = y * w * 3
            for x in range(w):
                o = row + x * 3
                px[x, y] = (data[o], data[o + 1], data[o + 2])
    if scale > 1:
        im = im.resize((w * scale, h * scale), Image.NEAREST)
    out = path[: -len(".bin")] + ".png"
    im.save(out)
    return out, w, h, bpp


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    scale = 1
    for a in sys.argv[1:]:
        if a.startswith("--scale"):
            scale = int(a.split("=", 1)[1])
    target = args[0] if args else "."
    n = 0
    for p in sorted(glob.glob(os.path.join(target, "frame-*.bin"))):
        r = convert(p, scale)
        if r:
            n += 1
            if n <= 3 or n % 25 == 0:
                print(f"{os.path.basename(r[0])}  {r[1]}x{r[2]} bpp={r[3]}")
    print(f"{n} frames converted")


if __name__ == "__main__":
    main()
