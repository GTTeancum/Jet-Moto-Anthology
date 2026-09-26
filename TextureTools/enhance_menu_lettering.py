"""Reconstruct original two-color menu lettering without substituting fonts.

Offline candidate authoring only. RGB palette and PS1 transparency semantics are
preserved. Learned foreground/occupancy contours replace nearest-repeated alpha.
"""
import argparse
import hashlib
import json
import struct
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import label
import torch

from build_native_pack import Disc, records
from enhance_menu_background import HASHES, digest, reconstruct
from neural_models import load_model


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue', type=Path, required=True)
    parser.add_argument('--weights', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a new candidate directory')
    for name, expected in HASHES.items():
        if digest(args.weights / name) != expected:
            raise ValueError('Unverified neural model')
    disc = Disc(args.cue)
    try:
        bank = disc.read('MISC/OPTIONS.TMS')
        model_data = disc.read('MISC/OPTIONS.DMD')
    finally:
        disc.close()
    source = {meta['ordinal']: (meta, image) for meta, image in records(bank)}
    gold, white = (np.asarray(source[i][1]) for i in (1, 2))
    if set(np.unique(white[:, :, 3])) != {0, 255}:
        raise ValueError('Unexpected semi-transparency; this reviewed profile is opaque lettering only')
    foreground = np.all(white[:, :, :3] == 208, axis=2)
    shadow = np.all(white[:, :, :3] == 8, axis=2)
    occupied = white[:, :, 3] != 0
    if not np.array_equal(foreground | shadow, occupied) or not np.array_equal(gold[:, :, 3], white[:, :, 3]):
        raise ValueError('Original lettering masks do not match the reviewed two-color profile')
    if not np.array_equal(np.all(gold[:, :, :3] == [248, 184, 0], axis=2), foreground):
        raise ValueError('Original highlighted lettering differs geometrically')
    torch.set_num_threads(3)
    model = load_model(args.weights, denoise=1).to(memory_format=torch.channels_last)

    def contour(mask):
        rgb = np.repeat((mask.astype(np.uint8) * 255)[:, :, None], 3, axis=2)
        return reconstruct(rgb, model).mean(axis=2) >= 127.5

    fg = contour(foreground)
    coverage = contour(occupied) | fg
    args.output.mkdir(parents=True)
    report = []
    regions = set()
    # Collect source-authored textured polygons, not screen-space rectangles.
    # Runtime still validates each polygon through its original native material.
    for at in range(0, len(model_data) - 36, 4):
        colors, words = model_data[at + 1:at + 3]
        op = model_data[at + 19]
        count = 4 if op & 8 else 3
        uv = at + 16 + colors * 4
        if not (1 <= colors <= 4 and 0x20 <= op <= 0x3f and op & 4 and
                uv + count * 4 <= at + words * 4 <= len(model_data)):
            continue
        clut = struct.unpack_from('<H', model_data, uv + 2)[0]
        page = struct.unpack_from('<H', model_data, uv + 6)[0]
        if (page >> 7) & 3 != 0 or (clut & 63) * 16 not in (768, 784) or clut >> 6 != 0:
            continue
        us = [model_data[uv + i * 4] for i in range(count)]
        vs = [model_data[uv + i * 4 + 1] for i in range(count)]
        regions.add((min(us), min(vs), max(us) - min(us) + 1, max(vs) - min(vs) + 1))
    for ordinal, color in ((1, [248, 184, 0]), (2, [208, 208, 208])):
        meta, original = source[ordinal]
        original.save(args.output / f'original-{ordinal}.png')
        rgba = np.zeros((*coverage.shape, 4), dtype=np.uint8)
        rgba[coverage] = [8, 8, 8, 255]
        rgba[fg] = [*color, 255]
        path = args.output / f"{ordinal:04d}-{meta['texture_id']}.png"
        Image.fromarray(rgba).save(path)
        region_reports = []
        for u, v, w, h in sorted(regions):
            native_foreground = foreground[v:v + h, u:u + w]
            rows = np.flatnonzero(native_foreground.any(axis=1))
            if not len(rows):
                continue
            tile = rgba[v * 4:(v + h) * 4, u * 4:(u + w) * 4].copy()
            # A leading row with shadow but no letter belongs to the preceding
            # packed label. Clear only this crop, never the shared source atlas.
            leading = int(rows[0])
            tile[:leading * 4] = 0
            region = args.output / 'Regions' / path.stem / f'{u:03d}-{v:03d}-{w:03d}-{h:03d}.png'
            region.parent.mkdir(parents=True, exist_ok=True)
            Image.fromarray(tile).save(region)
            region_reports.append({'bounds': [u, v, w, h], 'leadingForeignShadowRows': leading,
                                   'sha256': digest(region)})
        report.append({'ordinal': ordinal, 'id': meta['texture_id'], 'output': path.name,
                       'sha256': digest(path), 'regions': region_reports})
    reduced = np.asarray(Image.fromarray(fg.astype(np.uint8) * 255).resize((256, 256), Image.Resampling.BOX)) >= 128
    (args.output / 'manifest.json').write_text(json.dumps({
        'source': 'MISC/OPTIONS.TMS', 'sourceSha256': hashlib.sha256(bank).hexdigest(),
        'method': 'SRVGG mask reconstruction, original solid palette; identical highlight geometry',
        'models': HASHES, 'foregroundComponentsOriginal': int(label(foreground)[1]),
        'foregroundComponentsCandidate': int(label(fg)[1]),
        'roundTripForegroundChangedPixels': int(np.count_nonzero(reduced != foreground)),
        'status': 'unaccepted candidate: inspect every label and native draw before deployment',
        'outputs': report,
    }, indent=2) + '\n', encoding='ascii')


if __name__ == '__main__':
    main()
