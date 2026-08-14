#!/usr/bin/env bash
# Build a Jet Moto port from your own disc.
#
# Nothing derived from the game ships with this repository, so the recompiler
# has to run against your copy here. That is what this does: fetch RecompOne,
# apply the fork, translate the executable off your disc into C#, build it.
#
#   ./build.sh --game jm1 --cue "/path/to/Jet Moto (USA).cue"
#   ./build.sh --game jm2 --cue "/path/to/Jet Moto 2 (v1.1).cue"
#   ./build.sh --game jm3 --cue "/path/to/Jet Moto 3 (USA).cue"
#
# Add --loose to also extract the disc to a loose-file tree with an ogg
# soundtrack, which the port will then prefer.
set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME=""
CUE=""
LOOSE=0

while [ $# -gt 0 ]; do
    case "$1" in
        --game) GAME="$2"; shift 2 ;;
        --cue)  CUE="$2";  shift 2 ;;
        --loose) LOOSE=1;  shift ;;
        -h|--help) sed -n '2,14p' "$0" | sed 's/^# \?//'; exit 0 ;;
        *) echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

case "$GAME" in
    jm1) PROJ="JetMoto";  CONFIG="JetMoto/config/jetmoto.json";   LOOSEDIR="JetMoto_loose" ;;
    jm2) PROJ="JetMoto2"; CONFIG="JetMoto2/config/jetmoto2.json"; LOOSEDIR="JetMoto2_loose" ;;
    jm3) PROJ="JetMoto3"; CONFIG="JetMoto3/config/jetmoto3.json"; LOOSEDIR="JetMoto3_loose" ;;
    *) echo "usage: $0 --game jm1|jm2|jm3 --cue <path to .cue> [--loose]" >&2; exit 2 ;;
esac

if [ -z "$CUE" ] || [ ! -f "$CUE" ]; then
    echo "disc not found: ${CUE:-<none given>}" >&2
    echo "You need a bin/cue rip of your own disc; none is included here." >&2
    exit 1
fi

command -v dotnet >/dev/null || { echo "dotnet SDK not found on PATH" >&2; exit 1; }

echo "==> RecompOne"
"$REPO/tools/apply-fork.sh"

echo "==> building the recompiler"
dotnet build "$REPO/tools/RecompOne" -c Release -v q --nologo

echo "==> recompiling your disc's executable"
# The config points at the disc; override it for this run without editing it.
python - "$REPO/$CONFIG" "$CUE" <<'PY'
import json, re, sys, pathlib
cfg, cue = pathlib.Path(sys.argv[1]), sys.argv[2]
text = cfg.read_text(encoding="utf-8")
# the config carries // comments, so patch the cue line textually
patched = re.sub(r'("cue"\s*:\s*)"(?:[^"\\]|\\.)*"',
                 lambda m: m.group(1) + json.dumps(cue), text, count=1)
cfg.with_suffix(".build.json").write_text(patched, encoding="utf-8")
PY
BUILDCFG="${CONFIG%.json}.build.json"
dotnet run --project "$REPO/tools/RecompOne/RecompOne.Recompiler" -c Release --no-build \
    -- "$REPO/$BUILDCFG"
rm -f "$REPO/$BUILDCFG"

echo "==> building the port"
dotnet build "$REPO/$PROJ/$PROJ.csproj" -c Release -v q --nologo

if [ "$LOOSE" = "1" ]; then
    echo "==> extracting loose files + ogg soundtrack"
    python "$REPO/tools/extract-disc.py" --cue "$CUE" --out "$REPO/$LOOSEDIR" --force
fi

echo
echo "done. run it with:"
echo "  dotnet $PROJ/bin/Release/net10.0/$PROJ.dll \"$CUE\""
