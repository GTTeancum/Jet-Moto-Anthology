"""Rebuild source-owned HUD contours and buoy silhouettes at four times resolution.

Original palette/STP categories, atlas coordinates and gameplay geometry are retained.
Only the upper buoy silhouette is fitted to its original circular outline; the
submerged reflection retains the original mask. No replacement lettering is used.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import distance_transform_edt
from build_native_pack import Disc, records

TRACKS = ['ISLAND1', 'ISLAND2', 'ISLAND3', 'SWAMP1', 'SWAMP2', 'SWAMP3',
          'ALPINE1', 'ALPINE2', 'ALPINE3', 'DARK']
BUOYS = {0xC935, 0xC94F, 0xAD7A, 0xAD7B}
HUD = {0xF4AD, 0xEF98, 0xEF40, 0xEF49, 0x86E6}


def refine(original, existing, buoy):
    source = np.array(original)
    occupied = source[:, :, 3] != 0
    yi, xi = distance_transform_edt(~occupied, return_distances=False, return_indices=True)
    size = (original.width * 4, original.height * 4)
    # Extend source color through invisible texels before filtering, preventing
    # black seams when the reconstructed contour crosses a source pixel edge.
    color = Image.fromarray(source[yi, xi, :3]).resize(size, Image.Resampling.BICUBIC)
    mask = Image.fromarray(occupied.astype('uint8') * 255).resize(size, Image.Resampling.BICUBIC)
    inside = np.array(mask) >= 128
    classes = np.array(Image.fromarray(source[yi, xi, 3]).resize(size, Image.Resampling.NEAREST))
    result = np.array(existing.convert('RGBA'))
    if buoy:
        # Fit the top hemisphere to measured source row extents. Least squares
        # removes raster stair steps while retaining the source center/radius.
        points = []
        for y in range(2, original.height // 2):
            xs = np.flatnonzero(occupied[y])
            if len(xs):
                points.extend([(xs[0] - .5, y), (xs[-1] + .5, y)])
        p = np.array(points)
        a, b, c = np.linalg.lstsq(np.c_[2*p[:, 0], 2*p[:, 1], np.ones(len(p))],
                                 (p*p).sum(axis=1), rcond=None)[0]
        radius2 = c + a*a + b*b
        y, x = np.mgrid[:size[1], :size[0]] / 4 + .125 - .5
        upper = y < original.height / 2
        inside[upper] = ((x-a)**2 + (y-b)**2 <= radius2)[upper]
        # Preserve neural shading in the body, extending its occupied RGB at
        # the new edge only. The alpha remains categorical for PS1 blend modes.
        valid = result[:, :, 3] != 0
        ey, ex = distance_transform_edt(~valid, return_distances=False, return_indices=True)
        result[:, :, :3] = result[ey, ex, :3]
    else:
        # Keep the neural dashboard body. Restore lettering from original art
        # to avoid neural digit deformation and preserve the original typeface.
        result[:, :, :3] = np.array(color)
    result[:, :, 3] = np.where(inside, classes, 0)
    result[~inside, :3] = 0
    return Image.fromarray(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--pack', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    disc = Disc(args.cue)
    entries = []
    for track in TRACKS:
        bank = f'{track}/{track}.TMS'
        data = disc.read(bank)
        for meta, original in records(data):
            ident = int(meta['texture_id'], 16)
            if ident not in HUD | BUOYS:
                continue
            rel = f'{track}/{track}/{meta["ordinal"]:04d}-{ident:08X}.png'
            existing = Image.open(args.pack / rel).convert('RGBA')
            result = refine(original, existing, ident in BUOYS)
            if ident == 0xF4AD:
                # Dashboard and needles keep the existing detailed shading.
                result.paste(existing.crop((0, 64*4, 128*4, 128*4)), (0, 64*4))
            dest = args.output / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            result.save(dest)
            entries.append(dict(source=bank, ordinal=meta['ordinal'], id=meta['texture_id'],
                sourceSha256=hashlib.sha256(data).hexdigest(), output=rel,
                outputSha256=hashlib.sha256(dest.read_bytes()).hexdigest(),
                method='source-fitted circular upper silhouette' if ident in BUOYS else 'source lettering contours; retained dashboard artwork'))
    (args.output / 'manifest.json').write_text(json.dumps(entries, indent=2)+'\n')
    print(f'Authored {len(entries)} original-addressed HUD/buoy assets in {args.output}')


if __name__ == '__main__':
    main()
