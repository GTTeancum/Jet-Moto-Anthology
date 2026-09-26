"""Preserve original tiny trademark samples where learned reconstruction fails.

This local fidelity fallback does not claim to reconstruct missing glyph detail.
Only reviewed mark rectangles are restored; no fonts or substituted marks.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / 'reports/build12'
REGIONS = {0: [(127, 68, 16, 13), (223, 222, 15, 12)],
           3: [(224, 68, 15, 13)], 5: [(491, 68, 17, 13)]}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main(family='CREDITS', prefix='CREDITS', regions=REGIONS,
         output_folder='menu-credits-mark-candidates'):
    for page, boxes in regions.items():
        relative = Path(f'{family}/{prefix}{page}.png')
        original = ROOT / 'menu-original-assets' / relative
        candidate = ROOT / 'menu-quality-candidates' / relative
        output = ROOT / output_folder / relative
        if output.exists():
            raise FileExistsError(output)
        provenance = json.loads(Path(str(candidate) + '.json').read_text())
        assert sha(original).lower() == provenance['source']['pngSha256'].lower()
        assert sha(candidate).lower() == provenance['outputSha256'].lower()
        source = Image.open(original).convert('RGB')
        result = Image.open(candidate).convert('RGB')
        assert source.size == (640, 480) and result.size == (2560, 1920)
        # Upsample the complete source to avoid artificial crop-edge ringing.
        fallback = source.resize(result.size, Image.Resampling.LANCZOS)
        for x, y, w, h in boxes:
            box = (x*4, y*4, (x+w)*4, (y+h)*4)
            yy, xx = np.mgrid[:h*4, :w*4]
            edge = np.minimum.reduce([xx, yy, w*4-1-xx, h*4-1-yy])
            alpha = np.clip(edge / 8, 0, 1)[..., None]
            old = np.asarray(result.crop(box), dtype=float)
            restored = np.asarray(fallback.crop(box), dtype=float)
            result.paste(Image.fromarray(np.rint(old*(1-alpha)+restored*alpha)
                                        .astype(np.uint8)), box)
        output.parent.mkdir(parents=True, exist_ok=True)
        result.save(output)
        provenance['priorCandidateSha256'] = sha(candidate)
        provenance['markRestoration'] = {'sourcePixelRectangles': boxes,
            'method': 'original samples, Lanczos 4x, two-source-pixel boundary feather',
            'limitation': 'tiny marks retain source detail; not a new glyph reconstruction'}
        provenance['outputSha256'] = sha(output)
        provenance['status'] = 'candidate only; native integration review required'
        Path(str(output)+'.json').write_text(json.dumps(provenance, indent=2)+'\n')
        print(output)


if __name__ == '__main__':
    main()
