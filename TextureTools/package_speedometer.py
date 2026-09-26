"""Package the restored gauge into the approved HUD atlas, retaining PS1 STP.

Artwork is generated externally; this only resizes, assigns native alpha classes,
and packages it. Approved text and buoy images are not regenerated.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image
from scipy.ndimage import binary_propagation, distance_transform_edt


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--art', type=Path, required=True)
    parser.add_argument('--approved', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--stage', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    art = Image.open(args.art).convert('RGB')
    art.save(args.output/'speedometer-art.png')
    rgb = np.array(art.resize((416,256), Image.Resampling.LANCZOS))
    black = rgb.max(axis=2) < 12
    seed = np.zeros(black.shape, bool)
    seed[0] = black[0]; seed[:,0] = black[:,0]; seed[:,-1] = black[:,-1]
    occupied = ~binary_propagation(seed, mask=black)
    # Alpha 128 is PS1 semitransparency, not ordinary PNG half-opacity. Keep
    # the translucent face and opaque thin outer rim used by the original.
    depth = distance_transform_edt(occupied)
    alpha = np.where(occupied, np.where(depth <= 4,255,128),0).astype('uint8')
    rgba = np.dstack((rgb,alpha)); rgba[~occupied,:3] = 0
    gauge = Image.fromarray(rgba)
    manifest = []
    for entry in json.loads((args.approved/'manifest.json').read_text()):
        if entry['id'] != '0000F4AD': continue
        relative = entry['output']; source = args.approved/relative
        assert digest(source) == entry['outputSha256']
        approved = Image.open(source).convert('RGBA')
        result = approved.copy(); result.paste(gauge,(0,256))
        assert np.array_equal(np.array(result)[:256],np.array(approved)[:256])
        assert set(np.unique(np.array(result)[:,:,3])) <= {0,128,255}
        destination = args.output/relative
        destination.parent.mkdir(parents=True,exist_ok=True); result.save(destination)
        if args.stage:
            target = args.stage/relative
            assert digest(target) in (entry['outputSha256'],digest(destination)), str(target)
            target.write_bytes(destination.read_bytes())
        manifest.append(dict(output=relative,approvedSha256=entry['outputSha256'],
                             outputSha256=digest(destination)))
    assert len(manifest) == 10
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(f'Packaged {len(manifest)} HUD atlases; upper half is byte-identical to approved pixels.')


if __name__ == '__main__': main()
