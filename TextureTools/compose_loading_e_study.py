"""Place five reviewed source-fitted e glyphs in the offline Joyride study.

This is a typography comparison, not a completed paragraph or runtime asset.
Unreviewed glyph instances are left untouched. Local background separation is
an authoring estimate and must be assessed in the full-screen visual review.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import grey_opening

from fit_loading_e_outline import alpha


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fit',type=Path,required=True)
    parser.add_argument('--preview',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[1]/'reports/build12'
    fit=json.loads(args.fit.read_text())
    source_path=root/'menu-original-tim/ISLAND1/OVERV3.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest()!=fit['source']['pngSha256']:
        raise ValueError('Original changed')
    source=np.asarray(Image.open(source_path).convert('RGB'),dtype=float)
    background=grey_opening(source,size=(3,3,1))
    p=fit['parameters']
    args.output.mkdir(parents=True,exist_ok=False)
    outputs=[]
    for stem in ['OVERV3L','OVERV3']:
        canvas=Image.open(args.preview/f'{stem}-map-study.png').convert('RGB')
        for i,g in enumerate(fit['examples']):
            x0,y0,x1,y1=g['bounds'];x1+=1
            h,w=y1-y0,x1-x0
            yy,xx=np.mgrid[:h*4,:w*4]
            coverage=alpha((xx+.5)/4-p[9+i],(yy+.5)/4-p[6],p)
            bg=np.asarray(Image.fromarray(np.rint(background[y0:y1,x0:x1]).astype(np.uint8)).resize(
                (w*4,h*4),Image.Resampling.LANCZOS),dtype=float)
            # Fit's red-channel gain + baseline estimates the original ink's
            # intensity. Neutral RGB is a hypothesis for this white-letter layer.
            ink=fit.get('ink',p[7]+p[8])
            composed=bg*(1-coverage[:,:,None])+ink*coverage[:,:,None]
            # Preserve untouched background and the neighboring h's antialias
            # fringe. Replacing the entire rectangle erased grid detail and
            # made a visible patch around otherwise isolated e occurrences.
            original=source[y0:y1,x0:x1]
            sy,sx=np.mgrid[:h,:w]
            old_ink=(original[:,:,0]>40)&(original.max(axis=2)-original.min(axis=2)<48)
            old_ink&=((sx+.5>=p[9+i]-.6)&(sx+.5<=p[9+i]+p[0]+.6)&
                      (sy+.5>=p[6]-.8)&(sy+.5<=p[6]+p[1]+.8))
            support=np.repeat(np.repeat(old_ink,4,axis=0),4,axis=1)|(coverage>.001)
            existing=np.asarray(canvas.crop((x0*4,y0*4,x1*4,y1*4)))
            composed[~support]=existing[~support]
            canvas.paste(Image.fromarray(np.rint(composed).clip(0,255).astype(np.uint8)),(x0*4,y0*4))
        path=args.output/f'{stem}-e-study.png'
        canvas.save(path)
        canvas.crop((876,380,1240,796)).save(args.output/f'{stem}-paragraph-inspection.png')
        outputs.append({'file':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
    report={'source':fit['source'],'glyphIds':[g['id'] for g in fit['examples']],
            'fit':str(args.fit),'outputs':outputs,
            'status':'offline comparison of five e glyphs; not paragraph completion or runtime acceptance',
            'limitations':'local background estimate and neutral ink color remain subject to visual review'}
    (args.output/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='ascii')


if __name__=='__main__':
    main()
