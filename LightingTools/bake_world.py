#!/usr/bin/env python3
"""Reproduce world receiver/height DATA from the original disc; no texture-art or game-code edits.
Dependencies: Python 3.10+, NumPy, Pillow. Normal game builds already ship the baked data.
The input pack must be the complete native source manifest; output must be empty.
"""
from pathlib import Path
import sys,json,struct,hashlib,gzip,collections,argparse
import numpy as np
from PIL import Image
from native_graph import Graph
from native_material import material
sys.path.insert(0,str(Path(__file__).resolve().parent.parent/'TextureTools'))
from build_native_pack import Disc,records
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--cue',type=Path,required=True);parser.add_argument('--pack',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
parser.add_argument('--bank',action='append',help='Restrict output to these scene banks; repeat for multiple banks.')
args=parser.parse_args();OUT=args.output
if OUT.exists() and any(OUT.iterdir()):parser.error('Output must be new/empty')
OUT.mkdir(parents=True,exist_ok=True)
disc=Disc(args.cue);read=disc.read
if hashlib.sha256(read('SCUS_943.09')).hexdigest()!='f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':raise ValueError('Different original executable')
manifest=json.loads((args.pack/'pack-manifest.json').read_text())
water_ids={k:set(v) for k,v in json.loads(Path(__file__).with_name('water-identities.json').read_text()).items()}
summary=[]
for bank in manifest['banks']:
 name=bank['source'].split('/')[0]
 if args.bank and name not in args.bank:continue
 if name not in ['ISLAND1','ISLAND2','ISLAND3','SWAMP1','SWAMP2','SWAMP3','ALPINE1','ALPINE2','ALPINE3','DARK']:continue
 file=read(bank['source'].replace('.TMS','.DMD'));g=Graph(file);root=next((x for x in g.roots if g.sh(x+2)==1),g.roots[0]);g.walk(root)
 if g.bad:raise ValueError((name,g.bad[:3]))
 images=bank['textures'];image_cache={};material_cache={}
 def native_material(graph,offset):
  if offset not in material_cache:material_cache[offset]=material(graph,offset,images)
  return material_cache[offset]
 original_bank=read(bank['source']);assert hashlib.sha256(original_bank).hexdigest()==bank['original_sha256']
 original_images=[im for meta,im in records(original_bank)]
 xy_limit=30000 if name in ('ALPINE1','ALPINE2') else 6000
 z_limit=10000 if name in ('ALPINE1','ALPINE2') else 1200
 def image_opaque(im):
  k=im['ordinal']
  if k not in image_cache:
   a=np.array(original_images[k])[:,:,3];image_cache[k]=bool(np.all(a>0))
  return image_cache[k]
 water_lod_meshes=set()
 def polygon_info(g,o,pts,mesh_offset=-1,include_large_water=False):
  op=g.b[o+19];native=native_material(g,o);normal=-np.cross(pts[1]-pts[0],pts[2]-pts[0]);area=np.linalg.norm(normal)
  if area<1e-8:return None
  normal/=area
  # Exact source water/underwater surface identity plus flat geometry; avoids skies, particles,
  # riverside vegetation sharing approximate colors, and arbitrary blue artwork.
  island_ocean_plate=bool(include_large_water and name.startswith('ISLAND') and not native and (op&4)==0 and
   np.max(np.abs(pts[:,:2]))>6500 and pts[:,2].min()>=-160 and pts[:,2].max()<=16)
  water=bool(abs(normal[2])>.88 and ((native and native[0]['texture_id'] in water_ids.get(name,set())) or
   (not native and (op&4)==0 and mesh_offset in water_lod_meshes) or island_ocean_plate))
  if not water and (op&2 or not native or not image_opaque(native[0])):return None
  if island_ocean_plate:return 4
  if water and include_large_water:return 2
  if np.max(np.abs(pts[:,:2]))>xy_limit or np.max(pts[:,2])>z_limit or np.max(np.ptp(pts,axis=0))>2500:return None
  return 2 if water else 1
 # Include only actual solid highest-detail scene geometry as occluders.
 tris=[];bounds=[]
 for me in g.meshes:
  for o,pts in me['polys']:
   k=polygon_info(g,o,pts)
   if k:
    bounds.append(pts)
    if k==1:
     tris.append(pts[[0,1,2]])
     if len(pts)==4:tris.append(pts[[1,3,2]])
 if not bounds:continue
 allpts=np.concatenate(bounds);lo=allpts[:,:2].min(0)-32;hi=allpts[:,:2].max(0)+32;hlo=float(allpts[:,2].min()-32);hhi=float(allpts[:,2].max()+32)
 N=1024;heights=np.full((N,N),hlo,np.float32);occupancy=np.zeros((N,N),bool)
 for tri in tris:
  xy=(tri[:,:2]-lo)/(hi-lo)*(N-1);a,b,c=xy;det=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
  if abs(det)<1e-5:continue
  x0,y0=np.maximum(0,np.floor(xy.min(0)).astype(int));x1,y1=np.minimum(N-1,np.ceil(xy.max(0)).astype(int))
  if x1<x0 or y1<y0:continue
  yy,xx=np.mgrid[y0:y1+1,x0:x1+1];v=np.stack([xx-a[0],yy-a[1]],-1);ab=b-a;ac=c-a
  u=(v[:,:,0]*ac[1]-v[:,:,1]*ac[0])/det;vv=(ab[0]*v[:,:,1]-ab[1]*v[:,:,0])/det;mask=(u>=-.005)&(vv>=-.005)&(u+vv<=1.005)
  z=tri[0,2]+u*(tri[1,2]-tri[0,2])+vv*(tri[2,2]-tri[0,2]);target=heights[y0:y1+1,x0:x1+1];target[mask]=np.maximum(target[mask],z[mask]);occupancy[y0:y1+1,x0:x1+1]|=mask
 quant=np.rint(np.clip((heights-hlo)/(hhi-hlo),0,1)*65535).astype(np.uint16);rgba=np.zeros((N,N,4),np.uint8);rgba[:,:,0]=quant>>8;rgba[:,:,1]=quant&255;rgba[:,:,2]=occupancy*255;rgba[:,:,3]=255
 # PNG is DATA (height encoding), not authored visual artwork; no image model.
 Image.fromarray(rgba).save(OUT/(name+'-height.png'))
 full=Graph(file,all_variants=True);full.walk(root)
 if full.bad:raise ValueError((name,full.bad[:3]))
 # The original distant-water LODs are often UNTEXTURED flat polygons. Their
 # identity comes from the same verified native LOD family as a fully water-
 # textured near child, never from a rendered blue/green pixel or color value.
 water_families=set();classified={}
 for me in full.meshes:
  if me['offset'] not in classified:
   good=bool(me['polys'])
   for off,pts in me['polys']:
    mat=native_material(full,off)
    if not mat or mat[0]['texture_id'] not in water_ids.get(name,set()):good=False;break
   classified[me['offset']]=good
  if classified[me['offset']]:
   parent=next((node for node in reversed(me['path'][:-1]) if full.b[node]==2),None)
   if parent is not None:water_families.add(parent)
 for me in full.meshes:
  if any(node in water_families for node in me['path']):water_lod_meshes.add(me['offset'])
 meshes={}
 for me in full.meshes:
  if not me['polys']:continue
  if not any(polygon_info(full,o,pts,me['offset'],include_large_water=True) for o,pts in me['polys']):continue
  key=me['offset'];entry=meshes.setdefault(key,{'offset':key,'vertices':full.ptr(key+4),'instances':[],'polygons':{}})
  ins=[me['shift']]+[round(x,7) for row in me['rotation'] for x in row]+[round(x,7) for x in me['tr']]
  if ins not in entry['instances']:entry['instances'].append(ins)
  for o,pts in me['polys']:
   kind=polygon_info(full,o,pts,me['offset'],include_large_water=True)
   if not kind:continue
   count=len(pts);ix=struct.unpack_from('<'+'h'*count,full.b,o+4);local=np.array([struct.unpack_from('<3h',full.b,entry['vertices']+i*8) for i in ix],float);n=-np.cross(local[1]-local[0],local[2]-local[0]);n/=max(1e-8,np.linalg.norm(n))
   # Normal winding is kept except horizontal ground is always upward-facing.
   if abs(n[2])>.85 and n[2]<0:n=-n
   entry['polygons'][o]=[o,kind,*[round(x,7) for x in n],int(native[0]['ordinal']) if (native:=native_material(full,o)) else -1]
 for k in list(meshes):
  if not meshes[k]['polygons']:del meshes[k];continue
  meshes[k]['polygons']=list(meshes[k]['polygons'].values())
 doc={'format':1,'bank':bank['source'],'modelSha256':hashlib.sha256(file).hexdigest(),'heightSha256':hashlib.sha256((OUT/(name+'-height.png')).read_bytes()).hexdigest(),'origin':lo.tolist(),'size':(hi-lo).tolist(),'heightRange':[hlo,hhi-hlo],'mapSize':N,'lightDirection':[-.45,-.30,.84],'meshes':list(meshes.values()),'waterIds':sorted(water_ids.get(name,set())),'waterLodNodes':sorted(water_families)}
 (OUT/(name+'.json.gz')).write_bytes(gzip.compress(json.dumps(doc,separators=(',',':')).encode(),compresslevel=6,mtime=0))
 row={'bank':name,'meshes':len(meshes),'instances':sum(len(v['instances']) for v in meshes.values()),'surfaceRecords':sum(len(v['polygons']) for v in meshes.values()),'heightTriangles':len(tris),'occupancyFraction':float(occupancy.mean()),'waterIds':doc['waterIds']};summary.append(row);print(row,flush=True)
(OUT/'catalog.json').write_text(json.dumps({'format':1,'method':'Original static DMD hierarchy geometry; opaque near-LOD heightfield; exact source material receiver identities','banks':summary},indent=2)+'\n')

disc.close()
