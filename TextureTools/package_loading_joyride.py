"""Package the reviewed source-guided Joyride pair for isolated runtime testing."""
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    root = Path(__file__).resolve().parents[1]
    reports = root / 'reports/build12'
    preview = reports / 'menu-loading-joyride-map-study-v3'
    output = reports / 'menu-loading-runtime-candidate/ISLAND1'
    staging = root / '.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x/ISLAND1'
    records = []
    for stem in ('OVERV3L', 'OVERV3'):
        source = reports / f'menu-original-tim/ISLAND1/{stem}.png'
        original = json.loads(Path(str(source) + '.json').read_text())
        assert digest(source) == original['pngSha256'], 'Original changed'
        candidate = preview / f'{stem}-map-study.png'
        with Image.open(candidate) as image:
            assert image.size == (1280, 960)
        meta = {
            'source': original,
            'outputSha256': digest(candidate),
            'width': 1280, 'height': 960,
            'method': 'Source-guided title paths and bounded map contours; original lettering retained',
            'provenance': str(candidate.relative_to(root)),
            'status': 'Isolated runtime candidate; full loading-screen upscale remains incomplete',
            'limitations': 'Small text, terrain, markers and footer use original samples enlarged 4x. Title background includes provisional interpolation.'
        }
        for directory in (output, staging):
            directory.mkdir(parents=True, exist_ok=True)
            target = directory / f'{stem}.png'
            if target.exists() and digest(target) != meta['outputSha256']:
                raise FileExistsError(f'Refusing to overwrite different candidate: {target}')
            shutil.copyfile(candidate, target)
            Path(str(target) + '.json').write_text(json.dumps(meta, indent=2) + '\n')
        records.append({'source': original['source'], 'sha256': meta['outputSha256']})
    print(json.dumps(records, indent=2))


if __name__ == '__main__':
    main()
