"""REJECTED offline Joyride source-contour experiment; never stage its output.

Contours follow interpolated original coverage samples. The exported polygons
are review artifacts; neither their topology check nor resolution implies that
the original typography has been faithfully reconstructed. All six reviewed
variants produced misshapen letters or changed holes and failed visual review.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw
from scipy.ndimage import label, binary_fill_holes


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contours(field, threshold):
    """Marching squares with bilinear-center saddle disambiguation."""
    values = np.pad(field, 1)
    links = {}
    coordinates = {}

    def edge(x, y, side, corners):
        starts = ((x, y), (x + 1, y), (x, y + 1), (x, y))
        ends = ((x + 1, y), (x + 1, y + 1), (x + 1, y + 1), (x, y + 1))
        indices = ((0, 1), (1, 2), (3, 2), (0, 3))
        start, end = starts[side], ends[side]
        key = (start, end)
        a, b = (corners[i] for i in indices[side])
        t = float((threshold - a) / (b - a))
        # Samples are pixel centers. Remove the one-pixel padded border.
        coordinates[key] = np.array(start) + (np.array(end) - start) * t - .5
        return key

    for y in range(values.shape[0] - 1):
        for x in range(values.shape[1] - 1):
            c = [values[y, x], values[y, x + 1], values[y + 1, x + 1], values[y + 1, x]]
            sides = [i for i, (a, b) in enumerate(((0, 1), (1, 2), (3, 2), (0, 3)))
                     if (c[a] >= threshold) != (c[b] >= threshold)]
            if not sides:
                continue
            if len(sides) == 2:
                pairs = [sides]
            else:
                center_inside = sum(c) / 4 >= threshold
                pairs = [(0, 1), (2, 3)] if (c[0] >= threshold) == center_inside else [(0, 3), (1, 2)]
            for a, b in pairs:
                ea, eb = edge(x, y, a, c), edge(x, y, b, c)
                links.setdefault(ea, []).append(eb)
                links.setdefault(eb, []).append(ea)
    if any(len(v) != 2 for v in links.values()):
        raise ValueError('Open or branched contour')
    paths, seen = [], set()
    for start in links:
        if start in seen:
            continue
        previous, current, path = None, start, []
        while current not in seen:
            seen.add(current)
            path.append(coordinates[current])
            following = next(n for n in links[current] if n != previous)
            previous, current = current, following
        if current != start:
            raise ValueError('Contour did not close')
        paths.append(np.asarray(path))
    return paths


def rasterize(paths, size, smoothing):
    w, h = size
    mask = Image.new('1', (w * 16, h * 16))
    for path in paths:
        for _ in range(smoothing):
            following = np.roll(path, -1, axis=0)
            path = np.stack((.75 * path + .25 * following,
                             .25 * path + .75 * following), axis=1).reshape(-1, 2)
        tile = Image.new('1', mask.size)
        ImageDraw.Draw(tile).polygon([tuple(p * 16) for p in path], fill=1)
        mask = ImageChops.logical_xor(mask, tile)
    return mask.convert('L').resize((w * 4, h * 4), Image.Resampling.BOX)


def topology(mask):
    return {'components': int(label(mask)[1]),
            'holes': int(label(binary_fill_holes(mask) & ~mask)[1])}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a new experiment directory')
    meta = json.loads(Path(str(args.source) + '.json').read_text())
    if meta['source'] != 'ISLAND1/OVERV3.TIM' or meta['pngSha256'] != digest(args.source):
        raise ValueError('Reviewed coordinates require verified original Joyride')
    source = np.asarray(Image.open(args.source).convert('RGB'))
    crop = source[95:199, 219:310]
    # Red is near zero in the blue backdrop. Retain source antialias intensities,
    # including enclosed counters; no morphological opening of letter bodies.
    coverage = np.clip((crop[:, :, 0].astype(float) - 24) / (224 - 24), 0, 1)
    args.output.mkdir(parents=True)
    records = []
    for threshold in (.4, .5, .6):
        paths = contours(coverage, threshold)
        for smooth in (0, 1):
            alpha_image = rasterize(paths, (91, 104), smooth)
            alpha = np.asarray(alpha_image, dtype=float) / 255
            bg = np.array([0, 24, 40])
            fg = np.array([216, 216, 216])
            rgb = bg * (1 - alpha[:, :, None]) + fg * alpha[:, :, None]
            name = f'paragraph-{threshold:.1f}-smooth{smooth}.png'
            Image.fromarray(np.rint(rgb).astype(np.uint8)).save(args.output / name)
            reduced = np.asarray(alpha_image.resize((91, 104), Image.Resampling.BOX)) >= 128
            records.append({'file': name, 'threshold': threshold, 'smoothing': smooth,
                            'sourceTopology': topology(coverage >= threshold),
                            'resultTopology': topology(alpha >= .5),
                            'roundTripChangedMaskPixels': int(np.count_nonzero(reduced != (coverage >= threshold)))})
    (args.output / 'manifest.json').write_text(json.dumps({
        'source': meta, 'bounds': [219, 95, 91, 104], 'results': records,
        'status': 'offline geometry study on flat diagnostic background; not a full-screen candidate',
    }, indent=2) + '\n')
    print(json.dumps(records))


if __name__ == '__main__':
    main()
