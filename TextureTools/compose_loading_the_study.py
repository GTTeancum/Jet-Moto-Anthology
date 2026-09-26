"""Place the five joint source-fitted the/The outlines in offline Joyride pages.

Only these five words change. This is not a finished paragraph or runtime pack.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import grey_opening

from fit_loading_the_words import render_word


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fit',type=Path,required=True)
    parser.add_argument('--preview',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    fit=json.loads(args.fit.read_text())
    root=Path(__file__).resolve().parents[1]/'reports/build12'
    source_path=root/'menu-original-tim/ISLAND1/OVERV3.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest()!=fit['source']['pngSha256']:
        raise ValueError('Original changed')
    source=np.asarray(Image.open(source_path).convert('RGB'),dtype=float)
    background=grey_opening(source,size=(3,3,1))
    args.output.mkdir(parents=True,exist_ok=False)
    outputs=[];scope=[];deltas=[]
    for stem in ['OVERV3L','OVERV3']:
        canvas=Image.open(args.preview/f'{stem}-map-study.png').convert('RGB')
        before=np.asarray(canvas,dtype=np.int16)
        allowed=np.zeros((960,1280),dtype=bool)
        for i,(text,box) in enumerate(fit['words']):
            x0,y0,x1,y1=box;h,w=y1-y0,x1-x0
            coverage=render_word(fit['shapeParameters'],fit['eParameters'],text=='The',
                                 fit['translations'][i],w,4)
            bg=np.asarray(Image.fromarray(np.rint(background[y0:y1,x0:x1]).astype(np.uint8)).resize(
                (w*4,h*4),Image.Resampling.LANCZOS),dtype=float)
            rgb=bg*(1-coverage[:,:,None])+fit['ink']*coverage[:,:,None]
            original=source[y0:y1,x0:x1]
            old_ink=(original[:,:,0]>40)&(original.max(axis=2)-original.min(axis=2)<48)
            support=np.repeat(np.repeat(old_ink,4,axis=0),4,axis=1)|(coverage>.001)
            existing=np.asarray(canvas.crop((x0*4,y0*4,x1*4,y1*4)))
            rgb[~support]=existing[~support]
            canvas.paste(Image.fromarray(np.rint(rgb).clip(0,255).astype(np.uint8)),(x0*4,y0*4))
            allowed[y0*4:y1*4,x0*4:x1*4]=True
        path=args.output/f'{stem}-the-study.png';canvas.save(path)
        canvas.crop((876,380,1240,796)).save(args.output/f'{stem}-paragraph-inspection.png')
        after=np.asarray(canvas,dtype=np.int16);change=np.any(after!=before,axis=2)
        scope.append({'variant':stem,'changedPixels':int(change.sum()),
                      'outsideFiveWords':int((change&~allowed).sum())})
        deltas.append(after-before)
        outputs.append({'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    report={'source':fit['source'],'words':fit['words'],'fit':str(args.fit),'outputs':outputs,
            'scope':scope,'variantDeltaPreserved':bool(np.array_equal(*deltas)),
            'status':'offline typography study of five words; not complete or staged',
            'limitations':'source-guided geometric priors and local background separation remain unaccepted'}
    (args.output/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='ascii')
    print(json.dumps({'scope':scope,'variantDeltaPreserved':report['variantDeltaPreserved']}))


if __name__=='__main__':
    main()
