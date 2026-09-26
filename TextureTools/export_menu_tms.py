"""Extract original menu TMS records with provenance for offline review."""
import argparse
import hashlib
import json
from pathlib import Path

from build_native_pack import Disc, records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a new extraction directory')
    disc = Disc(args.cue)
    try:
        bank = disc.read(args.source)
    finally:
        disc.close()
    decoded = list(records(bank))
    if not decoded:
        raise ValueError('No original texture records')
    args.output.mkdir(parents=True)
    for metadata, image in decoded:
        path = args.output / f"{metadata['ordinal']:04d}-{metadata['texture_id']}.png"
        image.save(path)
        provenance = {
            'source': args.source,
            'sourceSha256': hashlib.sha256(bank).hexdigest(),
            'record': metadata,
            'width': image.width,
            'height': image.height,
            'pngSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
        }
        Path(str(path) + '.json').write_text(
            json.dumps(provenance, indent=2) + '\n', encoding='ascii')
        print(path)


if __name__ == '__main__':
    main()
