"""Rejected diagnostic candidate; do not deploy (soft detail and shading bands).

Retained to reproduce the comparison that led to the native tile-UV correction.
The accepted rendering candidate keeps the existing neural artwork unchanged.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter
from scipy.ndimage import distance_transform_edt, gaussian_filter
from build_native_pack import Disc, records


def rebuild(original):
    source = np.asarray(original)
    occupied = source[:, :, 3] != 0
    # Extend edge colors before filtering so black transparency cannot leave fringes.
    y, x = distance_transform_edt(~occupied, return_distances=False, return_indices=True)
    rgb = source[:, :, :3][y, x].astype(np.float32)
    rgb = gaussian_filter(rgb, sigma=(0.6, 0.6, 0))
    color = Image.fromarray(np.rint(rgb).clip(0, 255).astype(np.uint8))
    color = color.resize((original.width * 4, original.height * 4), Image.Resampling.LANCZOS)
    color = color.filter(ImageFilter.UnsharpMask(radius=1.6, percent=65, threshold=3))
    # Keep occupied regions/STP categories exact; runtime cutout filtering handles sampling.
    color.putalpha(original.getchannel('A').resize(color.size, Image.Resampling.NEAREST))
    return color


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cue', type=Path, required=True)
    p.add_argument('--output', type=Path, required=True)
    a = p.parse_args()
    a.output.mkdir(parents=True, exist_ok=False)
    catalog = json.loads((Path(__file__).parent.parent / 'JetMoto/config/native-banks.json').read_text())
    manifest = []
    disc = Disc(a.cue)
    try:
        for name in sorted(n for n in disc.files if n.startswith('PICKTRAC/') and n.endswith('.TMS')):
            data = disc.read(name)
            digest = hashlib.sha256(data).hexdigest()
            if digest.lower() != catalog[name].lower():
                raise ValueError(f'Unsupported original revision: {name}')
            for meta, original in records(data):
                # Exact authored dial segment IDs, shared across difficulty banks.
                if meta['texture_id'] not in {'00001D72', '00001D7D', '00001D7A', '00001D83',
                                              '00001D86', '00001D89', '00001D8E', '00001D8F',
                                              '00001D9F', '00001DA8'}:
                    continue
                rel = Path(name).with_suffix('') / f"{meta['ordinal']:04d}-{meta['texture_id']}.png"
                target = a.output / rel
                target.parent.mkdir(parents=True, exist_ok=True)
                rebuild(original).save(target)
                source = a.output / 'originals' / rel
                source.parent.mkdir(parents=True, exist_ok=True)
                original.save(source)
                manifest.append(dict(source=name, sourceSha256=digest, png=rel.as_posix(),
                                     sha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                                     method='source de-dither, Lanczos 4x, original categorical alpha'))
    finally:
        disc.close()
    (a.output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='ascii')
    print(f'Exported {len(manifest)} original-ID dial segments.')


if __name__ == '__main__':
    main()
