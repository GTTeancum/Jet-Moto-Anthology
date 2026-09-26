"""Verify original loading sources and compare each Loading/Continue pair.

This is source evidence, not an upscale or a visual-acceptance test. The output
retains every differing source sample so future paired authoring cannot silently
assume that only the prompt differs. No runtime catalog is modified.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image

from build_native_pack import Disc, records


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--originals', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a new audit output')
    disc = Disc(args.cue)
    try:
        if sha(disc.read('SCUS_943.09')) != 'f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':
            raise ValueError('Unsupported original executable')
        pairs = []
        sources = sorted(args.originals.glob('*/OVERV[0-9].png'))
        if len(sources) != 10:
            raise ValueError('Expected all ten original overview pairs')
        for source in sources:
            arrays, identities = [], []
            for png in (source, source.with_name(source.stem + 'L.png')):
                meta = json.loads(Path(str(png) + '.json').read_text())
                raw = disc.read(meta['source'])
                if sha(raw) != meta['sourceSha256'] or sha(png.read_bytes()) != meta['pngSha256']:
                    raise ValueError(f'Source hash mismatch: {png}')
                wrapper = struct.pack('<6I', 0x50535854, 0x43, 0, 1, 0, len(raw)) + raw
                _, decoded = next(records(wrapper))
                with Image.open(png) as image:
                    actual = np.array(image.convert('RGBA'))
                if actual.shape != (240, 320, 4) or not np.array_equal(actual, np.array(decoded)):
                    raise ValueError(f'Original decode mismatch: {png}')
                arrays.append(actual)
                identities.append(meta)
            changed = np.any(arrays[0] != arrays[1], axis=2)
            ys, xs = np.where(changed)
            # Half-open source-space rectangle enclosing both footer prompts.
            outside = changed.copy()
            outside[211:228, 209:306] = False
            oy, ox = np.where(outside)
            pairs.append({
                'sources': identities,
                'changedPixels': len(xs),
                'changedBounds': [int(xs.min()), int(ys.min()), int(xs.max()+1), int(ys.max()+1)],
                'outsidePromptRectangle': [
                    {'x': int(x), 'y': int(y), 'continue': arrays[0][y, x].tolist(),
                     'loading': arrays[1][y, x].tolist()} for y, x in zip(oy, ox)],
                'delta': [{'x': int(x), 'y': int(y), 'continue': arrays[0][y, x].tolist(),
                           'loading': arrays[1][y, x].tolist()} for y, x in zip(ys, xs)],
            })
        fonts = {name: sha(disc.read(name)) for name in sorted(disc.files) if 'FONT' in name}
        result = {'status': 'verified original source evidence; no authored upscale',
                  'promptRectangle': [209, 211, 306, 228], 'pairs': pairs, 'fontSources': fonts}
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='ascii')
        for pair in pairs:
            print(f"{pair['sources'][0]['source']}: {pair['changedPixels']} differences, "
                  f"{len(pair['outsidePromptRectangle'])} outside prompt rectangle")
        print(f'Verified {len(pairs)*2} original images; {len(set(fonts.values()))} distinct font streams')
    finally:
        disc.close()


if __name__ == '__main__':
    main()
