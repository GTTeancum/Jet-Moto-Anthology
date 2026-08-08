#!/usr/bin/env python3
"""
Search for a scripted-input timing that reliably enters a full race.

The attract demo makes fixed timings unreliable: it starts on its own a few
seconds after the title appears, and a press during a demo only returns to the
title. So a script can bounce between title and demo forever, which is exactly
what the hand-tuned ones did.

Rather than guess, try a grid of (start, spacing, count) and score each run by
whether it reaches a *sustained* race: track data loaded, and no TITLE.BS
(lba 30018) reload for the rest of the run. The winner is written to
harness/race-run.txt.

    python harness/find-menu-route.py --run-seconds 100
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
TMP = ROOT / "harness" / "_probe.txt"
OUT = ROOT / "harness" / "race-run.txt"

TITLE_LBA = 30018
# directory sectors for the tracks the game picks from
TRACK_LBAS = {11749, 4460, 503, 1733, 2875, 13014, 14341, 30364, 31606, 32900}

RE_READ = re.compile(r"CdRead sectors=\d+ buf=0x[0-9A-F]+ mode=0x[0-9A-F]+ lba=(-?\d+)")


def write_script(path, start, spacing, count):
    steps = []
    t = start
    for _ in range(count):
        steps.append(f"{t}:cross")
        steps.append(f"{t + 10}:")
        t += spacing
    steps.append(f"{t + 60}:")     # hands off: idle so the AI races
    path.write_text("\n".join(steps) + "\n", encoding="utf-8")
    return t


def run(script, seconds):
    env = {**os.environ, "RECOMPONE_HEADLESS": "1", "RECOMPONE_LOG": "sdk",
           "RECOMPONE_INPUT": f"@{script}"}
    try:
        p = subprocess.run(["dotnet", str(PORT), str(CUE)], cwd=ROOT, env=env,
                           timeout=seconds, capture_output=True, text=True,
                           encoding="utf-8", errors="replace")
        out = (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired as e:
        out = ((e.stdout or "") + (e.stderr or ""))
        if isinstance(out, bytes):
            out = out.decode("utf-8", "replace")
    return out


def score(log):
    """Reward reaching track data and then staying out of the title."""
    seq = [int(m.group(1)) for m in RE_READ.finditer(log)]
    if not seq:
        return 0, "no CD reads"
    last_title = max((i for i, l in enumerate(seq) if l == TITLE_LBA), default=-1)
    track_hits = [i for i, l in enumerate(seq) if l in TRACK_LBAS]
    if not track_hits:
        return 0, "never loaded track data"
    last_track = max(track_hits)
    if last_title > last_track:
        return 1, "returned to the title after the track"
    # tail length after the last track load, in CD-read events
    return 2 + (len(seq) - last_track), "sustained race"


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--run-seconds", type=int, default=100)
    ap.add_argument("--counts", default="4,6,8")
    ap.add_argument("--starts", default="1200,1800,2400")
    ap.add_argument("--spacings", default="120,180,300")
    args = ap.parse_args()

    best = (0, None, "")
    for start in [int(x) for x in args.starts.split(",")]:
        for spacing in [int(x) for x in args.spacings.split(",")]:
            for count in [int(x) for x in args.counts.split(",")]:
                write_script(TMP, start, spacing, count)
                t0 = time.time()
                log = run(TMP, args.run_seconds)
                s, why = score(log)
                print(f"  start={start:5} spacing={spacing:4} count={count}  "
                      f"score={s:<5} {why}  ({time.time()-t0:.0f}s)")
                if s > best[0]:
                    best = (s, (start, spacing, count), why)

    if best[1] is None:
        print("\nNothing reached a sustained race.")
        return 1
    start, spacing, count = best[1]
    write_script(OUT, start, spacing, count)
    print(f"\nBEST: start={start} spacing={spacing} count={count} "
          f"(score {best[0]}, {best[2]}) -> {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
