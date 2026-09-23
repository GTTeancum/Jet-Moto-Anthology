#!/usr/bin/env python3
"""Build08 rider/bike refinement: original 1x inputs, four-pass RRDB, original decals.

Offline authoring only. The game still loads ordinary original-ID 4x PNGs.
Requires PyTorch, Pillow, NumPy, SciPy. Never uses VRAM or an anime model.
"""
from __future__ import annotations
import argparse, hashlib, json, shutil, time
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image
import torch
from scipy.ndimage import binary_dilation, distance_transform_edt, gaussian_filter
from neural_models import load_model
from enhance_native_pack import prepare_rgb, PAD, source_key, digest_file, safe_join
from build_native_pack import Disc, records

MODEL_HASH='4fa0d38905f75ac06eb49a7951b426670021be3018265fd191d2125df9d682f1'
ATLAS_ID_ALIASES={'0000ED7E': '0000ED7E', '0000ED7F': '0000ED7F', '0000ED66': '0000ED66', '0000ED65': '0000ED65', '0000EED0': '0000ED7E', '0000EED2': '0000ED7F', '0000EECD': '0000ED66', '0000EEC4': '0000ED65'}
HERO_IDS=set(ATLAS_ID_ALIASES)

def neural_detail(im: Image.Image, model) -> Image.Image:
    if im.mode!='RGBA' or im.size!=(128,256):raise ValueError('Expected the original 128x256 RGBA rider atlas.')
    a,rgb,padded,_=prepare_rgb(im,'hero-reflect');outputs=[]
    for flip,rot in [(False,0),(True,0),(False,2),(True,2)]:
        arr=np.rot90(padded,rot)
        if flip:arr=arr[:,::-1]
        x=torch.from_numpy(arr.copy().astype(np.float32).transpose(2,0,1)/255.).unsqueeze(0).to(memory_format=torch.channels_last)
        with torch.inference_mode():y=model(x)[0].permute(1,2,0).cpu().numpy()*255.
        if flip:y=y[:,::-1]
        y=np.rot90(y,-rot)[PAD*4:-PAD*4,PAD*4:-PAD*4];outputs.append(y.copy())
    y=np.mean(outputs,axis=0)
    if not np.isfinite(y).all():raise ValueError('Non-finite model output.')
    reference=np.asarray(Image.fromarray(rgb).resize((512,1024),Image.Resampling.BICUBIC),dtype=np.float32)
    y+=gaussian_filter(reference-y,sigma=(8,8,0),mode='reflect')
    out=np.zeros((1024,512,4),np.uint8);out[:,:,:3]=np.rint(y).clip(0,255).astype('uint8')
    out[:,:,3]=a[:,:,3].repeat(4,0).repeat(4,1);out[out[:,:,3]==0,:3]=0
    return Image.fromarray(out)

def restore_decal(im: Image.Image, source: Image.Image, cfg: dict) -> Image.Image:
    a=np.array(im);original_alpha=a[:,:,3].copy()
    rgb=np.array(source.convert('RGB').crop(cfg['crop'])).astype(np.float32)
    if cfg['kind']=='axiom':
        yy,xx=np.indices(rgb.shape[:2]);alpha=np.clip((1-np.sqrt(((xx-24.5)/24.4)**2+((yy-24.5)/24.4)**2))*15,0,1)
    elif cfg['kind']=='mountain-dew':
        alpha=np.maximum(np.clip((rgb.min(2)-72)/88,0,1),np.clip((rgb[:,:,0]-np.maximum(rgb[:,:,1],rgb[:,:,2])-16)/64,0,1))
    else:raise ValueError('Unknown original decal profile.')
    layer=Image.fromarray(np.dstack((rgb,alpha*255)).round().clip(0,255).astype('uint8'))
    x,y,ww,hh=[round(v*4) for v in cfg['target']];layer=layer.resize((ww,hh),Image.Resampling.LANCZOS)
    if cfg['kind']=='mountain-dew':
        # Remove only old low-resolution decal ink within its existing jacket UV island.
        x0,y0,x1,y1=90*4,168*4,111*4,187*4;region=a[y0:y1,x0:x1,:3].astype(np.float32)
        r,g,b=region.transpose(2,0,1)
        ink=(np.minimum(np.minimum(r,g),b)>70)|((r>g*1.4)&(r>b*1.3)&(r>65))
        ink=binary_dilation(ink,iterations=3)
        if ink.any() and (~ink).any():
            indices=distance_transform_edt(ink,return_distances=False,return_indices=True)
            fill=gaussian_filter(region[indices[0],indices[1]],sigma=(2.5,2.5,0))
            blend=gaussian_filter(ink.astype(float),sigma=1.0)[:,:,None]
            a[y0:y1,x0:x1,:3]=np.rint(region*(1-blend)+fill*blend).clip(0,255).astype('uint8')
    layer=np.array(layer).astype(float);weight=layer[:,:,3:4]/255.;dest=a[y:y+hh,x:x+ww,:3].astype(float)
    a[y:y+hh,x:x+ww,:3]=np.rint(layer[:,:,:3]*weight+dest*(1-weight)).clip(0,255).astype('uint8')
    if not np.array_equal(a[:,:,3],original_alpha):raise ValueError('Native transparency changed.')
    return Image.fromarray(a)

def main() -> int:
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--base-pack',type=Path,required=True,help='Complete Build07 or Build08 pack; untouched files are copied unchanged.')
    p.add_argument('--output',type=Path,required=True,help='New/empty output directory.')
    source=p.add_mutually_exclusive_group(required=True)
    source.add_argument('--cue',type=Path);source.add_argument('--originals',type=Path,help='Original 1x PNG tree, not an enlarged pack.')
    p.add_argument('--weights',type=Path,required=True);p.add_argument('--threads',type=int,default=3)
    p.add_argument('--profiles',type=Path,default=Path(__file__).with_name('rider-decal-profiles.json'))
    a=p.parse_args()
    if a.output.resolve()==a.base_pack.resolve() or (a.output.exists() and any(a.output.iterdir())):p.error('Use a new output directory.')
    if a.threads<1:p.error('--threads must be positive.')
    if digest_file(a.weights/'RealESRGAN_x4plus.pth')!=MODEL_HASH:raise ValueError('Unexpected or corrupt model weights.')
    manifest=json.loads((a.base_pack/'pack-manifest.json').read_text());profiles=json.loads(a.profiles.read_text());originals={}
    if manifest.get('texture_count')!=1406 or manifest.get('build') not in ('07','08'):raise ValueError('Expected a complete cumulative neural pack.')
    if a.cue:
        with_disc=Disc(a.cue)
        try:
            if hashlib.sha256(with_disc.read('SCUS_943.09')).hexdigest()!='f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':raise ValueError('Different game revision.')
            for bank in manifest['banks']:
                raw=with_disc.read(bank['source'])
                if hashlib.sha256(raw).hexdigest()!=bank['original_sha256']:raise ValueError('Changed original bank.')
                for meta,im in records(raw):
                    rel=Path(bank['source']).with_suffix('').as_posix()+f"/{meta['ordinal']:04d}-{meta['texture_id']}.png"
                    if meta['texture_id'] in HERO_IDS or rel in {v['source'] for v in profiles.values()}:originals[rel]=im
        finally:with_disc.close()
    else:
        for bank in manifest['banks']:
            for item in bank['textures']:
                rel=item['png']
                if item['texture_id'] in HERO_IDS or rel in {v['source'] for v in profiles.values()}:originals[rel]=Image.open(safe_join(a.originals,rel)).convert('RGBA')
    torch.set_num_threads(a.threads);model=load_model(a.weights,'rrdb').to(memory_format=torch.channels_last)
    cache={};timings=[];changed=0
    shutil.copytree(a.base_pack,a.output,dirs_exist_ok=True)
    for bank in manifest['banks']:
        for item in bank['textures']:
            rel=item['png'];target=safe_join(a.output,rel)
            if digest_file(target)!=item['png_sha256']:raise ValueError('Damaged base-pack PNG: '+rel)
            if item['texture_id'] not in HERO_IDS:continue
            im=originals[rel];key=source_key(im)
            if key!=item['source_rgba_sha256']:raise ValueError('Wrong original texture: '+rel)
            cfg=profiles.get(ATLAS_ID_ALIASES[item['texture_id']])
            if key not in cache:
                begin=time.monotonic();out=neural_detail(im,model)
                if cfg:out=restore_decal(out,originals[cfg['source']],cfg)
                cache[key]=out;timings.append(dict(source=rel,seconds=time.monotonic()-begin,passes=4))
                print('Enhanced:',rel,flush=True)
            cache[key].save(target)
            item['png_sha256']=digest_file(target)
            item['enhancement']=dict(method='rrdb-rider-original-decal' if cfg else 'rrdb-rider-ensemble',neural=True,
                passes=4,borders=dict(x='reflect',y='reflect'),original_decal=cfg or None)
            changed+=1
    if changed!=44 or len(cache)!=4:raise ValueError('Unexpected rider atlas coverage.')
    manifest['build']='08';manifest['method']='Build07 neural pack plus focused four-pass RRDB rider/bike atlases and original-disc decal restoration'
    manifest['method_counts']=dict(Counter(t['enhancement']['method'] for b in manifest['banks'] for t in b['textures']))
    manifest['rider_refinement']=dict(tool=Path(__file__).name,tool_sha256=digest_file(Path(__file__)),profiles_sha256=digest_file(a.profiles),model_sha256=MODEL_HASH,
        unique_atlases=4,updated_entries=44,unchanged_entries=1362,source_size=[128,256],output_size=[512,1024],passes=4,torch=torch.__version__,timings=timings,
        note='Original 1x sources, no anime/style-transfer model; logo crops are original disc artwork, not invented lettering. Alpha and original material IDs retained.')
    (a.output/'pack-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('Completed: 44 rider/bike PNG entries, 1362 other PNGs preserved unchanged.')
    return 0

if __name__=='__main__':
    try:raise SystemExit(main())
    except (OSError,ValueError,KeyError) as e:raise SystemExit('Failed: '+str(e))
