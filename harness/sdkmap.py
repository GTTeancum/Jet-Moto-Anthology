#!/usr/bin/env python3
"""
Identify PSYQ SDK functions in a recompiled PS1 executable.

Both Jet Moto games kept the PSYQ debug string pool in their retail builds, and
each library function prints a string bearing its own name. Reconstructing the
`lui`/`addiu` pairs that materialise those string addresses and mapping each
reference back to its enclosing function gives ground-truth names for free.

The names that matter are the *public* wrappers, not the internals that do the
printing: RecompOne routes by name, and the internals take different arguments.
The public entry is normally a small function whose only callee is the internal.

    python harness/sdkmap.py --exe <raw.bin> --base 0x801048B8 \
                             --gen JetMoto2/generated/main.cs

Prints, for each located string: the function that prints it, and any small
function whose only call is to that one (its public wrapper).
"""
import argparse
import re
import struct
import sys
from collections import defaultdict

STRINGS = {
    "CD_init": "CdInit", "CD_read": "CdRead", "CD_ready": "CdReady",
    "CD_sync": "CdSync", "CD_datasync": "CdDataSync", "CD_cw": "CD_cw",
    "ResetGraph(%d)": "ResetGraph", "DrawSync(%d)": "DrawSync",
    "VSync: timeout": "VSync", "DrawSyncCallback": "DrawSyncCallback",
    "ClearImage": "ClearImage", "LoadImage": "LoadImage",
    "StoreImage": "StoreImage", "MoveImage": "MoveImage",
    "GPU timeout": "GPU_timeout", "MDEC_in_sync": "MDEC_in_sync",
    "MDEC_out_sync": "MDEC_out_sync",
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe", required=True)
    ap.add_argument("--base", required=True)
    ap.add_argument("--gen", required=True)
    args = ap.parse_args()

    base = int(args.base, 16)
    code = open(args.exe, "rb").read()
    gen = open(args.gen, encoding="utf-8", errors="replace").read()

    starts = sorted(int(a, 16) for a in
                    re.findall(r"public static void func_([0-9A-Fa-f]{8})\(", gen))
    if not starts:
        print("no func_ symbols in the generated source", file=sys.stderr)
        return 2

    def owner(addr):
        lo, hi, best = 0, len(starts) - 1, None
        while lo <= hi:
            mid = (lo + hi) // 2
            if starts[mid] <= addr:
                best = starts[mid]; lo = mid + 1
            else:
                hi = mid - 1
        return best

    # locate each wanted string in the pool
    targets = {}
    for needle, name in STRINGS.items():
        for m in re.finditer(re.escape(needle.encode()), code):
            s = m.start()
            while s > 0 and 0x20 <= code[s - 1] < 0x7F:
                s -= 1
            targets[base + s] = name
            break

    # every jal, so callers/callees can be answered
    callees = defaultdict(set)
    ncalls = defaultdict(int)
    callers = defaultdict(set)
    hi_reg, refs = {}, defaultdict(set)
    for off in range(0, len(code) - 4, 4):
        w = struct.unpack_from("<I", code, off)[0]
        op, a = w >> 26, base + off
        if op == 0x03:
            dest = (a & 0xF0000000) | ((w & 0x03FFFFFF) << 2)
            o = owner(a)
            callees[o].add(dest); callers[dest].add(o); ncalls[dest] += 1
        elif op == 0x0F:
            hi_reg[(w >> 16) & 0x1F] = (w & 0xFFFF) << 16
        elif op == 0x09:
            rs, imm = (w >> 21) & 0x1F, w & 0xFFFF
            if imm >= 0x8000:
                imm -= 0x10000
            if rs in hi_reg:
                v = (hi_reg[rs] + imm) & 0xFFFFFFFF
                if v in targets:
                    refs[targets[v]].add(owner(a))

    idx = {s: i for i, s in enumerate(starts)}

    def size_of(a):
        i = idx.get(a)
        return starts[i + 1] - a if i is not None and i + 1 < len(starts) else 0

    print(f"{'name':18} {'internal (prints it)':22} {'callers':>7}   public wrapper candidates")
    for name in STRINGS.values():
        for internal in sorted(refs.get(name, ())):
            wraps = [c for c in callers.get(internal, ())
                     if c is not None and len(callees[c]) == 1 and size_of(c) <= 80]
            w = ", ".join(f"0x{c:08X}(sz={size_of(c)})" for c in sorted(wraps)) or "-"
            print(f"{name:18} 0x{internal:08X} sz={size_of(internal):<8} "
                  f"{len(callers.get(internal, ())):7}   {w}")


if __name__ == "__main__":
    sys.exit(main())
