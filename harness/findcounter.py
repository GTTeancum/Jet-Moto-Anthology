#!/usr/bin/env python3
"""
Locate counter-like values (lap number, position, checkpoint) by diffing RAM
snapshots taken across a race.

The lap counter is the goal condition for this whole project, so it needs to be
found by observation rather than by reverse engineering the HUD. Snapshots come
from RECOMPONE_RAMDUMP_DIR; this looks for addresses that behave like a lap
count: small, non-decreasing, and stepping up by one.

    python harness/findcounter.py harness/ram
    python harness/findcounter.py harness/ram --width 8 --max 8

Reports candidate addresses in PS1 terms (0x800xxxxx).
"""
import argparse
import sys
from pathlib import Path

RAM_BASE = 0x80000000


def load(dirpath):
    files = sorted(Path(dirpath).glob("ram-*.bin"))
    if len(files) < 3:
        print(f"need at least 3 snapshots in {dirpath}, found {len(files)}")
        return None
    snaps = [f.read_bytes() for f in files]
    n = min(len(s) for s in snaps)
    print(f"{len(snaps)} snapshots, {n} bytes each")
    return [s[:n] for s in snaps]


def candidates(snaps, width, lo, hi, strict_step):
    """Offsets whose value is small, non-decreasing, and actually changes."""
    n = len(snaps[0])
    step = width // 8
    out = []

    def val(s, off):
        if width == 8:
            return s[off]
        if width == 16:
            return s[off] | (s[off + 1] << 8)
        return s[off] | (s[off + 1] << 8) | (s[off + 2] << 16) | (s[off + 3] << 24)

    first, last = snaps[0], snaps[-1]
    for off in range(0, n - step, step):
        a, b = val(first, off), val(last, off)
        # cheap rejects first: must rise, stay small, and end in range
        if b <= a or b > hi or a > hi or b < lo:
            continue
        seq = [val(s, off) for s in snaps]
        ok = True
        for i in range(1, len(seq)):
            d = seq[i] - seq[i - 1]
            if d < 0 or d > (1 if strict_step else hi):
                ok = False
                break
        if ok:
            out.append((off, seq))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dir", nargs="?", default="harness/ram")
    ap.add_argument("--width", type=int, default=0,
                    help="bit width to scan (8/16/32); default tries all three")
    ap.add_argument("--min", type=int, default=1, help="lowest plausible final value")
    ap.add_argument("--max", type=int, default=12, help="highest plausible final value")
    ap.add_argument("--loose", action="store_true",
                    help="allow jumps larger than 1 between snapshots")
    ap.add_argument("--limit", type=int, default=25)
    args = ap.parse_args()

    snaps = load(args.dir)
    if snaps is None:
        return 1

    widths = [args.width] if args.width else [8, 16, 32]
    total = 0
    for w in widths:
        found = candidates(snaps, w, args.min, args.max, not args.loose)
        print(f"\n=== {w}-bit: {len(found)} candidate(s) ===")
        for off, seq in found[:args.limit]:
            print(f"  0x{RAM_BASE + off:08X}  {seq}")
        if len(found) > args.limit:
            print(f"  ... {len(found) - args.limit} more")
        total += len(found)

    if total == 0:
        print("\nNothing matched. If the race never started, the snapshots are "
              "all menu state; check the frame dumps first.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
