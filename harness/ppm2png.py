#!/usr/bin/env python3
"""Convert the runtime's binary PPM frame dumps to PNG.

Deliberately dependency-free (zlib + struct only) so the harness never needs
Pillow installed to be able to look at a frame.

    python harness/ppm2png.py harness/captures
"""
import struct
import sys
import zlib
from pathlib import Path


def read_ppm(path):
    data = path.read_bytes()
    # header: P6 <ws> W <ws> H <ws> MAX <single ws> then raw RGB
    fields, pos = [], 2
    while len(fields) < 3:
        while pos < len(data) and data[pos:pos + 1].isspace():
            pos += 1
        if data[pos:pos + 1] == b'#':
            while data[pos:pos + 1] not in (b'\n', b''):
                pos += 1
            continue
        start = pos
        while pos < len(data) and not data[pos:pos + 1].isspace():
            pos += 1
        fields.append(int(data[start:pos]))
    pos += 1
    w, h, _ = fields
    return w, h, data[pos:pos + w * h * 3]


def write_png(path, w, h, rgb):
    raw = b''.join(b'\x00' + rgb[y * w * 3:(y + 1) * w * 3] for y in range(h))

    def chunk(tag, payload):
        return (struct.pack('>I', len(payload)) + tag + payload
                + struct.pack('>I', zlib.crc32(tag + payload) & 0xFFFFFFFF))

    png = (b'\x89PNG\r\n\x1a\n'
           + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0))
           + chunk(b'IDAT', zlib.compress(raw, 6))
           + chunk(b'IEND', b''))
    path.write_bytes(png)


def main():
    target = Path(sys.argv[1] if len(sys.argv) > 1 else "harness/captures")
    files = sorted(target.glob("*.ppm")) if target.is_dir() else [target]
    if not files:
        print(f"no .ppm files in {target}")
        return 1
    for f in files:
        w, h, rgb = read_ppm(f)
        if len(rgb) < w * h * 3:
            print(f"{f.name}: truncated ({len(rgb)} of {w*h*3} bytes), skipped")
            continue
        out = f.with_suffix(".png")
        write_png(out, w, h, rgb)
        nonzero = sum(1 for b in rgb[::97] if b)          # cheap content probe
        print(f"{out.name}  {w}x{h}  {nonzero * 97 * 100 // len(rgb)}% non-black samples")
    return 0


if __name__ == "__main__":
    sys.exit(main())
