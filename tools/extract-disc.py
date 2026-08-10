#!/usr/bin/env python3
"""
Extract a PS1 disc image into a loose-file tree the port can boot from.

The port prefers loose files when they are present. That makes the disc
contents browsable and moddable, and it turns the redbook audio tracks into an
ordinary .ogg soundtrack instead of hundreds of megabytes of raw PCM.

Layout produced:

    <out>/disc.json          manifest: tracks, files, sector map
    <out>/files/...          the ISO9660 tree, ";1" version suffixes stripped
    <out>/cdaudio/*.ogg      one per CD-DA track
    <out>/structure.bin      the data-track sectors that belong to no file
                             (volume descriptors, path tables, directories)

Original LBAs are preserved. The runtime rebuilds a byte-faithful view of the
data track from these pieces, so anything that addresses the disc by sector --
the game's own ISO reader, overlay loading, the recompiler's config -- keeps
working exactly as it did against the image.

    python tools/extract-disc.py --cue "JetMoto2_PS1image/Jet Moto 2 (v1.1).cue" \
                                 --out JetMoto2/disc
"""
import argparse
import json
import os
import re
import shutil
import struct
import subprocess
import sys
from pathlib import Path

RAW = 2352
USER = 2048


def parse_cue(cue_path):
    """
    Return [{number, mode, bin, idx, startLba}] in cue order.

    Absolute LBAs are derived the same way CueBin does it: sectors of all
    preceding FILEs plus the track's own INDEX 01 offset within its file. A
    per-track-file dump carries no absolute positions of its own, so the two
    have to agree or the table of contents the game reads will not match the
    audio it gets.
    """
    cue = Path(cue_path)
    text = cue.read_text(encoding="utf-8", errors="replace")
    tracks, cur_file, file_base = [], None, 0
    for line in text.splitlines():
        s = line.strip()
        m = re.match(r'FILE\s+"(.+)"\s+BINARY', s, re.I)
        if m:
            if cur_file is not None and cur_file.exists():
                file_base += cur_file.stat().st_size // RAW
            cur_file = cue.parent / m.group(1)
            continue
        m = re.match(r"TRACK\s+(\d+)\s+(\S+)", s, re.I)
        if m:
            tracks.append({"number": int(m.group(1)), "mode": m.group(2).upper(),
                           "bin": cur_file, "idx": {}, "base": file_base})
            continue
        m = re.match(r"INDEX\s+(\d+)\s+(\d+):(\d+):(\d+)", s, re.I)
        if m and tracks:
            mm, ss, ff = int(m.group(2)), int(m.group(3)), int(m.group(4))
            tracks[-1]["idx"][int(m.group(1))] = (mm * 60 + ss) * 75 + ff
    for t in tracks:
        t["startLba"] = t["base"] + t["idx"].get(1, 0)
    return tracks


def sector_user(raw):
    """User data of a MODE2 sector: form 2 carries 2324 bytes, form 1 carries 2048."""
    if raw[15] == 2:
        return raw[24:24 + USER]
    return raw[16:16 + USER]


def is_form2(raw):
    return raw[15] == 2 and (raw[18] & 0x20) != 0


def walk_iso(f, data_offset, root_lba, root_size):
    """Yield (isoPath, lba, size, isDir) for the whole tree."""
    def read_sector(lba):
        f.seek(data_offset + lba * RAW)
        return f.read(RAW)

    out = []

    def walk(lba, size, path):
        blob = b"".join(sector_user(read_sector(lba + i))
                        for i in range((size + USER - 1) // USER))
        off = 0
        while off < len(blob):
            length = blob[off]
            if length == 0:
                off = (off // USER + 1) * USER
                if off >= len(blob):
                    break
                continue
            ex = struct.unpack_from("<I", blob, off + 2)[0]
            sz = struct.unpack_from("<I", blob, off + 10)[0]
            flags = blob[off + 25]
            nlen = blob[off + 32]
            name = blob[off + 33:off + 33 + nlen]
            if name not in (b"\x00", b"\x01"):
                child = path + "/" + name.decode("latin1")
                if flags & 2:
                    out.append((child, ex, sz, True))
                    walk(ex, sz, child)
                else:
                    out.append((child, ex, sz, False))
            off += length

    walk(root_lba, root_size, "")
    return out


def clean_name(iso_name):
    """`FOO.BIN;1` -> `FOO.BIN`."""
    return iso_name.split(";")[0]



def verify(cue_path, out):
    """
    Prove the loose tree reproduces the data track byte for byte.

    Everything above the disc layer addresses sectors, not files, so "the files
    are all there" is not the property that matters -- the reconstruction has to
    return exactly what the image would. This rebuilds each sector the way the
    runtime does and compares the 2048-byte user area against the image.
    """
    manifest = json.loads((out / "disc.json").read_text(encoding="utf-8"))
    tracks = parse_cue(cue_path)
    dt = next(t for t in tracks if t["mode"].startswith("MODE"))
    total = manifest["dataTrack"]["sectors"]

    extents = sorted((f for f in manifest["files"] if not f.get("audio")),
                     key=lambda f: f["lba"])
    starts = [f["lba"] for f in extents]

    ranges, off = [], 0
    for lba, count in manifest["structure"]["ranges"]:
        ranges.append((lba, count, off))
        off += count * RAW

    import bisect
    struct_blob = open(out / "structure.bin", "rb")
    cache = {}

    def loose_user(lba):
        for start, count, offset in ranges:
            if start <= lba < start + count:
                struct_blob.seek(offset + (lba - start) * RAW)
                return sector_user(struct_blob.read(RAW))
        i = bisect.bisect_right(starts, lba) - 1
        if i >= 0:
            e = extents[i]
            nsec = max(1, (e["size"] + USER - 1) // USER)
            if e["lba"] <= lba < e["lba"] + nsec:
                fh = cache.get(e["path"])
                if fh is None:
                    fh = cache[e["path"]] = open(out / "files" / e["path"], "rb")
                    if len(cache) > 64:
                        k, v = next(iter(cache.items()))
                        if k != e["path"]:
                            v.close(); del cache[k]
                if e.get("raw"):
                    fh.seek((lba - e["lba"]) * RAW)
                    return sector_user(fh.read(RAW).ljust(RAW, bytes(1)))
                fh.seek((lba - e["lba"]) * USER)
                return fh.read(USER).ljust(USER, bytes(1))
        return bytes(USER)

    bad = 0
    with open(dt["bin"], "rb") as img:
        for lba in range(total):
            img.seek(lba * RAW)
            want = sector_user(img.read(RAW))
            if loose_user(lba) != want:
                bad += 1
                if bad <= 5:
                    print(f"  mismatch at lba {lba}")
    for fh in cache.values():
        fh.close()
    struct_blob.close()
    print(f"verify: {total - bad}/{total} sectors identical"
          + ("" if bad == 0 else f"  ({bad} MISMATCHED)"))
    return 1 if bad else 0


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cue", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--quality", default="6", help="ffmpeg vorbis quality (default 6)")
    ap.add_argument("--force", action="store_true", help="overwrite an existing extraction")
    ap.add_argument("--verify", action="store_true",
                    help="compare an existing extraction against the image, sector by sector")
    args = ap.parse_args()

    out = Path(args.out)
    if args.verify:
        return verify(args.cue, out)
    if out.exists() and any(out.iterdir()) and not args.force:
        print(f"{out} is not empty; pass --force to overwrite", file=sys.stderr)
        return 1
    # --force has to clear, not just overwrite. A previous run's output is not a
    # subset of this one's -- an entry can stop being extracted between runs (the
    # .DA files became CD-DA references) and would otherwise survive as a stale
    # zero-byte file in a tree that is meant to be browsable.
    for stale in ("files", "cdaudio"):
        shutil.rmtree(out / stale, ignore_errors=True)
    (out / "files").mkdir(parents=True, exist_ok=True)
    (out / "cdaudio").mkdir(parents=True, exist_ok=True)

    tracks = parse_cue(args.cue)
    data_tracks = [t for t in tracks if t["mode"].startswith("MODE")]
    if not data_tracks:
        print("no data track in the cue", file=sys.stderr)
        return 1
    dt = data_tracks[0]

    manifest = {"version": 1, "source": Path(args.cue).name,
                "tracks": [], "files": [], "structure": {}}

    with open(dt["bin"], "rb") as f:
        total_sectors = os.path.getsize(dt["bin"]) // RAW
        f.seek(16 * RAW)
        pvd = sector_user(f.read(RAW))
        root = pvd[156:156 + 34]
        root_lba = struct.unpack_from("<I", root, 2)[0]
        root_size = struct.unpack_from("<I", root, 10)[0]

        entries = walk_iso(f, 0, root_lba, root_size)
        files = [e for e in entries if not e[3]]
        audio_refs = sum(1 for _, lba, _, _ in files if lba >= total_sectors)
        print(f"data track: {total_sectors} sectors, {len(files)} files "
              f"({audio_refs} of them CD-DA references)")

        covered = bytearray(total_sectors)
        for _, lba, size, _ in files:
            n = max(1, (size + USER - 1) // USER)
            for i in range(lba, min(lba + n, total_sectors)):
                covered[i] = 1

        # extract each file
        for iso_path, lba, size, _ in sorted(files, key=lambda e: e[1]):
            rel = "/".join(clean_name(p) for p in iso_path.strip("/").split("/"))

            # Some entries point past the end of the data track: they name the
            # CD-DA tracks so the game can find them by filename (Jet Moto 2's
            # .DA files each start exactly at an audio track). There are no data
            # sectors to extract -- the audio itself becomes an ogg below.
            if lba >= total_sectors:
                manifest["files"].append({
                    "path": rel, "iso": iso_path.replace("/", "\\"),
                    "lba": lba, "size": size, "audio": True})
                continue

            dest = out / "files" / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            nsec = max(1, (size + USER - 1) // USER)

            f.seek(lba * RAW)
            chunk = f.read(nsec * RAW)
            raw_mode = any(is_form2(chunk[i * RAW:(i + 1) * RAW])
                           for i in range(min(nsec, len(chunk) // RAW)))
            with open(dest, "wb") as w:
                if raw_mode:
                    # Form 2 sectors carry 2324 bytes of XA payload that no
                    # 2048-byte view can represent, so those files are kept as
                    # whole 2352-byte sectors and served back verbatim.
                    w.write(chunk)
                else:
                    written = 0
                    for i in range(nsec):
                        sec = chunk[i * RAW:(i + 1) * RAW]
                        if len(sec) < RAW:
                            break
                        take = min(USER, size - written)
                        w.write(sector_user(sec)[:take])
                        written += take
            manifest["files"].append({
                "path": rel, "iso": iso_path.replace("/", "\\"),
                "lba": lba, "size": size, "raw": raw_mode})

        # every data-track sector that belongs to no file, unless it is blank
        ranges, blob = [], []
        run_start, run = None, []
        f.seek(0)
        for lba in range(total_sectors):
            if covered[lba]:
                if run_start is not None:
                    ranges.append([run_start, len(run)]); blob.extend(run)
                    run_start, run = None, []
                continue
            f.seek(lba * RAW)
            sec = f.read(RAW)
            if len(sec) < RAW or not any(sector_user(sec)):
                if run_start is not None:
                    ranges.append([run_start, len(run)]); blob.extend(run)
                    run_start, run = None, []
                continue
            if run_start is None:
                run_start = lba
            run.append(sec)
        if run_start is not None:
            ranges.append([run_start, len(run)]); blob.extend(run)

        with open(out / "structure.bin", "wb") as w:
            for sec in blob:
                w.write(sec)
        manifest["structure"] = {"blob": "structure.bin", "ranges": ranges}
        print(f"structure: {len(blob)} sectors in {len(ranges)} ranges "
              f"({len(blob) * RAW / 1e6:.1f} MB)")

    leadout = 0
    seen = set()
    for t in tracks:
        if t["bin"] not in seen and t["bin"].exists():
            seen.add(t["bin"])
            leadout += t["bin"].stat().st_size // RAW
    manifest["dataTrack"] = {"sectors": total_sectors}
    manifest["leadoutLba"] = leadout
    manifest["tracks"].append({"number": dt["number"], "type": "data",
                               "startLba": dt["startLba"]})

    # audio tracks -> ogg
    for t in tracks:
        if t["mode"] != "AUDIO":
            continue
        # skip the pregap that sits inside the track file, so the ogg starts
        # where INDEX 01 does rather than with two seconds of silence
        src = t["bin"]
        pcm_off = (t["idx"].get(1, 0) - t["idx"].get(0, 0)) * RAW
        nbytes = os.path.getsize(src) - pcm_off
        name = f"track{t['number']:02d}.ogg"
        dest = out / "cdaudio" / name
        cmd = ["ffmpeg", "-hide_banner", "-loglevel", "error", "-y",
               "-f", "s16le", "-ar", "44100", "-ac", "2",
               "-ss", "0", "-i", "pipe:0",
               "-c:a", "libvorbis", "-q:a", args.quality, str(dest)]
        with open(src, "rb") as r:
            r.seek(pcm_off)
            p = subprocess.Popen(cmd, stdin=subprocess.PIPE)
            shutil.copyfileobj(r, p.stdin, length=1 << 20)
            p.stdin.close()
            if p.wait() != 0:
                print(f"ffmpeg failed on track {t['number']}", file=sys.stderr)
                return 1
        secs = nbytes / RAW / 75.0
        manifest["tracks"].append({"number": t["number"], "type": "audio",
                                   "startLba": t["startLba"],
                                   "ogg": f"cdaudio/{name}",
                                   "sectors": nbytes // RAW})
        print(f"track {t['number']:02d}: {secs:6.1f}s -> {name} "
              f"({dest.stat().st_size / 1e6:.1f} MB)")

    (out / "disc.json").write_text(json.dumps(manifest, indent=1), encoding="utf-8")
    print(f"\nwrote {out / 'disc.json'}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
