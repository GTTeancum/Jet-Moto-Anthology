"""Source-fitted vector digits and continuous buoy surfaces, without neural lettering."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import distance_transform_edt, gaussian_filter
from scipy.optimize import least_squares
from scipy.interpolate import CubicSpline
from build_native_pack import Disc, records
from refine_hud_buoys import TRACKS, BUOYS, HUD
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'.build/hud-authoring'))
from skimage.measure import find_contours, approximate_polygon

MAPS={0xDACB,0xDACF,0xDAB3,0xDAE5,0xDAE9,0xDAED,0x224D,0x2256,0x2243,0xDA73}


def vector_mask(mask,tolerance=.4):
    """Fit straight segments to source pixel contours, retaining counters/holes."""
    contours=find_contours(np.pad(mask.astype(float),1),.5)
    paths=[]
    for contour in contours:
        points=approximate_polygon(contour,tolerance)-1
        area=np.sum(points[:,1]*np.roll(points[:,0],-1)-np.roll(points[:,1],-1)*points[:,0])/2
        paths.append((area,points))
    paths.sort(key=lambda p:abs(p[0]),reverse=True)
    im=Image.new('L',(mask.shape[1]*16,mask.shape[0]*16))
    draw=ImageDraw.Draw(im)
    for area,points in paths:
        if len(points)<3:continue
        draw.polygon([((x+.5)*16,(y+.5)*16) for y,x in points],fill=255 if area>0 else 0)
    return np.array(im.resize((mask.shape[1]*4,mask.shape[0]*4),Image.Resampling.LANCZOS))


def reconstruct_letters(original):
    a=np.array(original); occupancy=vector_mask(a[:,:,3]>0)
    r,g,b=a[:,:,0].astype(float),a[:,:,1].astype(float),a[:,:,2].astype(float)
    magenta=vector_mask((r>40)&(g<r*.65)&(b>r*.55))
    orange=vector_mask((r>70)&(g>r*.25)&(g<r*.85)&(b<r*.55))
    rgb=np.full((*occupancy.shape,3),208.,dtype=float)
    y=np.linspace(0,1,len(rgb))[:,None,None]
    for coverage,top,bottom in [(magenta,[152,0,120],[80,0,64]),(orange,[248,176,8],[208,88,0])]:
        factor=(coverage/255)[:,:,None]
        rgb=rgb*(1-factor)+(np.array(top)*(1-y)+np.array(bottom)*y)*factor
    out=np.dstack([rgb.astype('uint8'),np.where(occupancy>=128,255,0).astype('uint8')])
    out[occupancy<128,:3]=0
    return Image.fromarray(out)


def reconstruct_warning(original):
    coverage=vector_mask(np.array(original)[:,:,3]>0,.45)
    distance=distance_transform_edt(coverage>=128)
    dy,dx=np.gradient(gaussian_filter(distance,.65))
    light=np.clip((-dx-dy)*.6,-1,1)
    bevel=np.maximum(0,1-distance/4)
    rgb=np.array([132,32,20])[None,None,:]+(light*bevel)[:,:,None]*np.array([115,75,60])
    out=np.dstack([np.clip(rgb,0,248).astype('uint8'),np.where(coverage>=128,255,0).astype('uint8')])
    out[coverage<128,:3]=0
    return Image.fromarray(out)


def reconstruct_map(original):
    a=np.array(original);coverage=vector_mask(a[:,:,3]>0,.6)
    visible=a[a[:,:,3]>0,:3]
    color=np.median(visible,axis=0).astype('uint8')
    rgba=np.empty((*coverage.shape,4),dtype='uint8');rgba[:,:,:3]=color
    rgba[:,:,3]=np.where(coverage>=96,255,0);rgba[coverage<96,:3]=0
    return Image.fromarray(rgba)

# Centerlines traced from the original F4AD condensed numeral cells. Coordinates
# remain in original atlas pixels. Stroke/border widths reproduce the source;
# these are not glyphs from an unrelated installed font.
DIGITS = {
    '0': [[(3,1.5),(5,1.5),(6.5,3),(6.5,9),(5,10.5),(3,10.5),(1.5,9),(1.5,3),(3,1.5)]],
    '1': [[(1.5,4),(4.5,1.5),(4.5,10.5)],[(2,10.5),(7,10.5)]],
    '2': [[(1.5,3),(2.8,1.5),(5.2,1.5),(6.5,3),(6.5,4.5),(1.5,10.5),(6.5,10.5)]],
    '3': [[(1.5,1.5),(5,1.5),(6.5,3),(6.5,4.5),(5,6),(3,6)],[(5,6),(6.5,7.5),(6.5,9),(5,10.5),(2.8,10.5),(1.5,9)]],
    '4': [[(6,10.5),(6,1.5),(1.5,7.5),(7,7.5)]],
    '5': [[(6.5,1.5),(1.5,1.5),(1.5,5.5),(5,5.5),(6.5,7),(6.5,9),(5,10.5),(3,10.5),(1.5,9)]],
    '6': [[(6.5,3),(5,1.5),(3,1.5),(1.5,3),(1.5,9),(3,10.5),(5,10.5),(6.5,9),(6.5,7),(5,5.5),(1.5,5.5)]],
    '7': [[(1.5,3),(1.5,1.5),(6.5,1.5),(6.5,3),(3,10.5)]],
    '8': [[(3,1.5),(5,1.5),(6.5,3),(6.5,4.5),(5,6),(3,6),(1.5,4.5),(1.5,3),(3,1.5)],[(3,6),(1.5,7.5),(1.5,9),(3,10.5),(5,10.5),(6.5,9),(6.5,7.5),(5,6)]],
    '9': [[(6.5,6),(3,6),(1.5,4.5),(1.5,3),(3,1.5),(5,1.5),(6.5,3),(6.5,9),(5,10.5),(3,10.5),(1.5,9)]],
}


def vector_digits(result):
    scale = 32
    # Render above output resolution, then resolve coverage once. The native
    # cutout sampler handles the final boundary without a low-resolution mask.
    border = Image.new('L', (128*scale, 13*scale))
    ink = border.copy()
    for index, paths in enumerate(DIGITS.values()):
        for path in paths:
            xy = [((x+index*10+.5)*scale, (y+.5)*scale) for x,y in path]
            for mask, width in ((border, 3.4), (ink, 1.65)):
                draw = ImageDraw.Draw(mask)
                draw.line(xy, fill=255, width=int(width*scale), joint='curve')
                # Pillow's line joints do not join a closed path's first/last
                # vertex, and butt caps expose the ink at open stroke ends.
                # Explicit round joins/caps keep the silver outline continuous.
                radius = width*scale/2
                for x, y in xy:
                    draw.ellipse((x-radius,y-radius,x+radius,y+radius),fill=255)
    b = np.array(border.resize((512,52),Image.Resampling.LANCZOS))/255
    k = np.array(ink.resize((512,52),Image.Resampling.LANCZOS))/255
    color = np.empty((52,512,4),dtype=np.uint8)
    # Original magenta vertical shading, rather than a flat substitute color.
    y = np.linspace(0,1,52)[:,None,None]
    pigment = np.array([152,0,120])*(1-y)+np.array([80,0,64])*y
    ratio = np.minimum(1,k/np.maximum(b,1e-6))[:,:,None]
    color[:,:,:3] = np.clip(208*(1-ratio)+pigment*ratio,0,255)
    color[:,:,3] = np.where(b>=.5,255,0)
    color[b<.5,:3]=0
    # Keep slash, colon and plus as separate, original source crops.
    result.paste(Image.fromarray(color).crop((0,0,400,52)),(0,80))
    return result


def source_contours(original):
    a=np.array(original); occupied=a[:,:,3]>0; size=(a.shape[1]*4,a.shape[0]*4)
    iy,ix=distance_transform_edt(~occupied,return_distances=False,return_indices=True)
    rgb=Image.fromarray(a[iy,ix,:3]).resize(size,Image.Resampling.BICUBIC)
    mask=np.array(Image.fromarray(occupied.astype('uint8')*255).resize(size,Image.Resampling.BICUBIC))>=128
    classes=np.array(Image.fromarray(a[iy,ix,3]).resize(size,Image.Resampling.NEAREST))
    out=np.dstack([np.array(rgb),np.where(mask,classes,0).astype('uint8')]);out[~mask,:3]=0
    return Image.fromarray(out)


def buoy_surface(original):
    a=np.array(original); occupied=a[:,:,3]>0; h,w=a.shape[:2]
    points=[]
    for y in range(2,30):
        xs=np.flatnonzero(occupied[y]);points += [(xs[0]-.5,y),(xs[-1]+.5,y)]
    p=np.array(points)
    cx,cy,c=np.linalg.lstsq(np.c_[2*p[:,0],2*p[:,1],np.ones(len(p))],(p*p).sum(1),rcond=None)[0]
    radius=np.sqrt(c+cx*cx+cy*cy)
    yy,xx=np.mgrid[:h,:w]; valid=occupied & (yy<33) & (((xx-cx)**2+(yy-cy)**2)<(radius-1)**2)
    xy=np.c_[xx[valid],yy[valid]]; target=a[valid,:3]/255
    def basis(xy,q):
        nx=(xy[:,0]-cx)/radius; ny=(xy[:,1]-cy)/radius
        nz=np.sqrt(np.maximum(0,1-nx*nx-ny*ny))
        sx=(xy[:,0]-q[0])/q[2];sy=(xy[:,1]-q[1])/q[3]
        spec=np.exp(-.5*(sx*sx+sy*sy))
        return np.c_[np.ones(len(xy)),nx,ny,nz,nx*ny,nz*nz,spec,spec*spec]
    def residual(q):
        b=basis(xy,q); coefficients=np.linalg.lstsq(b,target,rcond=None)[0]
        return (b@coefficients-target).ravel()
    q=least_squares(residual,[16,10,8,8],bounds=([5,0,3,3],[30,25,20,20]),max_nfev=70).x
    coefficients=np.linalg.lstsq(basis(xy,q),target,rcond=None)[0]
    # The original 5-bit image clips the sun highlight. Fit the clipped lighting
    # response, otherwise linear regression turns its white plateau into a dull
    # yellow spot and loses the original gloss.
    def clipped_residual(parameters):
        return (np.clip(basis(xy,parameters[:4])@parameters[4:].reshape(8,3),0,248/255)-target).ravel()
    parameters=least_squares(clipped_residual,np.r_[q,coefficients.ravel()],
        max_nfev=120,ftol=1e-6,xtol=1e-6).x
    q=parameters[:4];coefficients=parameters[4:].reshape(8,3)
    y,x=np.mgrid[:h*4,:w*4]/4-.375
    rgb=np.clip(basis(np.c_[x.ravel(),y.ravel()],q)@coefficients*255,0,248).reshape(h*4,w*4,3)
    # Original submerged reflection remains source-derived with continuous
    # color interpolation and a smoothed source contour, retaining its extent.
    reflected=np.array(source_contours(original))
    # Remove original palette dithering in the reflected surface, which would
    # otherwise remain a magnified checker pattern beneath the clean sphere.
    reflected[:,:,:3]=np.clip(gaussian_filter(reflected[:,:,:3].astype(float),sigma=(3,3,0)),0,248)
    waterline=35*4
    rgb[waterline:]=reflected[waterline:,:,:3]
    mask=(x-cx)**2+(y-cy)**2<=radius*radius
    alpha=np.array(original.getchannel('A').resize((w*4,h*4),Image.Resampling.NEAREST))
    # Newly reconstructed opaque upper rim must never borrow reflection STP.
    alpha[:waterline]=255
    alpha=np.where(mask,np.where(alpha==0,255,alpha),0).astype('uint8')
    rgba=np.dstack([rgb.astype('uint8'),alpha]);rgba[~mask,:3]=0
    return Image.fromarray(rgba),dict(center=[float(cx),float(cy)],radius=float(radius),
        highlight=q.tolist(),sourceFitRmse=float(np.sqrt(np.mean(clipped_residual(parameters)**2))*255))


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cue',type=Path,required=True);p.add_argument('--pack',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True);args=p.parse_args()
    args.output.mkdir(parents=True,exist_ok=False);disc=Disc(args.cue);manifest=[]
    for track in TRACKS:
        bank=f'{track}/{track}.TMS';data=disc.read(bank)
        for m,original in records(data):
            ident=int(m['texture_id'],16)
            if ident not in BUOYS | MAPS and ident!=0xF4AD:continue
            rel=f'{track}/{track}/{m["ordinal"]:04d}-{ident:08X}.png'
            fitting=None
            if ident in BUOYS:out,fitting=buoy_surface(original)
            elif ident in MAPS:out=reconstruct_map(original)
            else:
                out=Image.open(args.pack/rel).convert('RGBA')
                restored=source_contours(original)
                out.paste(restored.crop((0,0,512,256)),(0,0))
                out.paste(reconstruct_warning(original.crop((0,0,128,16))),(0,0))
                out.paste(reconstruct_letters(original.crop((0,34,128,64))),(0,136))
                # Preserve dashboard surface, but remove damaged neural labels
                # using the same source numeral patch at its original location.
                out.paste(restored.crop((60,280,336,364)),(60,280))
                out=vector_digits(out)
            dest=args.output/rel;dest.parent.mkdir(parents=True,exist_ok=True);out.save(dest)
            manifest.append(dict(source=bank,ordinal=m['ordinal'],id=m['texture_id'],output=rel,
                sourceSha256=hashlib.sha256(data).hexdigest(),outputSha256=hashlib.sha256(dest.read_bytes()).hexdigest(),fit=fitting))
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(f'Reconstructed {len(manifest)} source-owned assets')


if __name__=='__main__':main()
