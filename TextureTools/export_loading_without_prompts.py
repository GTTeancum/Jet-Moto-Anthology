"""Dump original loading pages with the baked state-prompt area transparent."""
import hashlib
import json
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED

import numpy as np
from PIL import Image


def main():
    root = Path(__file__).resolve().parents[1]
    output = root / 'exports/loading-screens-no-prompts'
    output.mkdir(parents=True, exist_ok=False)
    tracks = [
        ('SWAMP1', 'Cypress Run', 0), ('SWAMP2', 'Blackwater Falls', 1),
        ('SWAMP3', 'Suicide Swamp', 2), ('ISLAND1', 'Joyride', 3),
        ('ISLAND2', 'Cliffdiver', 4), ('ISLAND3', 'Hammerhead', 5),
        ('ALPINE1', 'Willpower', 6), ('ALPINE2', 'Ice Crusher', 7),
        ('ALPINE3', 'Snow Blind', 8), ('DARK', 'Nightmare', 9),
    ]
    box = (209, 209, 314, 231)
    records = []
    for folder, name, index in tracks:
        source = root / f'reports/build12/menu-original-tim/{folder}/OVERV{index}L.png'
        metadata = json.loads(Path(str(source) + '.json').read_text())
        assert hashlib.sha256(source.read_bytes()).hexdigest() == metadata['pngSha256']
        original = np.array(Image.open(source).convert('RGB').convert('RGBA'))
        assert original.shape == (240, 320, 4)
        result = original.copy()
        x0, y0, x1, y1 = box
        result[y0:y1, x0:x1] = 0
        filename = f'{index + 1:02d}-{name.lower().replace(" ", "-")}.png'
        target = output / filename
        Image.fromarray(result).save(target)
        saved = np.array(Image.open(target))
        visible = saved[:, :, 3] != 0
        assert np.array_equal(saved[visible], original[visible])
        assert np.count_nonzero(~visible) == (x1-x0)*(y1-y0)
        records.append(dict(file=filename, track=name, source=metadata,
                            sha256=hashlib.sha256(target.read_bytes()).hexdigest()))
    (output / 'README.txt').write_text(
        'Ten original Jet Moto loading screens, 320x240 RGBA PNG.\n'
        'No upscaling, reconstruction, or filtering.\n'
        'The bottom-right Loading/Press X prompt and its bar are removed by\n'
        'making rectangle [209,209,314,231) transparent. The hidden background\n'
        'is not recovered or invented. All visible pixels match the original\n'
        'Loading image exactly. Map arrows, markers and legend are retained.\n',
        encoding='utf-8')
    (output / 'manifest.json').write_text(json.dumps(
        dict(width=320, height=240, transparentRectangle=box, screens=records),
        indent=2) + '\n', encoding='utf-8')
    archive = output.with_suffix('.zip')
    with ZipFile(archive, 'x', ZIP_DEFLATED) as bundle:
        for file in sorted(output.iterdir()):
            bundle.write(file, file.name)
    print(f'Exported and verified {len(records)} screens: {output}')
    print(archive)


if __name__ == '__main__':
    main()
