#!/usr/bin/env python3
"""
Drive the game into a real race by reacting to what it loads, then idle.

Fixed-timing scripts cannot do this reliably. The attract demo starts on its
own a few seconds after the title, and a press during a demo only returns to
the title, so a script with hardcoded press times bounces between the two
forever -- which is exactly what happened, and what made an earlier "8 confirms
works" claim luck rather than a route.

Instead: watch the CD reads, which say unambiguously which screen is up, and
press only when a new screen has actually appeared. Once track data loads, stop
pressing entirely so the rider idles and the AI races.

    python harness/drive-race.py --seconds 400 --peek 0x801744B4,0x80174538

Screens are identified by the LBA of the file each one loads.
"""
import argparse
import os
import re
import subprocess
import sys
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CUE = ROOT / "JetMotoPS1image" / "Jet Moto (USA).cue"
PORT = ROOT / "JetMoto" / "bin" / "Release" / "net10.0" / "JetMoto.dll"
LIVE = ROOT / "harness" / "_live-input.txt"

RE_READ = re.compile(r"CdRead sectors=\d+ buf=0x[0-9A-F]+ mode=0x[0-9A-F]+ lba=(-?\d+)")
RE_PEEK = re.compile(r"\[Peek\] 0x([0-9A-F]{8})=0x([0-9A-F]{8})")

TITLE = 30018          # STARTUP/TITLE.BS
PICKRIDE = 16093       # NAVIGATE/PICKRIDE.BS
RACETYPE = 16315       # NAVIGATE/RACETYPE.BS
SCORING = 18613        # NAVIGATE/SCORING.BS
TRACKSEL = range(19012, 19700)   # PICKTRAC/TRACKS*.BS
# directory sectors of the tracks themselves -- reaching one means a race is loading
TRACK_DIRS = {503, 1733, 2875, 4460, 11749, 13014, 14341, 30364, 31606, 32900}

MENU_SCREENS = {TITLE: "title", PICKRIDE: "rider select",
                RACETYPE: "race type", SCORING: "scoring"}


def press(button="cross", hold=0.25):
    LIVE.write_text(button, encoding="utf-8")
    time.sleep(hold)
    LIVE.write_text("", encoding="utf-8")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--seconds", type=int, default=400)
    ap.add_argument("--peek", default="")
    ap.add_argument("--settle", type=float, default=0.4,
                    help="seconds to wait after a screen appears before pressing")
    ap.add_argument("--realtime", action="store_true",
                    help="run at 60 Hz instead of fast-forward, so menu pacing is normal")
    args = ap.parse_args()

    LIVE.write_text("", encoding="utf-8")
    env = {**os.environ, "RECOMPONE_HEADLESS": "1", "RECOMPONE_LOG": "sdk",
           "RECOMPONE_INPUT_LIVE": str(LIVE)}
    if args.realtime:
        env["RECOMPONE_UNTHROTTLE"] = "0"
    if args.peek:
        env["RECOMPONE_PEEK"] = args.peek

    print(f"driving for {args.seconds}s; pressing only when a new screen loads")
    p = subprocess.Popen(["dotnet", str(PORT), str(CUE)], cwd=ROOT, env=env,
                         stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                         text=True, encoding="utf-8", errors="replace", bufsize=1)

    state = {"racing": False, "last_screen": None, "laps": {}, "t0": time.time()}

    def act(screen):
        """Press once, a beat after a menu screen appears."""
        time.sleep(args.settle)
        if state["racing"]:
            return
        press()
        print(f"  t+{time.time()-state['t0']:6.1f}s  pressed X on {screen}")

    try:
        for line in p.stdout:
            if time.time() - state["t0"] > args.seconds:
                break

            mp = RE_PEEK.search(line)
            if mp:
                state["laps"][mp.group(1)] = int(mp.group(2), 16)

            m = RE_READ.search(line)
            if not m:
                continue
            lba = int(m.group(1))

            if lba in TRACK_DIRS and not state["racing"]:
                state["racing"] = True
                print(f"  t+{time.time()-state['t0']:6.1f}s  track data at lba {lba} "
                      f"-- race loading, hands off from here")
                LIVE.write_text("", encoding="utf-8")
                continue

            if state["racing"]:
                if lba == TITLE:
                    print(f"  t+{time.time()-state['t0']:6.1f}s  back at the title "
                          f"-- that was a demo, not a race")
                    state["racing"] = False
                    state["last_screen"] = None   # so the next title triggers a press
                continue

            screen = MENU_SCREENS.get(lba) or ("track select" if lba in TRACKSEL else None)
            if screen and screen != state["last_screen"]:
                state["last_screen"] = screen
                print(f"  t+{time.time()-state['t0']:6.1f}s  screen: {screen}")
                threading.Thread(target=act, args=(screen,), daemon=True).start()
    finally:
        p.kill()

    laps = sorted(state["laps"].values())
    print(f"\nfinished. racing={state['racing']}  lap counters={laps}")
    return 0 if state["racing"] else 1


if __name__ == "__main__":
    sys.exit(main())
