"""REJECTED source-only repeated-word reconstruction experiment; never stage.

Uses four manually verified occurrences of 'the' in original Joyride artwork.
No font, learned image model, or runtime staging. Horizontal sample registration
is fitted from the source; vertical interpolation cannot recover missing detail.
All three variants failed visual review: e lost its crossbar and t became
rounded. Numerical source fit did not establish faithful letter reconstruction.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates
from scipy.optimize import least_squares, lsq_linear


COORDS = [(282, 117), (273, 157), (239, 177), (233, 187)]


def sampler(offset, width=16, scale=4):
    # Exact overlap with a unit-width source pixel, for a piecewise-constant
    # high-resolution field. The patch has empty margins for registration.
    lo = np.arange(width * scale) / scale
    hi = lo + 1 / scale
    starts = np.arange(width)[:, None] - offset
    return np.maximum(0, np.minimum(starts + 1, hi) - np.maximum(starts, lo))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a fresh experiment output')
    meta = json.loads(Path(str(args.source) + '.json').read_text())
    if meta['source'] != 'ISLAND1/OVERV3.TIM' or hashlib.sha256(args.source.read_bytes()).hexdigest() != meta['pngSha256']:
        raise ValueError('Expected verified original Joyride Continue')
    source = np.asarray(Image.open(args.source).convert('RGB'), dtype=float)
    patches = [source[y:y+10, x:x+16, 0] for x, y in COORDS]
    yy, xx = np.mgrid[:10, :16]
    registrations = [[0., 0., 1., 0.]]
    for patch in patches[1:]:
        def residual(v):
            sample = map_coordinates(patch, [yy+v[1], xx+v[0]], order=3, mode='nearest')
            return (sample*v[2]+v[3]-patches[0]).ravel()
        fit = least_squares(residual, [0, 0, 1, 0],
                            bounds=([-1, -1, .8, -16], [1, 1, 1.2, 16]))
        registrations.append(fit.x.tolist())
    # Tiny fitted vertical offsets are diagnostic only: the observations do
    # not establish additional vertical phases. Recover horizontal detail only.
    matrix = np.vstack([sampler(v[0]) for v in registrations])
    observations = np.concatenate([p*v[2]+v[3] for p, v in zip(patches, registrations)], axis=1).T
    curvature = np.diff(np.eye(64), n=2, axis=0)
    args.output.mkdir(parents=True)
    Image.fromarray(source[117:127, 282:298].astype(np.uint8)).resize(
        (256, 160), Image.Resampling.NEAREST).save(args.output/'original-word-nearest.png')
    outcomes = []
    for penalty in [.01, .05, .2]:
        design = np.vstack([matrix, penalty**.5 * curvature])
        target = np.vstack([observations, np.zeros((len(curvature), 10))])
        recovered = np.column_stack([lsq_linear(design, target[:, row], bounds=(0, 232),
                                               tol=1e-8).x for row in range(10)]).T
        rmse = float(np.sqrt(np.mean((matrix@recovered.T-observations)**2)))
        # White-on-dark monochrome diagnostic avoids synthesizing a text-free
        # background. This is not a composited paragraph or menu candidate.
        intensity = np.clip((recovered-8)/224, 0, 1)
        alpha = Image.fromarray(np.rint(intensity*255).astype(np.uint8))
        alpha = alpha.resize((64, 40), Image.Resampling.LANCZOS)
        rgb = np.asarray(alpha, dtype=float)[:, :, None]/255
        color = np.rint(np.array([0, 24, 40])*(1-rgb)+232*rgb).astype(np.uint8)
        out = args.output/f'repeated-word-{penalty}.png'
        Image.fromarray(color).save(out)
        Image.fromarray(color).resize((256, 160), Image.Resampling.NEAREST).save(
            args.output/f'repeated-word-{penalty}-inspection.png')
        outcomes.append({'penalty': penalty, 'sourceFitRmse': rmse,
                         'pngSha256': hashlib.sha256(out.read_bytes()).hexdigest()})
    (args.output/'manifest.json').write_text(json.dumps({
        'source': meta, 'bounds': [[x, y, 16, 10] for x, y in COORDS],
        'registrationDxDyGainBias': registrations, 'outcomes': outcomes,
        'status': 'REJECTED after visual review: malformed e/t; never stage',
        'limitation': 'no additional vertical samples; synthetic diagnostic background only',
    }, indent=2)+'\n', encoding='ascii')
    print(json.dumps({'registrations': registrations, 'outcomes': outcomes}, indent=2))


if __name__ == '__main__':
    main()
