#!/usr/bin/env python3
"""
The goal test: drive the port into a race and assert a lap completes.

Runs headless (rendering is not needed to count laps, and headless is ~4x
faster), navigates the menus with the recorded input script, idles while the AI
races, and watches a memory address for the lap counter to increase.

    python harness/verify-lap.py --addr 0x800XXXXX --width 8
    python harness/verify-lap.py --addr 0x800XXXXX --laps 2 --timeout 900

Exit code 0 means a lap was observed. Anything else means it was not, and the
reason is printed rather than guessed at.
"""
import argparse
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CUE = ROOT / "JetMotoPS1image" / "Jet Moto (USA).cue"
PORT = ROOT / "JetMoto" / "bin" / "Release" / "net10.0" / "JetMoto.dll"
SCRIPT = ROOT / "harness" / "race-run.txt"

RE_PEEK = re.compile(r"\[Peek\] 0x([0-9A-F]{8})=0x([0-9A-F]{8})")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--addr", required=True,
                    help="comma-separated lap-counter addresses (the standings array)")
    ap.add_argument("--settle", type=int, default=25,
                    help="seconds before the all-zero race-start marker is trusted; "
                         "RAM is zeroed at process start so it is trivially true early")
    ap.add_argument("--min-lap-seconds", type=int, default=15,
                    help="reject an increase faster than this as not a real lap")
    ap.add_argument("--width", type=int, default=8, choices=(8, 16, 32),
                    help="how many bits of the peeked word are the counter")
    ap.add_argument("--laps", type=int, default=1, help="laps that must be observed")
    ap.add_argument("--timeout", type=int, default=900)
    ap.add_argument("--input", default=str(SCRIPT))
    args = ap.parse_args()

    if not PORT.exists():
        print(f"port not built: {PORT}")
        return 2

    addrs = [a.strip().upper().replace("0X", "") for a in args.addr.split(",")]
    env = {
        **os.environ,
        "RECOMPONE_HEADLESS": "1",
        "RECOMPONE_INPUT": f"@{args.input}",
        "RECOMPONE_PEEK": args.addr,
    }

    mask = (1 << args.width) - 1
    latest = {}
    zero_run, race_start = 0, None
    peak, history = 0, []
    t0 = time.time()

    print(f"watching {len(addrs)} address(es) ({args.width}-bit) for {args.laps} lap(s); "
          f"{args.timeout}s budget")
    print("  waiting for the array to read all-zero, which marks a race start")
    p = subprocess.Popen(["dotnet", str(PORT), str(CUE)],
                         cwd=ROOT, env=env, stdout=subprocess.PIPE,
                         stderr=subprocess.STDOUT, text=True,
                         encoding="utf-8", errors="replace", bufsize=1)
    try:
        for line in p.stdout:
            now = time.time() - t0
            if now > args.timeout:
                print(f"\nTIMEOUT after {args.timeout}s")
                break
            m = RE_PEEK.search(line)
            if not m:
                continue
            latest[m.group(1)] = int(m.group(2), 16) & mask
            if len(latest) < len(addrs):
                continue

            # The array is sorted by standings, so an individual slot can go
            # down when riders swap places. The maximum across it is what
            # actually tracks race progress.
            hi = max(latest.values())

            # Anchor on an unambiguous event rather than a settle window: at a
            # race start every counter reads zero. Baselining on the first read
            # instead is what produced a bogus "0 -> 2 in 29s" when the array
            # still held pre-race values.
            if race_start is None:
                # RAM is zeroed at process start, so "all zero" is trivially
                # true before the game has booted -- an earlier version anchored
                # at t+1.4s and mislabelled the numbers that followed. Only
                # accept the marker once the game has had time to reach a race.
                if now < args.settle:
                    continue
                if hi == 0:
                    zero_run += 1
                    if zero_run >= 3:
                        race_start = time.time()
                        peak = 0
                        print(f"  t+{now:6.1f}s  race start detected (all zero)")
                else:
                    zero_run = 0
                continue

            if hi > peak:
                dt = time.time() - race_start
                history.append((f"lap {hi}", f"t+{dt:.0f}s"))
                print(f"  t+{now:6.1f}s  max {peak} -> {hi} "
                      f"({dt:.0f}s into the race) {sorted(latest.values())}")
                peak = hi
                if peak >= args.laps:
                    if dt < args.min_lap_seconds * peak:
                        print(f"      rejected: {dt:.0f}s for {peak} lap(s) is implausibly fast")
                        continue
                    print(f"\nLAP CONFIRMED: {peak} lap(s) completed, "
                          f"{dt:.0f}s into the race; {history}")
                    return 0
    finally:
        p.kill()

    print(f"\nNo lap observed. Increments: {history or '(none)'}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
