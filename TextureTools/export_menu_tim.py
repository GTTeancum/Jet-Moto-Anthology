"""Extract standalone menu/track-overview TIMs by original disc filename.

Original-resolution sources only, not upscales. The overview files are loading-
screen investigation inputs; their runtime use still needs native verification.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

from build_native_pack import Disc, records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a fresh source extraction directory')
    disc = Disc(args.cue)
    exported = []
    try:
        boot = disc.read('SCUS_943.09')
        if hashlib.sha256(boot).hexdigest() != 'f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':
            raise ValueError('Unsupported original executable revision')
        names = sorted(name for name in disc.files if name.endswith('.TIM') and
                       (name.startswith(('NAVIGATE/', 'PRIZES/', 'MISC/')) or '/OVERV' in name))
        for name in names:
            original = disc.read(name)
            # Reuse the strict TIM parser inside the existing native bank reader.
            wrapper = struct.pack('<6I', 0x50535854, 0x43, 0, 1, 0, len(original)) + original
            meta, image = next(records(wrapper))
            target = args.output / Path(name).with_suffix('.png')
            target.parent.mkdir(parents=True, exist_ok=True)
            image.save(target)
            record = {'source': name, 'sourceSha256': hashlib.sha256(original).hexdigest(),
                      'width': image.width, 'height': image.height, 'depth': meta['depth'],
                      'placement': meta['source_placement'], 'clut': meta['source_clut'],
                      'pngSha256': hashlib.sha256(target.read_bytes()).hexdigest(),
                      'status': 'original standalone TIM extraction; not an upscale'}
            Path(str(target) + '.json').write_text(json.dumps(record, indent=2) + '\n', encoding='ascii')
            exported.append(record)
            print(f'{name}: {image.width}x{image.height}')
    finally:
        disc.close()
    (args.output / 'manifest.json').write_text(json.dumps(exported, indent=2) + '\n', encoding='ascii')
    print(f'Extracted {len(exported)} original standalone sources')


if __name__ == '__main__':
    main()
