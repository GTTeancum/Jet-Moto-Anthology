"""Create an offline neural candidate from a verified original BS/TIM extraction.

Does not deploy assets or imply visual acceptance. Baked lettering remains part of
the original image: no font substitution, redraw, or generated artwork.
"""
import argparse
import hashlib
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
import torch

from neural_models import load_model

HASHES = {
    'realesr-general-x4v3.pth': '8dc7edb9ac80ccdc30c3a5dca6616509367f05fbc184ad95b731f05bece96292',
    'realesr-general-wdn-x4v3.pth': '1641f8c4464b9f097c9fdda5589273713f67cf59f3d909e0bd688f0cee269dca',
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def reconstruct(rgb, model, tile=128):
    h, w, _ = rgb.shape
    result = np.empty((h * 4, w * 4, 3), np.float32)
    # The compact network has 34 local convolutions; 40 source pixels keep
    # artificial tile boundaries outside the retained inference region.
    pad = 40
    padded = np.pad(rgb, ((pad, pad), (pad, pad), (0, 0)), mode='reflect')
    with torch.inference_mode():
        for y in range(0, h, tile):
            for x in range(0, w, tile):
                th, tw = min(tile, h - y), min(tile, w - x)
                patch = padded[y:y + th + 2 * pad, x:x + tw + 2 * pad]
                tensor = torch.from_numpy(patch.astype(np.float32).transpose(2, 0, 1) / 255).unsqueeze(0)
                out = model(tensor.to(memory_format=torch.channels_last))[0]
                out = out[:, pad * 4:(pad + th) * 4, pad * 4:(pad + tw) * 4]
                result[y * 4:(y + th) * 4, x * 4:(x + tw) * 4] = out.permute(1, 2, 0).numpy() * 255
            print(f'Inference rows {y + th}/{h}', flush=True)
    if not np.isfinite(result).all():
        raise ValueError('Non-finite neural result')
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--weights', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--right-source', type=Path,
                        help='Reviewed horizontal continuation tile; reconstruct jointly')
    parser.add_argument('--right-output', type=Path)
    parser.add_argument('--denoise', type=float, default=0.5)
    parser.add_argument('--threads', type=int, default=3)
    parser.add_argument('--opaque-cutout', action='store_true',
                        help='Reconstruct binary transparency; reject semi-transparent source pixels')
    args = parser.parse_args()
    if bool(args.right_source) != bool(args.right_output):
        parser.error('Paired authoring requires both right-source and right-output')
    if not 0 <= args.denoise <= 1 or args.threads < 1:
        parser.error('Invalid denoise or thread count')
    if args.output.exists() or Path(str(args.output) + '.json').exists():
        raise FileExistsError('Refusing to overwrite a prior candidate')
    if args.right_output and (args.right_output == args.output or args.right_output.exists() or
                              Path(str(args.right_output) + '.json').exists()):
        raise FileExistsError('Right output must be a distinct new candidate')
    provenance = json.loads(Path(str(args.source) + '.json').read_text())
    if re.search(r'/OVERV[0-9]L?\.TIM$', provenance['source'], re.IGNORECASE):
        raise ValueError('Full-image SR rejected for track overviews: it distorts lettering and map lines. '
                         'Use a separately reviewed overview reconstruction experiment.')
    if provenance['pngSha256'].lower() != digest(args.source):
        raise ValueError('Source extraction checksum mismatch')
    for name, expected in HASHES.items():
        if digest(args.weights / name) != expected:
            raise ValueError(f'Unverified model: {name}')
    source_image = Image.open(args.source).convert('RGBA')
    if source_image.size != (provenance['width'], provenance['height']):
        raise ValueError('Source dimensions disagree with provenance')
    left_width = source_image.width
    right_provenance = None
    if args.right_source:
        right_provenance = json.loads(Path(str(args.right_source) + '.json').read_text())
        if right_provenance['pngSha256'].lower() != digest(args.right_source):
            raise ValueError('Right source extraction checksum mismatch')
        right = Image.open(args.right_source).convert('RGBA')
        if right.size != (right_provenance['width'], right_provenance['height']) or right.height != source_image.height:
            raise ValueError('Continuation tile dimensions disagree')
        if (provenance['source'], provenance['sourceSha256']) != (right_provenance['source'], right_provenance['sourceSha256']):
            raise ValueError('Continuation tiles must belong to the same original bank')
        joined = Image.new('RGBA', (source_image.width + right.width, source_image.height))
        joined.paste(source_image, (0, 0))
        joined.paste(right, (source_image.width, 0))
        source_image = joined
    alpha = np.asarray(source_image)[:, :, 3]
    if args.opaque_cutout and not set(np.unique(alpha)).issubset({0, 255}):
        raise ValueError('Cutout authoring cannot discard semi-transparency')
    if not args.opaque_cutout and np.any(alpha != 255):
        raise ValueError('Transparent artwork requires explicit cutout authoring')
    im = source_image.convert('RGB')
    torch.set_num_threads(args.threads)
    model = load_model(args.weights, denoise=args.denoise).to(memory_format=torch.channels_last)
    out = reconstruct(np.asarray(im), model)
    reference = np.asarray(im.resize((im.width * 4, im.height * 4), Image.Resampling.BICUBIC), np.float32)
    # Preserve original broad colors without mixing the original jagged edges
    # back into the reconstructed high-frequency detail.
    out += gaussian_filter(reference - out, sigma=(8, 8, 0), mode='reflect')
    result = Image.fromarray(np.rint(out).clip(0, 255).astype(np.uint8))
    if args.opaque_cutout:
        coverage = reconstruct(np.repeat(alpha[:, :, None], 3, axis=2), model).mean(axis=2) >= 127.5
        rgba = np.asarray(result.convert('RGBA')).copy()
        rgba[:, :, 3] = coverage.astype(np.uint8) * 255
        rgba[~coverage] = 0
        result = Image.fromarray(rgba)
    outputs = [(args.output, provenance, result)]
    if right_provenance is not None:
        outputs = [
            (args.output, provenance, result.crop((0, 0, left_width * 4, result.height))),
            (args.right_output, right_provenance, result.crop((left_width * 4, 0, result.width, result.height))),
        ]
    for path, original, tile in outputs:
        path.parent.mkdir(parents=True, exist_ok=True)
        tile.save(path)
        Path(str(path) + '.json').write_text(json.dumps({
            'source': original, 'models': HASHES, 'denoise': args.denoise,
            'method': '4x SRVGG learned reconstruction; broad-color constraint',
            'joinedSources': [provenance, right_provenance] if right_provenance else None,
            'transparency': 'learned binary cutout' if args.opaque_cutout else 'opaque',
            'outputSha256': digest(path), 'width': tile.width, 'height': tile.height,
            'status': 'candidate only; fidelity and native integration not accepted',
        }, indent=2) + '\n', encoding='ascii')


if __name__ == '__main__':
    main()
