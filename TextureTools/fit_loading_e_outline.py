"""Offline source-guided e outline fit from repeated original Joyride glyphs.

Fits a connected bowl, counter and crossbar to the source samples. The shape
prior encodes the observed letter topology, not a system-font outline. This is
an authoring hypothesis, not proof of the exact original outline or an upgrade.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.optimize import least_squares

from trace_overview_lettering import contours


IDS = [41,64,98,117,128]


def sdf(x,y,p):
    w,h,stroke,bar,opening,inner_dx = p[:6]
    cx,cy=w/2,h/2
    outer = min(cx,cy)*(1-np.sqrt(((x-cx)/cx)**2+((y-cy)/cy)**2))
    rx,ry=cx-stroke,cy-stroke
    inner = min(rx,ry)*(1-np.sqrt(((x-cx-inner_dx)/rx)**2+((y-cy)/ry)**2))
    ring=np.minimum(outer,-inner)
    top=bar*h
    bottom=top+stroke*.85
    crossbar=np.minimum.reduce([x,w-x,y-top,bottom-y])
    shape=np.minimum(outer,np.maximum(ring,crossbar))
    mouth=np.minimum.reduce([x-cx,y-bottom,h*opening-y,w+1-x])
    return np.minimum(shape,-mouth)


def alpha(x,y,p):
    return 1/(1+np.exp(np.clip(-sdf(x,y,p)/.025,-60,60)))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--measurements',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--ink',type=float,help='Optional source-palette foreground intensity constraint')
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[1]/'reports/build12'
    doc=json.loads(args.measurements.read_text())
    path=root/'menu-original-tim/ISLAND1/OVERV3.png'
    if hashlib.sha256(path.read_bytes()).hexdigest()!=doc['source']['pngSha256']:
        raise ValueError('Original source changed')
    source=np.asarray(Image.open(path).convert('RGB'),dtype=float)
    chosen=[doc['glyphs'][i] for i in IDS]
    target=np.zeros((5,10,8));weight=np.zeros_like(target)
    for i,g in enumerate(chosen):
        if g['character']!='e': raise ValueError('Unreviewed glyph annotation')
        x0,y0,x1,y1=g['bounds'];width=x1-x0
        target[i,:,:width]=source[y0:y1,x0:x1,0]
        weight[i,:,:width]=1
    # Last occurrence is withheld from shape fitting. Only its translation is
    # fitted afterward, to assess transfer across original sampling phases.
    train=weight.copy();train[4]=0
    yy,xx=np.mgrid[:10,:8]
    sub=(np.arange(8)+.5)/8
    xgrid=np.broadcast_to(xx[...,None,None]+sub[None,None,:,None],(10,8,8,8))
    ygrid=np.broadcast_to(yy[...,None,None]+sub[None,None,None,:],(10,8,8,8))

    def prediction(p):
        shape=p[:6];y0,gain,bg=p[6:9];dx=p[9:]
        foreground = args.ink if args.ink is not None else gain+bg
        return np.array([alpha(xgrid-d,ygrid-y0,shape).mean(axis=(2,3))*(foreground-bg)+bg for d in dx])
    start=[4.6,4.9,1.05,.40,.82,.10,2.2,224,8,.3,.3,.3,.3,.3]
    lower=[3.7,4.2,.65,.25,.68,-.4,1.5,190,0,-1,-1,-1,-1,-1]
    upper=[5.8,5.8,1.55,.55,.97,.4,3.0,248,24,2,2,2,2,2]
    fit=least_squares(lambda p:((prediction(p)-target)*train).ravel(),start,
                      bounds=(lower,upper),x_scale='jac',max_nfev=400)
    params=fit.x
    def heldout(dx):
        p=params.copy();p[-1]=dx[0]
        return ((prediction(p)[4]-target[4])*weight[4]).ravel()
    validation=least_squares(heldout,[.3],bounds=([-1],[2]),max_nfev=80)
    params[-1]=validation.x[0]
    if args.ink is not None:
        # This slot is inactive during a constrained fit; export the effective
        # gain so the parameter vector remains interpretable with its baseline.
        params[7]=args.ink-params[8]
    predicted=prediction(params)
    errors=[float(np.sqrt(np.sum(((predicted[i]-target[i])*weight[i])**2)/weight[i].sum())) for i in range(5)]
    args.output.mkdir(parents=True,exist_ok=False)
    # Individual source/projection comparisons, followed by a large outline
    # diagnostic. Black padding is outside each measured glyph's support.
    for i,g in enumerate(chosen):
        actual=np.repeat(target[i,:,:,None],3,axis=2)
        project=np.repeat(predicted[i,:,:,None],3,axis=2)
        pair=np.concatenate([actual,project],axis=1)
        Image.fromarray(np.rint(pair).clip(0,255).astype(np.uint8)).resize(
            (256,160),Image.Resampling.NEAREST).save(args.output/f'example-{g["id"]}-source-projection.png')
    oy,ox=np.mgrid[:160,:128]
    a=alpha((ox+.5)/16-1,(oy+.5)/16-1,params)
    ink=args.ink if args.ink is not None else params[7]+params[8]
    rgb=np.array([0,24,40])*(1-a[:,:,None])+ink*a[:,:,None]
    Image.fromarray(np.rint(rgb).clip(0,255).astype(np.uint8)).save(args.output/'e-outline-inspection.png')
    polys=contours(a,.5)
    svg=['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 10">',
         '<title>Unaccepted source-fitted e outline study</title>',
         '<path fill="#d8d8d8" fill-rule="evenodd" d="']
    for poly in polys:
        svg.append('M '+' L '.join(f'{x/16:.5f} {y/16:.5f}' for x,y in poly)+' Z')
    svg+=['"/>','</svg>']
    (args.output/'e-outline.svg').write_text('\n'.join(svg)+'\n',encoding='ascii')
    result={'source':doc['source'],'examples':chosen,'trainingIds':IDS[:4],'heldOutId':IDS[4],
            'parameters':params.tolist(),'ink':float(ink),'inkConstrained':args.ink is not None,
            'sourceProjectionRmse':errors,
            'status':'offline shape hypothesis; not accepted or staged',
            'limitations':'ellipse/crossbar prior estimates unsampled curves; source fit is not fidelity proof'}
    (args.output/'manifest.json').write_text(json.dumps(result,indent=2)+'\n',encoding='ascii')
    print(json.dumps({'parameters':params.tolist(),'errors':errors}))


if __name__=='__main__':
    main()
