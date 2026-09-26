"""Full-screen Joyride authoring preview with source-guided title outlines.

Only the title is reconstructed. The original map, paragraph and footer remain
source-resolution content enlarged for context. Never stages runtime assets or
labels this limited authoring preview as a completed loading upscale.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt, map_coordinates

from trace_loading_joyride_title import PATHS, points


def verified(path):
    meta = json.loads(Path(str(path)+'.json').read_text())
    if hashlib.sha256(path.read_bytes()).hexdigest() != meta['pngSha256']:
        raise ValueError(f'Source changed: {path}')
    return meta, np.asarray(Image.open(path).convert('RGB'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]/'reports/build12'
    fit = json.loads((root/'menu-loading-native-title-study/manifest.json').read_text())
    paths = sorted((root/'menu-original-tim').glob('*/OVERV[0-9].png'))
    sources = [verified(path) for path in paths]
    if len(sources) != 10:
        raise ValueError('Expected ten original sources for the background study')
    stack = np.stack([a[17:33,128:194] for _,a in sources])
    eligible = ((stack[:,:,:,0]<=40)&(stack[:,:,:,2]>=stack[:,:,:,1])&
                (stack[:,:,:,1]>=stack[:,:,:,0]))
    background = np.zeros((16,66,3),dtype=np.uint8)
    missing = ~eligible.any(axis=0)
    choices = []
    for y in range(16):
        for x in range(66):
            if missing[y,x]:
                continue
            colors,counts = np.unique(stack[:,y,x][eligible[:,y,x]],axis=0,return_counts=True)
            # Exact repeated samples first; lower red breaks ties to avoid
            # selecting gray text-antialias contamination over blue backdrop.
            selected = min(range(len(colors)),key=lambda i:(-counts[i],int(colors[i,0]),int(colors[i].sum())))
            color = colors[selected]
            background[y,x] = color
            names = [meta['source'] for i,(meta,_) in enumerate(sources)
                     if eligible[i,y,x] and np.array_equal(stack[i,y,x],color)]
            choices.append({'x':x+128,'y':y+17,'rgb':color.tolist(),'sources':names})
    # Eleven covered positions have no eligible sample. Keep this uncertainty
    # explicit; nearest-sample fill is an authoring placeholder, not recovered
    # original artwork. Full-screen review must account for those positions.
    indices = distance_transform_edt(missing,return_distances=False,return_indices=True)
    background[missing] = background[tuple(indices[:,missing])]
    mask = Image.new('L',(71*64,15*64))
    draw = ImageDraw.Draw(mask)
    for glyph in PATHS:
        for i,path in enumerate(glyph):
            draw.polygon([tuple(p*64) for p in points(path)],fill=255 if i==0 else 0)
    x0,y0,sx,sy,*_ = fit['parameters']
    yy,xx = np.mgrid[:64,:264]
    alpha = map_coordinates(np.asarray(mask,dtype=float)/255,
                            [((17+(yy+.5)/4-y0)/sy)*64-.5,
                             ((128+(xx+.5)/4-x0)/sx)*64-.5],order=1,mode='constant')
    args.output.mkdir(parents=True,exist_ok=False)
    results = []
    for name in ('OVERV3','OVERV3L'):
        meta,source = verified(root/f'menu-original-tim/ISLAND1/{name}.png')
        base = Image.fromarray(source).resize((1280,960),Image.Resampling.LANCZOS)
        crop = source[17:33,128:194].astype(int)
        # Replace only neutral, bright title samples; retain colored map art
        # that enters the edge of the study rectangle.
        ink = ((crop[:,:,0]>32)&(crop.max(axis=2)-crop.min(axis=2)<=32))
        # Native title samples occupy cap rows 20..28; only J descends below
        # them. The resort outline enters the lower-right part of this crop.
        # A color-only mask incorrectly removed that real map geometry.
        py,px = np.mgrid[17:33,128:194]
        title_bounds = ((py>=20)&(py<29)&(px>=131)&(px<190)) | (
                        (py>=29)&(py<32)&(px>=131)&(px<139))
        ink &= title_bounds
        clean = crop.copy()
        clean[ink] = background[ink]
        clean = np.asarray(Image.fromarray(clean.astype(np.uint8)).resize((264,64),Image.Resampling.LANCZOS),dtype=float)
        rgb = clean*(1-alpha[:,:,None])+216*alpha[:,:,None]
        replacement = np.rint(rgb).clip(0,255).astype(np.uint8)
        # Outside the actual erase/draw support retain the original full-frame
        # interpolation, avoiding seams from independently resizing this crop.
        support = Image.fromarray((ink*255).astype(np.uint8)).resize((264,64),Image.Resampling.LANCZOS)
        support = (np.asarray(support)>0) | (alpha>0)
        untouched = np.array(base.crop((512,68,776,132)))
        replacement[~support] = untouched[~support]
        base.paste(Image.fromarray(replacement),(512,68))
        out = args.output/f'{name}-title-preview.png'
        base.save(out)
        results.append({'source':meta,'file':out.name,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()})
    uy,ux = np.where(missing)
    manifest = {'status':'offline full-screen authoring preview, TITLE ONLY; not a finished loading upscale',
                'unreconstructed':['paragraph','map and labels','legend','Loading/Continue prompt'],
                'titleFit':fit,'results':results,
                'backgroundSamples':choices,
                'backgroundPlaceholders':[[int(x+128),int(y+17)] for y,x in zip(uy,ux)]}
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='ascii')
    print(json.dumps({'files':[r['file'] for r in results],'backgroundPlaceholders':manifest['backgroundPlaceholders']}))


if __name__ == '__main__':
    main()
