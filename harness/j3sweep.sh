#!/usr/bin/env bash
# Sweep the two knobs that govern Jet Moto 3's movie playback and report, for
# each pairing, whether anything renders and how far the boot sequence gets.
cd "$(dirname "$0")/.."
for unpaced in 0 1; do
  for field in 1 2 4 8 16; do
    out="harness/sw"; rm -rf "$out"; mkdir -p "$out"
    log=$(RECOMPONE_UNPACED=$unpaced RECOMPONE_STREAM_FIELD=$field \
          RECOMPONE_OFFSCREEN=1 RECOMPONE_DUMP_DIR="$out" RECOMPONE_DUMP_EVERY=40 \
          RECOMPONE_LOG=sdk RECOMPONE_LOG_FILE=off \
          timeout 70 dotnet JetMoto3/bin/Release/net10.0/JetMoto3.dll "Jet Moto 3 (USA).cue" 2>&1 \
          | grep -viE "read outside|VSync")
    movies=$(echo "$log" | grep -oE "STR;1'" | wc -l)
    bright=$(python3 - "$out" <<'PY'
import sys, glob
best=0
for p in sorted(glob.glob(sys.argv[1]+"/*.ppm")):
    f=open(p,'rb'); f.readline(); w,h=map(int,f.readline().split()); f.readline(); d=f.read(w*h*3)
    n=0; tot=0
    for y in range(40,h-40,20):
        for x in range(200,w-200,20):
            o=(y*w+x)*3
            if d[o]+d[o+1]+d[o+2]>60: n+=1
            tot+=1
    best=max(best, n*100//max(tot,1))
print(best)
PY
)
    echo "unpaced=$unpaced field=x$field  movies_opened=$movies  brightest=${bright}%"
  done
done
