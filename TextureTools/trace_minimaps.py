"""Fit cubic vector contours to original minimaps and rasterize crisp 4x masks."""
import argparse
import hashlib
import json
import sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from scipy.interpolate import splprep, splev
from scipy.ndimage import label
sys.path.insert(0, str(Path(__file__).resolve().parents[1]/'.build/hud-authoring'))
from skimage.measure import find_contours, euler_number
from build_native_pack import Disc, records
from refine_hud_buoys import TRACKS

MAPS = {0xDACB,0xDACF,0xDAB3,0xDAE5,0xDAE9,0xDAED,0x224D,0x2256,0x2243,0xDA73}


def topology(mask):
    return (label(mask, np.ones((3,3)))[1],int(euler_number(mask,connectivity=2)))


def trace(original, error):
    a = np.array(original); mask = a[:,:,3] != 0
    paths = []
    for contour in find_contours(np.pad(mask.astype(float),1),.5,fully_connected='high'):
        xy = (contour-1+.5)[:,::-1]
        area = np.sum(xy[:,0]*np.roll(xy[:,1],-1)-np.roll(xy[:,0],-1)*xy[:,1])/2
        fit,_ = splprep(xy.T,s=len(xy)*error**2,per=True,k=min(3,len(xy)-2))
        points = np.array(splev(np.linspace(0,1,2049),fit)).T
        knots = np.unique(np.clip(fit[0],0,1))
        p0 = np.array(splev(0,fit)); commands = [f'M {p0[0]:.4f},{p0[1]:.4f}']
        for t0,t1 in zip(knots[:-1],knots[1:]):
            start,end = np.array(splev(t0,fit)),np.array(splev(t1,fit))
            c1 = start+np.array(splev(t0,fit,der=1))*(t1-t0)/3
            c2 = end-np.array(splev(t1,fit,der=1))*(t1-t0)/3
            commands.append('C '+ ' '.join(f'{v[0]:.4f},{v[1]:.4f}' for v in (c1,c2,end)))
        paths.append((area,points,' '.join(commands)+' Z'))
    paths.sort(key=lambda p:abs(p[0]),reverse=True)
    coverage = Image.new('L',(original.width*16,original.height*16))
    draw = ImageDraw.Draw(coverage)
    for area,points,_ in paths:
        draw.polygon([tuple(p*16) for p in points],fill=255 if area>0 else 0)
    coverage = np.array(coverage.resize((original.width*4,original.height*4),Image.Resampling.LANCZOS))
    occupied = coverage >= 128
    color = np.median(a[mask,:3],axis=0).astype('uint8')
    out = np.zeros((*occupied.shape,4),dtype='uint8')
    out[occupied,:3] = color
    # Preserve the original STP class. Coverage is a hard cutout, never blur.
    out[occupied,3] = 128
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {original.width} {original.height}">\n'
           f'<path fill="rgb({color[0]},{color[1]},{color[2]})" fill-rule="evenodd" d="'+
           ' '.join(p[2] for p in paths)+'"/>\n</svg>\n')
    return Image.fromarray(out),svg,topology(occupied),topology(mask)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--cue',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--stage',type=Path)
    args = parser.parse_args(); args.output.mkdir(parents=True,exist_ok=True)
    disc = Disc(args.cue); manifest = []
    for track in TRACKS:
        bank=f'{track}/{track}.TMS'; data=disc.read(bank)
        for meta,original in records(data):
            ident=int(meta['texture_id'],16)
            if ident not in MAPS: continue
            for error in (.30,.25,.20,.15,.10):
                result,svg,after,before=trace(original,error)
                if after==before: break
            assert after==before,(track,before,after)
            relative=f'{track}/{track}/{meta["ordinal"]:04d}-{ident:08X}.png'
            destination=args.output/relative;destination.parent.mkdir(parents=True,exist_ok=True)
            result.save(destination);destination.with_suffix('.svg').write_text(svg)
            if args.stage:
                target=args.stage/relative;target.parent.mkdir(parents=True,exist_ok=True)
                backup=args.output/'previous'/relative;backup.parent.mkdir(parents=True,exist_ok=True)
                if target.exists() and not backup.exists():backup.write_bytes(target.read_bytes())
                target.write_bytes(destination.read_bytes())
            # Opaque diagnostic preview; it does not alter the game asset.
            preview=Image.new('RGB',result.size,'#d0d8e0')
            preview.paste(result.convert('RGB'),mask=result.getchannel('A').point(lambda x:255 if x else 0))
            preview.save(args.output/f'{track}-preview.png')
            manifest.append(dict(track=track,output=relative,contourRmsTolerance=error,
                topology=after,sourceSha256=hashlib.sha256(data).hexdigest(),
                outputSha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
    assert len(manifest)==10
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(json.dumps(manifest,indent=2))


if __name__=='__main__':main()
