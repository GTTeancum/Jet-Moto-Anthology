"""Stage supplied full-page loading artwork, retaining inputs and prior test assets."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image


TRACKS = [('SWAMP1', 'Cypress Run'), ('SWAMP2', 'Blackwater Falls'),
          ('SWAMP3', 'Suicide Swamp'), ('ISLAND1', 'Joyride'),
          ('ISLAND2', 'Cliffdiver'), ('ISLAND3', 'Hammerhead'),
          ('ALPINE1', 'Willpower'), ('ALPINE2', 'Ice Crusher'),
          ('ALPINE3', 'Snow Blind'), ('DARK', 'Nightmare')]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--stage', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    runtime = root / '.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x'
    records = []
    # Validate all sources before authoring or changing the isolated catalog.
    for index, (folder, name) in enumerate(TRACKS):
        for suffix, state in [('L', 'Loading'), ('', 'Continue')]:
            source = root / f'reports/build12/menu-original-tim/{folder}/OVERV{index}{suffix}.png'
            original = json.loads(Path(str(source)+'.json').read_text())
            if sha(source) != original['pngSha256']:
                raise ValueError(f'Original export changed: {source}')
            supplied = args.input / f'{index+1:02d}-{name.replace(" ", "-")}-{state}.png'
            with Image.open(supplied) as image:
                image.load()
                if image.size != (1024,768) or image.mode != 'RGB':
                    raise ValueError(f'Expected supplied 1024x768 RGB: {supplied}')
            records.append((supplied, Path(folder)/source.name, {
                'source': original, 'width': 1280, 'height': 960,
                'track': name, 'variant': state,
                'suppliedFile': str(supplied.resolve().relative_to(root)),
                'suppliedSha256': sha(supplied), 'suppliedWidth':1024, 'suppliedHeight':768,
                'method': 'User-supplied complete artwork; Lanczos resize for exact 4x runtime requirement',
                'status': 'Isolated user-supplied candidate; native track coverage recorded separately',
            }))
    args.output.mkdir(parents=True, exist_ok=False)
    outputs = []
    for supplied, relative, meta in records:
        target = args.output/'Textures/Menu4x'/relative
        target.parent.mkdir(parents=True, exist_ok=True)
        with Image.open(supplied) as image:
            image.resize((1280,960), Image.Resampling.LANCZOS).save(target)
        with Image.open(target) as image:
            if image.mode != 'RGB' or image.size != (1280,960):
                raise ValueError(f'Invalid runtime output: {target}')
        if sha(supplied) != meta['suppliedSha256']:
            raise ValueError('Supplied file changed during packaging')
        meta['outputSha256'] = sha(target)
        meta['provenance'] = str(target.resolve().relative_to(root))
        Path(str(target)+'.json').write_text(json.dumps(meta,indent=2)+'\n')
        outputs.append(dict(file=str(relative),**meta))
    previous = []
    if args.stage:
        # Finish backing up every affected PNG/manifest before replacing any.
        for _,relative,_ in records:
            for suffix in ('', '.json'):
                old = Path(str(runtime/relative)+suffix)
                if old.exists():
                    backup = args.output/'previous-runtime'/Path(str(relative)+suffix)
                    backup.parent.mkdir(parents=True,exist_ok=True)
                    shutil.copyfile(old,backup)
                    if sha(old) != sha(backup):
                        raise ValueError(f'Backup verification failed: {old}')
                    previous.append(dict(file=str(old.relative_to(runtime)),sha256=sha(old)))
        for _,relative,meta in records:
            target = runtime/relative
            target.parent.mkdir(parents=True,exist_ok=True)
            for suffix in ('', '.json'):
                shutil.copyfile(Path(str(args.output/'Textures/Menu4x'/relative)+suffix),
                                Path(str(target)+suffix))
            if sha(target) != meta['outputSha256']:
                raise ValueError(f'Staging verification failed: {target}')
    (args.output/'manifest.json').write_text(json.dumps(dict(
        staged=args.stage, outputs=outputs, previousRuntimeFiles=previous),indent=2)+'\n')
    print(json.dumps(dict(outputs=len(outputs),staged=args.stage,
                          backedUpFiles=len(previous),output=str(args.output))))


if __name__ == '__main__':
    main()
