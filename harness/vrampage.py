#!/usr/bin/env python3
"""
Decode a texture page + CLUT out of a raw VRAM dump (RECOMPONE_DUMP_VRAM).

VRAM is 1024x512 16-bit halfwords. A texture page is 64 halfwords wide by 256
lines; at 8bpp that is 128 texels across, at 4bpp 256. CLUT coordinates are
absolute VRAM pixel coordinates.

    python harness/vrampage.py <vram.bin> <pageX> <pageY> <clutX> <clutY> <bpp> <out.png>

Exists so a claim about what a texture contains can be looked at rather than
argued about -- the failure mode this project keeps hitting.
"""
import struct
import sys

from PIL import Image

W, H = 1024, 512


def main():
    path, px, py, cx, cy, bpp, out = sys.argv[1:8]
    px, py, cx, cy, bpp = int(px), int(py), int(cx), int(cy), int(bpp)
    data = open(path, "rb").read()
    vram = struct.unpack("<%dH" % (W * H), data[: W * H * 2])

    def at(x, y):
        return vram[y * W + x]

    def rgb(v):
        return ((v & 0x1F) << 3, ((v >> 5) & 0x1F) << 3, ((v >> 10) & 0x1F) << 3)

    entries = 256 if bpp == 8 else 16
    pal = [rgb(at(cx + i, cy)) for i in range(entries)]

    tw = 128 if bpp == 8 else 256
    im = Image.new("RGB", (tw, 256))
    o = im.load()
    for y in range(256):
        for x in range(tw):
            if bpp == 8:
                hw = at(px + (x >> 1), py + y)
                idx = (hw >> ((x & 1) * 8)) & 0xFF
            else:
                hw = at(px + (x >> 2), py + y)
                idx = (hw >> ((x & 3) * 4)) & 0xF
            o[x, y] = pal[idx]
    im = im.resize((im.size[0] * 3, im.size[1] * 3), Image.NEAREST)
    im.save(out)

    lit = sum(1 for y in range(0, 256, 4) for x in range(0, tw, 4) if sum(o[x, y]) > 60)
    tot = len(range(0, 256, 4)) * len(range(0, tw, 4))
    print(f"{out}: {tw}x256 bpp={bpp} page=({px},{py}) clut=({cx},{cy})  "
          f"{lit * 100 // tot}% of sampled texels brighter than 60 total")
    print("  palette[0..7]:", pal[:8])


if __name__ == "__main__":
    main()
