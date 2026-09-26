"""Offline joint source fit for repeated Joyride the/The word outlines.

Uses the existing source-fitted e plus explicit stem/shoulder/crossbar geometry
for h, t and T. The priors constrain topology; they do not identify a font or
establish exact typography. Nothing is staged or labelled as accepted.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.optimize import least_squares

from fit_loading_e_outline import sdf as e_sdf
from trace_overview_lettering import contours


WORDS=[('the',[282,117,298,127]),('The',[250,137,268,147]),
       ('the',[273,157,289,167]),('the',[239,177,255,187]),('the',[233,187,249,197])]
NAMES=['h_width','h_height','h_stroke','h_shoulder_top','h_inner_arch_height',
       't_width','t_height','t_stroke','t_stem_left','t_bar_top','t_bar_height','t_inner_bend',
       'T_width','T_height','T_stroke','t_advance','T_advance','h_advance','baseline']


def rect(x,y,left,top,right,bottom):
    return np.minimum.reduce([x-left,right-x,y-top,bottom-y])


def ellipse(x,y,cx,cy,rx,ry):
    return min(rx,ry)*(1-np.sqrt(((x-cx)/rx)**2+((y-cy)/ry)**2))


def h_sdf(x,y,p):
    w,h,s,top,inner_h=p[:5]
    radius=s+inner_h;cy=top+radius
    outside=np.maximum(ellipse(x,y,w/2,cy,w/2,radius),rect(x,y,0,cy,w,h))
    inside=np.maximum(ellipse(x,y,w/2,cy,w/2-s,inner_h),rect(x,y,s,cy,w-s,h+2))
    return np.minimum(h-y,np.maximum(np.minimum(outside,-inside),rect(x,y,0,0,s,h)))


def t_sdf(x,y,p):
    w,h,s,left,bar,bar_h,inner=p[5:12]
    radius=s+inner;cx=left+radius;cy=h-radius
    ring=np.minimum(ellipse(x,y,cx,cy,radius,radius),-ellipse(x,y,cx,cy,inner,inner))
    bend=np.minimum.reduce([ring,cx-x,y-cy])
    return np.maximum.reduce([rect(x,y,left,0,left+s,cy),
                              rect(x,y,0,bar,w,bar+bar_h),bend,
                              rect(x,y,cx,h-s,w,h)])


def word_alpha(x,y,p,e,capital=False,hard=False):
    baseline=p[18]
    if capital:
        w,h,s=p[12:15]
        top=y-(baseline-h)
        first=np.maximum(rect(x,top,0,0,w,s),rect(x,top,(w-s)/2,0,(w+s)/2,h))
        advance=p[16]
    else:
        first=t_sdf(x,y-(baseline-p[6]),p)
        advance=p[15]
    second=h_sdf(x-advance,y-(baseline-p[1]),p)
    third=e_sdf(x-advance-p[17],y-(baseline-e[1]),e)
    shape=np.maximum.reduce([first,second,third])
    if hard:
        return (shape>=0).astype(float)
    return 1/(1+np.exp(np.clip(-shape/.025,-60,60)))


def render_word(p,e,capital,translation,width,scale):
    # Boolean union at high sampling density prevents the fitting surrogate's
    # half-opacity seam along coincident interior edges of t's stem and bend.
    density=scale*4
    yy,xx=np.mgrid[:10*density,:width*density]
    field=word_alpha((xx+.5)/density-translation,(yy+.5)/density,p,e,capital,hard=True)
    return np.asarray(Image.fromarray(np.rint(field*255).astype(np.uint8)).resize(
        (width*scale,10*scale),Image.Resampling.BOX),dtype=float)/255


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--e-fit',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=Path(__file__).resolve().parents[1]/'reports/build12'
    emeta=json.loads(args.e_fit.read_text());e=emeta['parameters'][:6]
    source_path=root/'menu-original-tim/ISLAND1/OVERV3.png'
    if hashlib.sha256(source_path.read_bytes()).hexdigest()!=emeta['source']['pngSha256']:
        raise ValueError('Source changed')
    image=np.asarray(Image.open(source_path).convert('RGB'),dtype=float)
    target=np.zeros((5,10,18));weight=np.zeros_like(target)
    for i,(_,box) in enumerate(WORDS):
        x0,y0,x1,y1=box;width=x1-x0
        target[i,:,:width]=image[y0:y1,x0:x1,0];weight[i,:,:width]=1
    train=weight.copy();train[4]=0
    yy,xx=np.mgrid[:10,:18];sub=(np.arange(8)+.5)/8
    xgrid=np.broadcast_to(xx[...,None,None]+sub[None,None,:,None],(10,18,8,8))
    ygrid=np.broadcast_to(yy[...,None,None]+sub[None,None,None,:],(10,18,8,8))
    ink=emeta['ink']

    def predict(p):
        shapes=p[:19];dx=p[19:24];bg=p[24:29]
        return np.array([word_alpha(xgrid-dx[i],ygrid,shapes,e,text=='The').mean(axis=(2,3))*(ink-bg[i])+bg[i]
                         for i,(text,_) in enumerate(WORDS)])
    initial=[4.6,6.6,1.25,1.8,.5,3.3,6.2,1.15,.75,.9,1.,.3,
             5.9,6.5,1.3,3.8,5.8,5.5,7.6]+[1.,1.,1.25,1.4,1.5]+[8.]*5
    lower=[3.8,5.8,.8,.8,.15,2.7,5.2,.8,.2,.3,.65,.08,
           4.5,5.8,.8,3.,4.8,4.6,7.0]+[0.]*5+[0.]*5
    upper=[5.6,7.3,1.6,2.8,1.,4.,7.,1.5,1.2,1.8,1.5,.7,
           6.8,7.3,1.6,4.6,6.5,6.1,8.2]+[2.5]*5+[24.]*5
    def residual(p):
        # Weak source-e stroke consistency regularization prevents a very
        # different weight being hidden by original low-resolution sampling.
        penalty=np.array([p[2]-e[2],p[7]-e[2],p[14]-e[2]])*4
        return np.r_[((predict(p)-target)*train).ravel(),penalty]
    fit=least_squares(residual,initial,bounds=(lower,upper),x_scale='jac',max_nfev=350)
    p=fit.x
    def held(v):
        q=p.copy();q[23]=v[0];q[28]=v[1]
        return ((predict(q)[4]-target[4])*weight[4]).ravel()
    check=least_squares(held,[1.5,8],bounds=([0,0],[2.5,24]),max_nfev=100)
    p[23],p[28]=check.x
    prediction=predict(p)
    errors=[float(np.sqrt(np.sum(((prediction[i]-target[i])*weight[i])**2)/weight[i].sum())) for i in range(5)]
    args.output.mkdir(parents=True,exist_ok=False)
    for i,(text,box) in enumerate(WORDS):
        width=box[2]-box[0]
        comparison=np.concatenate([target[i,:,:width],prediction[i,:,:width]],axis=1)
        Image.fromarray(np.rint(comparison).clip(0,255).astype(np.uint8)).resize(
            (width*32,160),Image.Resampling.NEAREST).save(args.output/f'word-{i}-source-projection.png')
        alpha=render_word(p,e,text=='The',p[19+i],width,16)
        rgb=np.array([0,24,40])*(1-alpha[:,:,None])+ink*alpha[:,:,None]
        Image.fromarray(np.rint(rgb).astype(np.uint8)).save(args.output/f'word-{i}-outline-inspection.png')
        paths=contours(alpha,.5)
        svg=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} 10">',
             '<title>Unaccepted source-fitted loading word study</title>',
             '<path fill="#e8e8e8" fill-rule="evenodd" d="']
        for poly in paths:
            svg.append('M '+' L '.join(f'{x/16:.5f} {y/16:.5f}' for x,y in poly)+' Z')
        svg+=['"/>','</svg>']
        (args.output/f'word-{i}.svg').write_text('\n'.join(svg)+'\n',encoding='ascii')
    result={'source':emeta['source'],'eParameters':e,'ink':ink,'words':WORDS,
            'shapeParameterNames':NAMES,'shapeParameters':p[:19].tolist(),
            'translations':p[19:24].tolist(),'backgroundRed':p[24:29].tolist(),
            'trainingWords':[0,1,2,3],'heldOutWord':4,'sourceProjectionRmse':errors,
            'status':'offline joint word-shape hypothesis; not typography acceptance or staging'}
    (args.output/'manifest.json').write_text(json.dumps(result,indent=2)+'\n',encoding='ascii')
    print(json.dumps({'errors':errors,'shape':dict(zip(NAMES,p[:19])),'translations':p[19:24].tolist()}))


if __name__=='__main__':
    main()
