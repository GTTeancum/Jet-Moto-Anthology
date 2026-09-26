"""Rejected Joyride mask-separation experiment, retained for reproducibility.

Visual inspection found malformed e/g shapes and inconsistent strokes. This is
not a recommended reconstruction method and never deploys its outputs.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import grey_opening
import torch

from enhance_menu_background import HASHES, digest, reconstruct
from neural_models import load_model


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--weights', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError('Use a new experiment directory')
    meta = json.loads(Path(str(args.source) + '.json').read_text())
    if meta['source'] != 'ISLAND1/OVERV3.TIM' or meta['pngSha256'] != digest(args.source):
        raise ValueError('This reviewed crop is only for original Joyride Continue')
    for name, expected in HASHES.items():
        if digest(args.weights / name) != expected:
            raise ValueError('Unverified model')
    source = np.asarray(Image.open(args.source).convert('RGB'), dtype=np.float32)
    # Original paragraph bounds. Red separates the near-white foreground from
    # the dark blue grid. No new font, OCR, or regenerated wording is involved.
    crop = source[95:199, 219:310]
    background = grey_opening(crop, size=(3, 3, 1))
    coverage = np.clip((crop[:, :, 0] - background[:, :, 0]) /
                       np.maximum(232 - background[:, :, 0], 1), 0, 1)
    torch.set_num_threads(3)
    model = load_model(args.weights, denoise=1).to(memory_format=torch.channels_last)
    alpha = np.clip(reconstruct(np.repeat((coverage * 255)[:, :, None], 3, axis=2), model).mean(axis=2) / 255, 0, 1)
    bg = np.asarray(Image.fromarray(np.rint(background).astype(np.uint8)).resize(
        (crop.shape[1] * 4, crop.shape[0] * 4), Image.Resampling.BICUBIC), dtype=np.float32)
    result = bg * (1 - alpha[:, :, None]) + 232 * alpha[:, :, None]
    args.output.mkdir(parents=True)
    Image.fromarray(np.rint(result).clip(0, 255).astype(np.uint8)).save(args.output / 'paragraph.png')
    Image.fromarray(np.rint(coverage * 255).astype(np.uint8)).save(args.output / 'source-coverage.png')
    Image.fromarray(np.rint(alpha * 255).astype(np.uint8)).save(args.output / 'learned-coverage.png')
    (args.output / 'manifest.json').write_text(json.dumps({
        'source': meta, 'models': HASHES, 'bounds': [219, 95, 91, 104],
        'method': 'source foreground coverage separation; neural mask reconstruction',
        'status': 'offline paragraph experiment; neither full-screen nor runtime accepted',
    }, indent=2) + '\n')


if __name__ == '__main__':
    main()
