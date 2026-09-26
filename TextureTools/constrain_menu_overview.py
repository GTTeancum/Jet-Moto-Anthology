"""Rejected offline experiment: constrain a reconstruction to original samples.

This is not an acceptance gate or deployment tool. A small round-trip error does
not establish legible typography or faithful subpixel linework. Inspect the
authored image and native loading/continue transition separately. The Joyride
experiment reduced sample error but retained warped letters and introduced halos.
"""
import argparse
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

from enhance_menu_background import digest


def reduce4(image):
    h, w, channels = image.shape
    return image.reshape(h // 4, 4, w // 4, 4, channels).mean(axis=(1, 3))


def enlarge(image):
    return np.stack([
        np.asarray(Image.fromarray(image[:, :, c]).resize(
            (image.shape[1] * 4, image.shape[0] * 4), Image.Resampling.BICUBIC))
        for c in range(3)], axis=2)


def verified(path, key):
    meta = json.loads(Path(str(path) + '.json').read_text())
    if meta[key].lower() != digest(path):
        raise ValueError(f'Checksum mismatch: {path}')
    image = Image.open(path).convert('RGB')
    if image.size != (meta['width'], meta['height']):
        raise ValueError(f'Dimensions disagree with manifest: {path}')
    return np.asarray(image, dtype=np.float32), meta


def error_stats(result, source):
    error = np.abs(reduce4(result) - source)
    return {'meanAbsoluteChannelError': float(error.mean()),
            'p99AbsoluteChannelError': float(np.quantile(error, .99)),
            'maxAbsoluteChannelError': float(error.max())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--prior', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--iterations', type=int, default=80)
    args = parser.parse_args()
    if args.iterations < 1 or args.iterations > 500:
        parser.error('iterations must be 1..500')
    if args.output.exists() or Path(str(args.output) + '.json').exists():
        raise FileExistsError('Refusing to overwrite an experiment')
    source, original = verified(args.source, 'pngSha256')
    prior, prior_meta = verified(args.prior, 'outputSha256')
    if not re.fullmatch(r'(SWAMP[123]|ISLAND[123]|ALPINE[123]|DARK)/OVERV[0-9]L?\.TIM', original['source']):
        raise ValueError('Only original track overview TIMs are in scope')
    if source.shape != (240, 320, 3) or prior.shape != (960, 1280, 3):
        raise ValueError('Expected original 320x240 and prior 1280x960')
    if prior_meta['source'] != original:
        raise ValueError('Prior belongs to a different original extraction')
    result = prior.copy()
    # Correct spatially local evidence, unlike the broad sigma=8 color correction
    # in the initial SR pass. Smooth corrections avoid hard 4x4 block boundaries.
    # A 2-level deadband allows a portion of the source's 5-bit quantization error.
    for _ in range(args.iterations):
        residual = source - reduce4(result)
        residual = np.sign(residual) * np.maximum(np.abs(residual) - 2, 0)
        correction = gaussian_filter(enlarge(residual), sigma=(.65, .65, 0))
        result = np.clip(result + .75 * correction, 0, 255)
    encoded = np.rint(result).astype(np.uint8)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    Image.fromarray(encoded).save(args.output)
    meta = {
        'source': original, 'priorSha256': digest(args.prior),
        'method': 'iterative local source-sample constraint on existing SR prior',
        'iterations': args.iterations, 'sourceDeadband': 2,
        'before': error_stats(prior, source),
        'after': error_stats(encoded.astype(np.float32), source),
        'outputSha256': digest(args.output), 'width': 1280, 'height': 960,
        'status': 'offline experiment only; visual quality and native behavior unaccepted',
    }
    Path(str(args.output) + '.json').write_text(json.dumps(meta, indent=2) + '\n')
    print(json.dumps({'before': meta['before'], 'after': meta['after']}))


if __name__ == '__main__':
    main()
