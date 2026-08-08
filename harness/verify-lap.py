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
    ap.add_argument("--addr", required=True, help="lap counter address, e.g. 0x800A1234")
    ap.add_argument("--width", type=int, default=8, choices=(8, 16, 32),
                    help="how many bits of the peeked word are the counter")
    ap.add_argument("--laps", type=int, default=1, help="laps that must be observed")
    ap.add_argument("--timeout", type=int, default=900)
    ap.add_argument("--input", default=str(SCRIPT))
    args = ap.parse_args()

    if not PORT.exists():
        print(f"port not built: {PORT}")
        return 2

    env = {
        **os.environ,
        "RECOMPONE_HEADLESS": "1",
        "RECOMPONE_INPUT": f"@{args.input}",
        "RECOMPONE_PEEK": args.addr,
    }

    mask = (1 << args.width) - 1
    seen, start, first_ts = [], None, None
    t0 = time.time()

    print(f"watching {args.addr} ({args.width}-bit) for {args.laps} lap(s), "
          f"{args.timeout}s budget")
    p = subprocess.Popen([sys.executable and "dotnet", str(PORT), str(CUE)],
                         cwd=ROOT, env=env, stdout=subprocess.PIPE,
                         stderr=subprocess.STDOUT, text=True,
                         encoding="utf-8", errors="replace", bufsize=1)
    try:
        for line in p.stdout:
            if time.time() - t0 > args.timeout:
                print(f"\nTIMEOUT after {args.timeout}s")
                break
            m = RE_PEEK.search(line)
            if not m:
                continue
            v = int(m.group(2), 16) & mask
            if start is None:
                start, first_ts = v, time.time()
                print(f"  initial value {v}")
                continue
            if not seen or seen[-1] != v:
                seen.append(v)
                print(f"  t+{time.time()-t0:6.1f}s  value {v}")
            if v - start >= args.laps:
                print(f"\nLAP CONFIRMED: {args.addr} went {start} -> {v} "
                      f"in {time.time()-first_ts:.0f}s")
                return 0
    finally:
        p.kill()

    print(f"\nNo lap observed. Values seen: {seen or '(none)'}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
