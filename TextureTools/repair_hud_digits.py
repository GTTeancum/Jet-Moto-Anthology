"""Re-render the source-fitted vector digits in existing restored HUD atlases."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image
from reconstruct_hud_buoys import vector_digits


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--stage', type=Path)
    args = parser.parse_args()
    manifest = []
    for entry in json.loads((args.source/'manifest.json').read_text()):
        relative = entry['output']
        source = args.source/relative
        assert hashlib.sha256(source.read_bytes()).hexdigest() == entry['outputSha256']
        original = Image.open(source).convert('RGBA')
        result = vector_digits(original.copy())
        before, after = np.array(original), np.array(result)
        before[80:132, :400] = after[80:132, :400]
        assert np.array_equal(before, after), 'Only numeral cells may change'
        destination = args.output/relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        result.save(destination)
        if args.stage:
            target = args.stage/relative
            assert target.read_bytes() in (source.read_bytes(), destination.read_bytes())
            target.write_bytes(destination.read_bytes())
        manifest.append(dict(output=relative,sourceSha256=entry['outputSha256'],
            outputSha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
    assert len(manifest) == 10
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('Repaired all 10 numeral atlases; all pixels outside digit cells unchanged.')


if __name__ == '__main__':
    main()
