#!/usr/bin/env python3
"""
Convert captured PPM frames to PNG.

Native captures come out at the console's own 320x240 (or 512x240), which is
hard to read on a modern display, so frames smaller than 640 wide are scaled up
with nearest-neighbour. That keeps every pixel exactly as rendered -- it makes
the image legible without inventing detail that was never drawn.

    python harness/ppm2png.py <dir> [--scale N]
"""
import glob
import os
import sys

from PIL import Image


def convert(path, scale):
    im = Image.open(path).convert("RGB")
    w, h = im.size
    if scale is None:
        scale = max(1, -(-640 // w)) if w < 640 else 1
    if scale > 1:
        im = im.resize((w * scale, h * scale), Image.NEAREST)
    out = path.rsplit(".", 1)[0] + ".png"
    im.save(out)

    px = im.load()
    lit = tot = 0
    for y in range(0, im.size[1], 7):
        for x in range(0, im.size[0], 7):
            r, g, b = px[x, y]
            tot += 1
            if r + g + b > 24:
                lit += 1
    print(f"{os.path.basename(out)}  {w}x{h} -> {im.size[0]}x{im.size[1]}  "
          f"{lit * 100 // max(tot, 1)}% non-black")


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    scale = None
    for a in sys.argv[1:]:
        if a.startswith("--scale"):
            scale = int(a.split("=", 1)[1]) if "=" in a else None
    target = args[0] if args else "."
    for p in sorted(glob.glob(os.path.join(target, "*.ppm"))):
        convert(p, scale)


if __name__ == "__main__":
    main()
