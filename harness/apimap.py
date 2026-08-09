#!/usr/bin/env python3
"""
Enumerate functions in an address range with size, call counts and callees.

Used to find the PSYQ public API stubs, which sit in a contiguous band and
mostly consist of a small function whose only job is to call the internal that
does the work. Routing must happen at these public entries, never at the
internals, because the internals take different arguments.

    python harness/apimap.py --exe raw.bin --base 0x801048B8 \
        --gen JetMoto2/generated/main.cs --from 0x8010DC00 --to 0x8010E400
"""
import argparse
import re
import struct
import sys
from collections import defaultdict


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--base", required=True)
    ap.add_argument("--gen", required=True)
    ap.add_argument("--from", dest="lo", required=True)
    ap.add_argument("--to", dest="hi", required=True)
    args = ap.parse_args()

    base = int(args.base, 16)
    lo, hi = int(args.lo, 16), int(args.hi, 16)
    code = open(args.exe, "rb").read()
    gen = open(args.gen, encoding="utf-8", errors="replace").read()

    starts = sorted(int(a, 16) for a in
                    re.findall(r"public static void func_([0-9A-Fa-f]{8})\(", gen))

    def owner(addr):
        a, b, best = 0, len(starts) - 1, None
        while a <= b:
            m = (a + b) // 2
            if starts[m] <= addr:
                best = starts[m]; a = m + 1
            else:
                b = m - 1
        return best

    callees = defaultdict(list)
    callers = defaultdict(set)
    ncalls = defaultdict(int)
    for off in range(0, len(code) - 4, 4):
        w = struct.unpack_from("<I", code, off)[0]
        if w >> 26 != 0x03:
            continue
        a = base + off
        dest = (a & 0xF0000000) | ((w & 0x03FFFFFF) << 2)
        o = owner(a)
        callees[o].append(dest)
        callers[dest].add(o)
        ncalls[dest] += 1

    print(f"{'addr':11} {'size':>5} {'callers':>7} {'calls':>6}  callees")
    for i, s in enumerate(starts):
        if not (lo <= s < hi):
            continue
        end = starts[i + 1] if i + 1 < len(starts) else s
        outs = sorted(set(callees.get(s, ())))
        tgt = ", ".join(f"0x{o:08X}" for o in outs[:4]) or "-"
        print(f"0x{s:08X} {end-s:5} {len(callers.get(s,())):7} {ncalls.get(s,0):6}  {tgt}")


if __name__ == "__main__":
    sys.exit(main())
