#!/usr/bin/env python3
"""
Headless run-diagnose-fix loop for the Jet Moto port.

Runs the port with no window, parses the fault out of the log, applies the fix
when the fault is mechanically fixable, recompiles, and goes again. Stops when
it reaches a fault that needs a human-grade decision, or when the game survives
the whole timeout (which is the good ending).

    python harness/autorun.py                 # loop until stuck
    python harness/autorun.py --once          # single run, no fixing
    python harness/autorun.py --timeout 120   # longer soak

Auto-fixable:
  unmapped call: 0xADDR     -> add an explicit functions[] entry at ADDR
Reported, not auto-fixed:
  unmapped address: 0xADDR  -> bad pointer; needs diagnosis, not a config edit
"""

import argparse
import json
import os
import re
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CONFIG = ROOT / "JetMoto" / "config" / "jetmoto.json"
CUE = ROOT / "JetMotoPS1image" / "Jet Moto (USA).cue"
PORT_DLL = ROOT / "JetMoto" / "bin" / "Release" / "net10.0" / "JetMoto.dll"
RECOMPILER = ROOT / "tools" / "RecompOne" / "RecompOne.Recompiler"
LOGDIR = ROOT / "harness" / "traces"

RE_UNMAPPED_CALL = re.compile(r"unmapped call:\s*0x([0-9A-Fa-f]{8})")
RE_UNMAPPED_ADDR = re.compile(r"unmapped address:\s*0x([0-9A-Fa-f]{8})")
RE_FRAME = re.compile(r"at Recompiled\.JetMoto\.(func_[0-9A-Fa-f]{8})")


def sh(cmd, timeout=None, env=None, cwd=ROOT):
    """Run a command, capture everything, never raise on non-zero."""
    merged = {**os.environ, **(env or {})}
    try:
        p = subprocess.run(cmd, cwd=cwd, env=merged, timeout=timeout,
                           capture_output=True, text=True,
                           encoding="utf-8", errors="replace")
        return p.returncode, (p.stdout or "") + (p.stderr or "")
    except subprocess.TimeoutExpired as e:
        out = (e.stdout or "") + (e.stderr or "")
        if isinstance(out, bytes):
            out = out.decode("utf-8", "replace")
        return "timeout", out


def strip_jsonc(text):
    """RecompOne allows // comments; json.loads does not."""
    return re.sub(r"^\s*//.*$", "", text, flags=re.MULTILINE)


def load_config():
    return json.loads(strip_jsonc(CONFIG.read_text(encoding="utf-8")))


def save_functions(entries):
    """
    Splice the functions[] array into the config as text.

    A json.dumps round-trip would strip the // comments that document why the
    config looks the way it does, and those comments are the only record of a
    lot of reasoning. So the array is rewritten in place and everything else in
    the file is left byte-for-byte alone.
    """
    text = CONFIG.read_text(encoding="utf-8")
    body = ",\n".join(
        f'    {{ "address": "{e["address"]}", "name": "{e["name"]}" }}'
        for e in entries)
    block = (
        '  // Auto-maintained by harness/autorun.py: call targets the sweep did\n'
        '  // not emit, discovered by running until it faults.\n'
        '  "functions": [\n' + body + '\n  ],\n\n'
    )
    existing = re.search(
        r'[ \t]*//[^\n]*\n(?:[ \t]*//[^\n]*\n)*[ \t]*"functions"\s*:\s*\[.*?\][ \t]*,?\s*\n\s*\n?'
        r'|[ \t]*"functions"\s*:\s*\[.*?\][ \t]*,?\s*\n\s*\n?',
        text, re.DOTALL)
    if existing:
        text = text[:existing.start()] + block + text[existing.end():]
    else:
        anchor = re.search(r'[ \t]*"patches"\s*:', text)
        if not anchor:
            raise RuntimeError("cannot find an anchor to insert functions[] before")
        text = text[:anchor.start()] + block + text[anchor.start():]
    CONFIG.write_text(text, encoding="utf-8")


def recompile():
    rc, out = sh(["dotnet", "run", "--project", str(RECOMPILER),
                  "-c", "Release", "--no-build", "--", str(CONFIG)], timeout=1800)
    ok = "Recompilation finished" in out
    total = re.search(r"total functions:\s*(\d+)", out)
    return ok, (total.group(1) if total else "?"), out


def build():
    rc, out = sh(["dotnet", "build", str(ROOT / "JetMoto" / "JetMoto.csproj"),
                  "-c", "Release", "-v", "q", "--nologo"], timeout=1800)
    return "Build succeeded" in out or rc == 0, out


def run_port(timeout):
    """One headless run. Returns (log_text, verdict)."""
    env = {
        "RECOMPONE_HEADLESS": "1",
        "DOTNET_gcServer": "1",
    }
    rc, out = sh(["dotnet", str(PORT_DLL), str(CUE)], timeout=timeout, env=env)
    return out, ("survived" if rc == "timeout" else f"exit {rc}")


def diagnose(log):
    """Classify the failure. Returns (kind, detail, callstack)."""
    stack = RE_FRAME.findall(log)
    m = RE_UNMAPPED_CALL.search(log)
    if m:
        return "unmapped_call", "0x" + m.group(1).upper(), stack
    m = RE_UNMAPPED_ADDR.search(log)
    if m:
        return "unmapped_address", "0x" + m.group(1).upper(), stack
    if "Unhandled exception" in log:
        first = next((l for l in log.splitlines() if "Unhandled exception" in l), "")
        return "exception", first.strip(), stack
    return "clean", "", stack


def add_function(cfg, addr_hex):
    """Add an explicit functions[] entry so the recompiler emits code there."""
    funcs = cfg.setdefault("functions", [])
    if any(f.get("address", "").lower() == addr_hex.lower() for f in funcs):
        return False
    funcs.append({"address": addr_hex, "name": f"func_{addr_hex[2:]}"})
    return True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--timeout", type=int, default=60,
                    help="seconds to let the port run before calling it a survival")
    ap.add_argument("--max-iters", type=int, default=40)
    ap.add_argument("--once", action="store_true", help="run once, do not fix")
    ap.add_argument("--no-rebuild", action="store_true",
                    help="skip recompile/build on the first iteration")
    args = ap.parse_args()

    LOGDIR.mkdir(parents=True, exist_ok=True)
    fixed = []

    for i in range(1, args.max_iters + 1):
        if not (args.no_rebuild and i == 1):
            ok, count, out = recompile()
            if not ok:
                print(f"[{i}] RECOMPILE FAILED\n{out[-3000:]}")
                return 2
            ok, out = build()
            if not ok:
                errs = [l for l in out.splitlines() if ": error " in l][:15]
                print(f"[{i}] BUILD FAILED\n" + "\n".join(errs))
                return 2
            print(f"[{i}] recompiled ({count} functions), built")

        log, verdict = run_port(args.timeout)
        stamp = time.strftime("%Y%m%d-%H%M%S")
        (LOGDIR / f"run-{stamp}.log").write_text(log, encoding="utf-8")

        kind, detail, stack = diagnose(log)
        depth = len(stack)
        printed = [l for l in log.splitlines()
                   if l and not l.startswith(("   at ", "[Host]"))][:12]

        print(f"[{i}] {verdict} | fault={kind} {detail} | stack depth {depth}")
        for l in printed:
            print(f"      | {l}")

        if kind == "clean" and verdict == "survived":
            print(f"\nSURVIVED {args.timeout}s with no fault. "
                  f"{len(fixed)} function(s) added this session.")
            return 0

        if args.once:
            return 1

        if kind == "unmapped_call":
            cfg = load_config()
            if add_function(cfg, detail):
                save_functions(cfg["functions"])
                fixed.append(detail)
                print(f"      -> added functions[] entry at {detail}")
                continue
            print(f"      -> {detail} already in config; sweep is not emitting it")
            return 1

        print(f"\nSTUCK: {kind} {detail}")
        if stack:
            print("Innermost frames: " + " <- ".join(stack[:6]))
        print(f"Full log: {LOGDIR / f'run-{stamp}.log'}")
        return 1

    print(f"\nHit max iterations ({args.max_iters}).")
    return 1


if __name__ == "__main__":
    sys.exit(main())
