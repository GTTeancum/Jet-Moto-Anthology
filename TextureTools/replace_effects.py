#!/usr/bin/env python3
"""Author new, source-color-matched particle sprites; this is NOT image upscaling.

Inputs: the original 1x PNG tree exported with build_native_pack.py and a complete
Build08-or-later native pack. Output must be a new directory. Only the 15 verified
spray/roost images change. Original geometry, animation, texture UVs, shadows,
material IDs, source disc, rider art and other textures are not modified.
Requires Python 3.10+, Pillow, NumPy and SciPy for OFFLINE authoring only.
"""
from __future__ import annotations
import argparse, collections, hashlib, json, math, shutil
from pathlib import Path
import numpy as np
from scipy.ndimage import gaussian_filter, distance_transform_edt
from PIL import Image, ImageDraw

SCALE=4
FAMILIES={
 '0000EF77': ('water', 9041),
 '0000C6A1': ('sand', 9013),
 '0000C65D': ('soil', 9027),
 '00008F87': ('mud', 9059),
 '00003D31': ('snow', 9067),
}
ALLOW={
 ('ISLAND1/ISLAND1.TMS',62,'0000EF77'),('ISLAND2/ISLAND2.TMS',79,'0000EF77'),
 ('ISLAND3/ISLAND3.TMS',53,'0000EF77'),('SWAMP1/SWAMP1.TMS',73,'0000EF77'),
 ('SWAMP2/SWAMP2.TMS',65,'0000EF77'),('SWAMP3/SWAMP3.TMS',67,'0000EF77'),
 ('ISLAND1/ISLAND1.TMS',30,'0000C6A1'),('ISLAND3/ISLAND3.TMS',27,'0000C6A1'),
 ('ISLAND2/ISLAND2.TMS',78,'0000C65D'),('SWAMP1/SWAMP1.TMS',72,'00008F87'),
 ('SWAMP2/SWAMP2.TMS',64,'00008F87'),('SWAMP3/SWAMP3.TMS',66,'00008F87'),
 ('ALPINE1/ALPINE1.TMS',49,'00003D31'),('ALPINE2/ALPINE2.TMS',39,'00003D31'),
 ('ALPINE3/ALPINE3.TMS',42,'00003D31'),
}
def digest(b):return hashlib.sha256(b).hexdigest()
def ramp(a,lo,hi):
 x=np.clip((a-lo)/(hi-lo),0,1);return x*x*(3-2*x)
def noise(rng,n):
 a=np.zeros((n,n),np.float64)
 for size,weight in [(7,1.0),(17,.6),(41,.34),(101,.17)]:
  raw=rng.random((size,size)).astype(np.float32)
  layer=np.asarray(Image.fromarray(raw).resize((n,n),Image.Resampling.BICUBIC),np.float64)
  a+=layer*weight
 return np.clip((a-.45)/1.3,0,1)
def palette(source):
 a=np.asarray(source.convert('RGBA'));p=a[a[:,:,3]>0,:3].astype(float)
 if len(p)<8:raise ValueError('Particle source has insufficient visible color.')
 lum=p@np.array([.2126,.7152,.0722]);order=np.argsort(lum);p=p[order]
 # Robust, original-color anchors. No invented cyan water or orange dust.
 anchors=np.stack([np.median(p[int(len(p)*lo):max(int(len(p)*hi),int(len(p)*lo)+1)],axis=0)
  for lo,hi in [(.12,.30),(.42,.62),(.82,.99)]])
 return anchors

def author(source:Image.Image,family:str,seed:int)->tuple[Image.Image,dict]:
 if source.size!=(64,64):raise ValueError('Verified sprites are 64 x 64.')
 n=512;rng=np.random.default_rng(seed);rgbp=palette(source)
 y,x=np.mgrid[0:n,0:n].astype(float)/(n-1);flow=noise(rng,n)
 border=ramp(x,.025,.07)*ramp(1-x,.025,.07)*ramp(y,.025,.075)*ramp(1-y,.045,.095)
 def color(t):
  t=np.clip(t,0,1);q=np.minimum((t*2).astype(int),1) if isinstance(t,np.ndarray) else min(int(t*2),1)
  f=t*2-q
  return rgbp[q]*(1-f[...,None])+rgbp[q+1]*f[...,None] if isinstance(t,np.ndarray) else rgbp[q]*(1-f)+rgbp[q+1]*f
 if family in ('water','snow','mud'):
  width=.13+.33*(1-y)**.60
  fan=np.exp(-((x-.5)/width)**2*2.0)*ramp(y,.03,.23)*ramp(1-y,.075,.25)
  density={'water':.72,'snow':.30,'mud':.075}[family]
  alpha=np.clip(fan*(.18+flow**2)*density*border,0,1)
  tones=.38+.42*flow+.11*(1-y)
 else:
  alpha=np.zeros((n,n));tones=.37+.42*flow
  for i,base in enumerate([.19,.44,.70]):
   center=base+.21*((x-.5)/.5)**2+.016*np.sin(x*22+i*3)
   wav=.010*np.sin(x*49+i)+.007*np.sin(x*89+i*1.7)
   a=np.exp(-((y-center-wav)/(.026+.036*flow))**2)
   alpha+=a*(.22+.66*flow**1.2)*(.90 if family=='soil' else 1)
  alpha=np.clip(alpha*border,0,.79)
 base=np.zeros((n,n,4),np.uint8);base[:,:,:3]=np.rint(color(tones)).astype(np.uint8);base[:,:,3]=np.rint(alpha*255).astype(np.uint8)
 image=Image.fromarray(base);layer=Image.new('RGBA',(n,n));draw=ImageDraw.Draw(layer)
 def fill(t,a):return tuple(np.rint(color(float(t))).astype(int))+(int(np.clip(a,0,1)*255),)
 def dot(px,py,r,t,a,elong=1,angle=0,clod=False):
  # The sprite remains within its verified native UV domain and does not touch edges.
  if not(.05<px<.95 and .055<py<.915):return
  xx,yy=px*n,py*n;r=float(r);ct,st=math.cos(angle),math.sin(angle)
  steps=7 if clod else 10;points=[]
  for k in range(steps):
   th=k*math.tau/steps;rr=r*(rng.uniform(.65,1.22) if clod else 1)
   a0=math.cos(th)*rr*elong;b0=math.sin(th)*rr
   points.append((xx+a0*ct-b0*st,yy+a0*st+b0*ct))
  draw.polygon(points,fill=fill(t,a))
  if clod and r>1.7:
   highlight=[(xx-.4*r,yy-.4*r),(xx+.5*r,yy-.6*r),(xx+.2*r,yy-.1*r)]
   draw.polygon(highlight,fill=fill(min(t+.25,1),a*.85))
 if family=='water':
  # Broken, curved jets, not evenly spaced straight spokes.
  for j in range(46):
   endpoint=np.array([rng.uniform(.10,.90),rng.uniform(.08,.56)])
   origin=np.array([rng.uniform(.455,.555),rng.uniform(.75,.89)])
   bend=np.array([endpoint[0]+rng.uniform(-.07,.07),rng.uniform(.38,.70)])
   tt=np.linspace(rng.uniform(.05,.20),rng.uniform(.68,.96),26)
   pts=[tuple(((1-t)**2*origin+2*(1-t)*t*bend+t*t*endpoint)*n) for t in tt]
   draw.line(pts,fill=fill(rng.uniform(.50,.99),rng.uniform(.28,.65)),width=int(rng.integers(2,5)))
  for j in range(940):
   t=rng.uniform(.06,.91);vx=rng.normal(0,.34)
   px=.5+vx*t;py=.87-1.20*t+.32*t*t+rng.normal(0,.016)
   r=rng.uniform(.75,2.7)*(1.20-.48*t)
   dot(px,py,r,rng.uniform(.54,1),rng.uniform(.27,.88),rng.uniform(1,2.6),math.atan2(-1.2+.64*t,vx))
  # Aerated foam with small transparent gaps at the spray origin.
  for j in range(360):
   px=rng.normal(.505,.093);py=rng.normal(.725,.097)
   dot(px,py,rng.uniform(1.2,4.3),rng.uniform(.40,.94),rng.uniform(.30,.77),rng.uniform(1,1.8),rng.uniform(0,math.tau))
 elif family in ('sand','soil'):
  for j in range(1260 if family=='sand' else 870):
   px=rng.uniform(.05,.95);i=int(rng.integers(0,3));center=[.19,.44,.70][i]+.21*((px-.5)/.5)**2+.016*math.sin(px*22+i*3)
   py=center+rng.normal(0,.025 if family=='sand' else .035)
   dot(px,py,rng.uniform(.75,2.6 if family=='sand' else 3.4),rng.uniform(.38,1),rng.uniform(.22,.78),rng.uniform(1,2.4),math.atan(1.68*(px-.5)),family=='soil')
  for j in range(95):
   px=rng.uniform(.12,.88);i=int(rng.integers(0,3));py=[.19,.44,.70][i]+.21*((px-.5)/.5)**2+rng.normal(0,.035)
   le=rng.uniform(.008,.035);draw.line([(px*n,py*n),((px+le)*n,(py+le*(px-.5)*1.2)*n)],fill=fill(rng.uniform(.55,.90),rng.uniform(.15,.38)),width=1)
 elif family=='mud':
  for j in range(520):
   px=rng.uniform(.07,.93);py=rng.uniform(.08,.90)
   # Larger wet clods stay toward the source, with separated fine thrown droplets.
   r=rng.uniform(1.3,5.8)*(0.72+.48*py)
   dot(px,py,r,rng.uniform(.16,.78),rng.uniform(.50,.96),rng.uniform(1,1.7),rng.uniform(0,math.tau),True)
  for j in range(650):
   px=rng.uniform(.06,.94);py=rng.uniform(.08,.90)
   dot(px,py,rng.uniform(.40,1.0),rng.uniform(.35,.88),rng.uniform(.24,.58),rng.uniform(1,2.4),-math.pi*.35)
 elif family=='snow':
  for j in range(1650):
   t=rng.uniform(.08,.93);px=.5+rng.normal(0,.30)*(.4+.8*t);py=.90-.91*t+rng.normal(0,.06)
   dot(px,py,rng.uniform(.7,2.45),rng.uniform(.30,1),rng.uniform(.25,.85),rng.uniform(1,1.8),rng.uniform(0,math.tau),False)
 else:raise ValueError(family)
 image=Image.alpha_composite(image,layer)
 # Premultiplied resizing gives coverage-correct downsampling, not dark RGB fringes.
 image=image.resize((256,256),Image.Resampling.LANCZOS)
 a=np.array(image);edge=(np.minimum.reduce(np.broadcast_arrays(np.arange(256)[None,:],255-np.arange(256)[None,:],np.arange(256)[:,None],255-np.arange(256)[:,None]))<6)
 a[edge,3]=0;a[a[:,:,3]<2,3]=0
 valid=a[:,:,3]>0
 if not np.any(valid):raise ValueError('Empty authored particle')
 nearest=distance_transform_edt(~valid,return_distances=False,return_indices=True)
 a[~valid,:3]=a[nearest[0][~valid],nearest[1][~valid],:3]
 return Image.fromarray(a),{'source_palette_anchors':rgbp.tolist(),'seed':seed,'internal_size':512,'output_size':256,
  'nonzero_coverage_pixels':int(np.count_nonzero(a[:,:,3])),'mean_coverage':float(np.mean(a[:,:,3])/255)}

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--originals',required=True,type=Path);ap.add_argument('--base-pack',required=True,type=Path);ap.add_argument('--output',required=True,type=Path);a=ap.parse_args()
 if a.output.exists():raise ValueError('Output must be a NEW directory; source pack never overwritten.')
 if not a.originals.is_dir() or not a.base_pack.is_dir():raise ValueError('Missing source directories.')
 manifest=json.loads((a.base_pack/'pack-manifest.json').read_text());shutil.copytree(a.base_pack,a.output)
 entries=[];cache={}
 for bank in manifest['banks']:
  for t in bank['textures']:
   if (bank['source'],t['ordinal'],t['texture_id']) not in ALLOW:continue
   src=a.originals/t['png'];im=Image.open(src).convert('RGBA')
   h=digest(str(im.size).encode("ascii")+im.tobytes())
   if h!=t['source_rgba_sha256']:raise ValueError('Original fingerprint mismatch: '+str(src))
   family,seed=FAMILIES[t['texture_id']]
   if (h,family) not in cache:cache[h,family]=author(im,family,seed)
   result,info=cache[h,family];dest=a.output/t['png'];result.save(dest,compress_level=9)
   sidecar={'format':'jetmoto-effect-material-1','sourceKey':bank['source']+f"#{t['ordinal']}:{t['texture_id']}",
    'sourceWidth':t['width'],'sourceHeight':t['height'],'alphaMode':'coverage','family':family,
    'note':'New authored artwork; native UV animation and original primitive blending retained. Alpha is coverage, not STP.'}
   sidepath=Path(str(dest)+'.material.json');sidepath.write_text(json.dumps(sidecar,indent=2)+'\n')
   t['png_sha256']=digest(dest.read_bytes());t['enhancement']={'method':'authored-particle-'+family,'neural':False,'seed':seed,'alpha':'coverage','material_sha256':digest(sidepath.read_bytes())}
   entries.append({'bank':bank['source'],'ordinal':t['ordinal'],'id':t['texture_id'],'png':t['png'],'source_rgba_sha256':h,'png_sha256':t['png_sha256'],**info,'family':family})
 if len(entries)!=15 or len(cache)!=5:raise ValueError(f'Expected 15 entries / 5 originals, got {len(entries)} / {len(cache)}')
 manifest['build']='09';manifest['method']='Cumulative Build08 art with new color-matched water/sand/soil/mud/snow sprites; explicit smooth effect coverage'
 manifest['method_counts']=dict(collections.Counter(t['enhancement']['method'] for b in manifest['banks'] for t in b['textures']))
 manifest['neural_texture_count']=sum(t['enhancement'].get('neural',False) for b in manifest['banks'] for t in b['textures'])
 manifest['processing']['alpha']='Original categories retained for unchanged textures; 15 explicitly authored effects use smooth coverage with identity-bound sidecars.'
 manifest['effect_replacement']={'tool':'replace_effects.py','tool_sha256':digest(Path(__file__).read_bytes()),'changed_entries':15,'unchanged_entries':1391,'unique_artworks':5,'source':'original 1x color palette only; no old sprite geometry or enlarged pixels used','entries':entries}
 (a.output/'pack-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
 print(json.dumps({'changed':15,'unchanged':1391,'unique_artworks':5,'neural_images_retained':manifest['neural_texture_count']},indent=2))
if __name__=='__main__':
 try:main()
 except Exception as e:raise SystemExit('Failed: '+str(e))
