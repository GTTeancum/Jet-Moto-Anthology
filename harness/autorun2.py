#!/usr/bin/env python3
"""
Discover Jet Moto 2's missing function entry points by running the port.

Same idea as autorun.py, extended for an overlay-based game. RecompOne's
function detector merges some code into its neighbour, so entry points that are
only ever reached through a pointer -- the game builds its dispatch tables in
RAM -- cannot be found statically. Running until each one faults is what finds
them, and every fault names the address.

The address decides where the entry goes: inside the shell overlay's span it is
appended to that overlay's `functions`, otherwise to the top-level list.

    python harness/autorun2.py --timeout 60 --max-iters 200
"""
import argparse
import json
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONFIG = ROOT / "JetMoto2" / "config" / "jetmoto2.json"
RECOMPILER = ROOT / "tools" / "RecompOne" / "RecompOne.Recompiler"
PORT_DLL = ROOT / "JetMoto2" / "bin" / "Release" / "net10.0" / "JetMoto2.dll"
CUE = ROOT / "JetMoto2_PS1image" / "Jet Moto 2 (v1.1).cue"

RE_UNMAPPED = re.compile(r"unmapped (?:call|address|jump)[^0-9a-fx]*(?:0x)?([0-9A-Fa-f]{8})")


def sh(cmd, timeout, env=None):
    import os
    e = dict(os.environ)
    if env:
        e.update(env)
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout,
                           env=e, cwd=str(ROOT), errors="replace")
        return p.returncode, (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired as ex:
        out = (ex.stdout or "") + (ex.stderr or "")
        if isinstance(out, bytes):
            out = out.decode("utf-8", "replace")
        return "timeout", out


def load_cfg():
    # the config carries // comments, which json refuses
    text = CONFIG.read_text(encoding="utf-8")
    stripped = "\n".join(l for l in text.splitlines()
                         if not l.lstrip().startswith("//"))
    return json.loads(stripped), text


def add_entry(addr):
    """Insert `addr` into the right functions[] list, preserving comments."""
    cfg, text = load_cfg()
    a = int(addr, 16)
    ov = cfg.get("overlays", [])
    # Every /BIN track overlay is loaded at the same address, so an entry point
    # in that span exists in each of them. Adding it to only the first one left
    # the fault unfixed for whichever track was actually loaded.
    hits = [o["name"] for o in ov
            if int(o["base"], 16) <= a < int(o["base"], 16) + o.get("size", 0)]
    target = "overlay" if hits else "main"
    anchor = ",".join(hits) if hits else None

    entry = f'{{ "address": "0x{a:08X}" }},'
    token = f'"0x{a:08X}"'
    added = []

    if target == "overlay":
        for name in hits:
            # Confine the edit to this overlay object's own braces. Matching
            # `"functions": [` with a lazy wildcard from the overlay's name ran
            # straight past the end of the block and appended into the
            # top-level list instead, silently sending overlay addresses to the
            # main executable.
            lo = text.index(f'"name": "{name}"')
            depth, hi = 0, None
            for i in range(text.rindex("{", 0, lo), len(text)):
                if text[i] == "{":
                    depth += 1
                elif text[i] == "}":
                    depth -= 1
                    if depth == 0:
                        hi = i
                        break
            block = text[lo:hi]
            # Idempotent per block: the config can already carry the address
            # for some overlays and not others, and emitting it twice in one
            # module makes the generated C# collide.
            if token in block:
                continue
            added.append(name)
            if '"functions"' in block:
                j = lo + block.index('"functions"')
                j = text.index("[", j) + 1
                text = text[:j] + "\n        " + entry + text[j:]
            else:
                text = (text[:hi].rstrip().rstrip(",")
                        + ',\n      "functions": [\n        ' + entry.rstrip(",")
                        + "\n      ]\n    " + text[hi:])
    else:
        # The top-level array is the one at two-space indentation. Searching
        # for the first `"functions": [` after `"overlays"` found the overlay's
        # own array as soon as one existed, so main-exe addresses ended up
        # inside the overlay.
        j = text.index('\n  "functions": [') + 1
        j = text.index("[", j) + 1
        if token in text[j:text.index("\n  ]", j)]:
            return "already-present", None
        added.append("main")
        text = text[:j] + "\n    " + entry + text[j:]

    if not added:
        return "already-present", anchor
    CONFIG.write_text(text, encoding="utf-8")
    return target, ",".join(added)


def recompile():
    rc, out = sh(["dotnet", "run", "--project", str(RECOMPILER), "-c", "Release",
                  "--no-build", "--", str(CONFIG)], timeout=2400)
    return "Recompilation finished" in out, out


def build():
    rc, out = sh(["dotnet", "build", str(ROOT / "JetMoto2" / "JetMoto2.csproj"),
                  "-c", "Release", "-v", "q", "--nologo"], timeout=2400)
    return "Build succeeded" in out or rc == 0, out


def run_port(timeout):
    rc, out = sh(["dotnet", str(PORT_DLL), str(CUE)], timeout=timeout,
                 env={"RECOMPONE_HEADLESS": "1", "RECOMPONE_LOG_FILE": "off"})
    return out, rc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--timeout", type=int, default=60)
    ap.add_argument("--max-iters", type=int, default=200)
    args = ap.parse_args()

    seen = set()
    for it in range(1, args.max_iters + 1):
        log, rc = run_port(args.timeout)
        m = RE_UNMAPPED.search(log)
        if not m:
            if rc == "timeout":
                print(f"[{it}] survived {args.timeout}s with no unmapped call")
                return 0
            bad = next((l for l in log.splitlines() if "xception" in l), "")
            print(f"[{it}] exit {rc}: {bad.strip()[:160]}")
            return 1

        addr = m.group(1).upper()
        if addr in seen:
            print(f"[{it}] 0x{addr} reported again after being added -- stopping")
            return 1
        seen.add(addr)
        where, name = add_entry(addr)
        print(f"[{it}] 0x{addr} -> {where}{'/' + name if name else ''}", flush=True)

        ok, out = recompile()
        if not ok:
            print("recompile failed:\n" + out[-2000:])
            return 1
        ok, out = build()
        if not ok:
            print("build failed:\n" + out[-2000:])
            return 1

    print(f"hit max-iters ({args.max_iters}); {len(seen)} entries added")
    return 1


if __name__ == "__main__":
    sys.exit(main())
