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
    ap.add_argument("--settle", type=int, default=60,
                    help="seconds to ignore at the start, while the race loads and the "
                         "addresses still hold pre-race garbage")
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
    baseline = None
    peak, peak_ts, history = None, None, []
    t0 = time.time()

    print(f"watching {len(addrs)} address(es) ({args.width}-bit) for {args.laps} lap(s); "
          f"ignoring the first {args.settle}s, {args.timeout}s budget")
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

            # Settle first: before the race initialises these hold stale values,
            # which is exactly what made a naive first-read baseline report a
            # bogus "0 -> 2 in 29s".
            if now < args.settle:
                continue
            if baseline is None:
                baseline, peak, peak_ts = hi, hi, time.time()
                print(f"  t+{now:6.1f}s  baseline {baseline} {sorted(latest.values())}")
                continue

            if hi > peak:
                dt = time.time() - peak_ts
                history.append((round(now, 1), hi, round(dt, 1)))
                print(f"  t+{now:6.1f}s  max {peak} -> {hi} "
                      f"(+{dt:.0f}s) {sorted(latest.values())}")
                if dt < args.min_lap_seconds:
                    print(f"      rejected: {dt:.0f}s is too fast to be a lap")
                    baseline = hi   # resync and keep watching
                peak, peak_ts = hi, time.time()

            if peak - baseline >= args.laps:
                print(f"\nLAP CONFIRMED: max lap count {baseline} -> {peak}; "
                      f"increments at {history}")
                return 0
    finally:
        p.kill()

    print(f"\nNo lap observed. Increments: {history or '(none)'}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
