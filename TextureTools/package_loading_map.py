"""Package reviewed map pairs, checking source, edit scope and variant deltas."""
import argparse
import hashlib
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--candidate', type=Path, required=True)
    parser.add_argument('--stage', action='store_true')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    reports = root / 'reports/build12'
    profile = json.loads(args.profile.read_text())
    authoring = json.loads((args.candidate/'manifest.json').read_text())
    if authoring['profile'] != profile:
        raise ValueError('Candidate was authored with a different profile')
    if authoring['originalContourTopology'] != authoring['splineTopology']:
        raise ValueError('Candidate spline topology differs from source contours')
    authored_hashes = {item['file']:item['sha256'] for item in authoring['outputs']}
    name = Path(profile['source'])
    originals, candidates, records = [], [], []
    for suffix in ('', 'L'):
        relative = name.parent / (name.name + suffix + '.png')
        source = reports / 'menu-original-tim' / relative
        meta = json.loads(Path(str(source)+'.json').read_text())
        if digest(source) != meta['pngSha256']:
            raise ValueError('Original changed')
        candidate = args.candidate / (name.name+suffix+'-map-study.png')
        if digest(candidate) != authored_hashes.get(candidate.name):
            raise ValueError('Candidate differs from its authoring manifest')
        original = np.array(Image.open(source).convert('RGB').resize((1280,960),Image.Resampling.LANCZOS))
        result = np.array(Image.open(candidate).convert('RGB'))
        if result.shape != original.shape:
            raise ValueError('Unexpected candidate dimensions')
        allowed = np.zeros((960,1280),dtype=bool)
        x0,y0,x1,y1 = [v*4 for v in profile['bounds']]
        allowed[y0:y1,x0:x1] = True
        for box in profile['protected'].values():
            x0,y0,x1,y1 = [v*4 for v in box]
            allowed[y0:y1,x0:x1] = False
        changed = np.any(original != result,axis=2)
        if np.any(changed & ~allowed):
            raise ValueError('Edits escaped reviewed map area')
        originals.append(original.astype(np.int16))
        candidates.append(result.astype(np.int16))
        records.append((relative,candidate,{
            'source':meta,'outputSha256':digest(candidate),'width':1280,'height':960,
            'method':'Bounded source map contours with protected original lettering and markers',
            'provenance':str(candidate.resolve().relative_to(root)),
            'status':'Isolated partial map upgrade; full loading-screen upgrade incomplete',
            'limitations':'Title, paragraph, labels, terrain, markers, legend and prompts retain enlarged source samples',
            'changedPixels':int(changed.sum())}))
    if not np.array_equal(candidates[1]-candidates[0],originals[1]-originals[0]):
        raise ValueError('Loading/Continue source delta changed')
    destinations = [reports/'menu-loading-runtime-candidate']
    if args.stage:
        destinations.append(root/'.build/ui-buoy-bin/Release/net10.0/win-x64/Textures/Menu4x')
    # Preflight the entire pair before modifying either catalog.
    for destination in destinations:
        for relative,candidate,meta in records:
            target = destination/relative
            if target.exists() and digest(target) != meta['outputSha256']:
                raise FileExistsError(f'Refusing to overwrite different candidate: {target}')
    for destination in destinations:
        for relative,candidate,meta in records:
            target = destination/relative
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(candidate,target)
            Path(str(target)+'.json').write_text(json.dumps(meta,indent=2)+'\n')
    checks = {'pairDeltaPreserved':True,'protectedRegionsUnchanged':True,
              'outputs':[dict(file=str(r),**m) for r,c,m in records]}
    (args.candidate/'scope-check.json').write_text(json.dumps(checks,indent=2)+'\n')
    print(json.dumps({'staged':args.stage,'changedPixels':[m['changedPixels'] for _,_,m in records]}))


if __name__ == '__main__':
    main()
