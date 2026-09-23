#!/usr/bin/env python3
"""Build Jet Moto's original-ID 4x neural texture pack from original TMS assets.

Requires Python 3.10+, PyTorch supporting weights_only=True, NumPy, Pillow, SciPy.
Only the authoring tool needs these dependencies; the game loads ordinary PNGs.
No runtime pixel hashing, VRAM replacement, style-transfer or anime model.
"""
from __future__ import annotations
import argparse, hashlib, json, shutil, sys, time
from collections import Counter
from pathlib import Path
import numpy as np
from PIL import Image
import torch
from scipy.ndimage import distance_transform_edt, gaussian_filter
from build_native_pack import Disc, records
from neural_models import load_model

MODEL_HASHES = {
 'RealESRGAN_x4plus.pth':'4fa0d38905f75ac06eb49a7951b426670021be3018265fd191d2125df9d682f1',
 'realesr-general-x4v3.pth':'8dc7edb9ac80ccdc30c3a5dca6616509367f05fbc184ad95b731f05bece96292',
 'realesr-general-wdn-x4v3.pth':'1641f8c4464b9f097c9fdda5589273713f67cf59f3d909e0bd688f0cee269dca',
}
SCALE=4
PAD=36

def digest_file(path: Path) -> str:
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1048576),b''):h.update(block)
    return h.hexdigest()

def source_key(im: Image.Image) -> str:
    # Offline computation reuse only. Runtime identity remains bank/ordinal/ID.
    return hashlib.sha256(str(im.size).encode('ascii')+im.tobytes()).hexdigest()

def safe_join(root: Path, relative: str) -> Path:
    p=Path(relative)
    if p.is_absolute() or any(s in ('','..','.') for s in relative.split('/')) or '\\' in relative or ':' in relative:
        raise ValueError(f'Unsafe asset path: {relative!r}')
    return root/p

def continuous_axis(rgb: np.ndarray, axis: int) -> bool:
    """Conservative numeric seam hint, not a claim about original material flags."""
    if rgb.shape[axis]<4:return False
    f=rgb.astype(np.float32)
    step=float(np.median(np.abs(np.diff(f,axis=axis)).mean(axis=2)))
    seam=float(np.abs(np.take(f,0,axis=axis)-np.take(f,-1,axis=axis)).mean())
    return seam<=max(2.0,step*1.35)

def prepare_rgb(im: Image.Image, mode: str):
    a=np.asarray(im,dtype=np.uint8);rgb=a[:,:,:3].copy();occupied=a[:,:,3]!=0
    if occupied.any() and not occupied.all():
        y,x=distance_transform_edt(~occupied,return_distances=False,return_indices=True)
        rgb=rgb[y,x]  # Fill invisible RGB only; never change alpha or visible inputs.
    # Repeat-friendly context only for reviewed, opaque surface assets. Other
    # images use reflection; this does not assume that atlas islands are tiles.
    wrap_y=mode=='rrdb-surface' and occupied.all() and continuous_axis(rgb,0)
    wrap_x=mode=='rrdb-surface' and occupied.all() and continuous_axis(rgb,1)
    padded=np.pad(rgb,((PAD,PAD),(0,0),(0,0)),mode='wrap' if wrap_y else 'reflect')
    padded=np.pad(padded,((0,0),(PAD,PAD),(0,0)),mode='wrap' if wrap_x else 'reflect')
    return a,rgb,padded,dict(x='wrap' if wrap_x else 'reflect',y='wrap' if wrap_y else 'reflect')

def enhance(im: Image.Image, mode: str, models, device):
    a,rgb,padded,borders=prepare_rgb(im,mode)
    occupied=a[:,:,3]!=0
    # Uniform swatches and exact effect masks must not acquire invented marks.
    visible=a[:,:,:3][occupied]
    constant=len(visible)==0 or np.all(visible==visible[0])
    if constant:
        out=np.repeat(np.repeat(a,SCALE,0),SCALE,1)
        return Image.fromarray(out),dict(method='constant-color-preserved',borders=borders,neural=False)
    model=models['rrdb' if mode=='rrdb-surface' else 'general']
    x=torch.from_numpy(padded.astype(np.float32).transpose(2,0,1)/255.0).unsqueeze(0)
    x=x.to(device=device,memory_format=torch.channels_last)
    with torch.inference_mode():
        output=model(x)[0,:,PAD*SCALE:-PAD*SCALE,PAD*SCALE:-PAD*SCALE]
        y=output.permute(1,2,0).float().cpu().numpy()*255.0
    if not np.isfinite(y).all():raise ValueError('Non-finite neural output')
    # Correct only broad color/brightness drift. Do not blend away reconstructed
    # edge or surface detail. Sigma=2 original pixels =8 high-resolution pixels.
    reference=np.asarray(Image.fromarray(rgb).resize((im.width*SCALE,im.height*SCALE),Image.Resampling.BICUBIC),dtype=np.float32)
    y += gaussian_filter(reference-y,sigma=(8.0,8.0,0),mode='reflect')
    out=np.empty((im.height*SCALE,im.width*SCALE,4),dtype=np.uint8)
    out[:,:,:3]=np.rint(y).clip(0,255).astype(np.uint8)
    out[:,:,3]=np.repeat(np.repeat(a[:,:,3],SCALE,0),SCALE,1)
    out[out[:,:,3]==0,:3]=0
    return Image.fromarray(out),dict(method=mode,borders=borders,neural=True)

def main() -> int:
    parser=argparse.ArgumentParser(description=__doc__)
    source=parser.add_mutually_exclusive_group(required=True)
    source.add_argument('--cue',type=Path);source.add_argument('--assets-root',type=Path)
    parser.add_argument('--weights',required=True,type=Path)
    parser.add_argument('--output',required=True,type=Path)
    parser.add_argument('--profiles',type=Path,default=Path(__file__).with_name('enhancement-profiles.json'))
    parser.add_argument('--catalog',type=Path,default=Path(__file__).parent.parent/'JetMoto/config/native-banks.json')
    parser.add_argument('--originals',type=Path,help='Optional separate 1x source PNG tree')
    parser.add_argument('--threads',type=int,default=3)
    parser.add_argument('--device',choices=['cpu','cuda'],default='cpu')
    args=parser.parse_args()
    if args.threads<1:parser.error('--threads must be positive')
    if args.output.exists() and any(args.output.iterdir()):parser.error('Output must be a new or empty directory.')
    if args.originals and args.originals.resolve()==args.output.resolve():parser.error('Originals and outputs must be separate.')
    if args.device=='cuda' and not torch.cuda.is_available():parser.error('CUDA is not available.')
    for name,expected in MODEL_HASHES.items():
        if digest_file(args.weights/name)!=expected:raise ValueError(f'Unexpected/corrupt weights: {name}')
    config=json.loads(args.profiles.read_text(encoding='utf-8'))
    profiles=config['source_pixel_profiles'];denoise=float(config['general_denoise_strength'])
    if not 0<=denoise<=1:raise ValueError('Invalid denoise strength')
    fingerprints=json.loads(args.catalog.read_text(encoding='utf-8'))
    torch.set_num_threads(args.threads);torch.set_num_interop_threads(1)
    models={kind:load_model(args.weights,kind,denoise).to(device=args.device,memory_format=torch.channels_last) for kind in ('general','rrdb')}
    disc=Disc(args.cue) if args.cue else None
    try:
        if disc:
            if hashlib.sha256(disc.read('SCUS_943.09')).hexdigest()!='f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':raise ValueError('Unexpected disc revision')
            read=disc.read;banks=sorted(n for n in disc.files if n.endswith('.TMS'))
        else:
            root=args.assets_root.resolve();banks=sorted(p.relative_to(root).as_posix() for p in root.rglob('*.TMS'))
            read=lambda n:safe_join(root,n).read_bytes()
        if set(banks)!=set(fingerprints):raise ValueError('Expected the complete 34-bank original catalog')
        args.output.mkdir(parents=True,exist_ok=True)
        manifest={'format':1,'game':'Jet Moto USA SCUS_943.09','scale':4,'build':'07',
          'method':'Real-ESRGAN learned 4x SR: reviewed RRDB surfaces; general x4v3 DNI 0.25 otherwise; broad color-drift correction; exact categorical alpha',
          'identity':'original bank path / record ordinal / original texture ID; no VRAM matching',
          'processing':{'tool':'enhance_native_pack.py','torch':torch.__version__,'device':args.device,'precision':'float32','general_denoise_strength':denoise,'source_padding':PAD,'color_drift_sigma_highres':8.0,'alpha':'exact 4x nearest original categories 0/128/255','models_sha256':MODEL_HASHES,'profile_sha256':digest_file(args.profiles)},'banks':[]}
        counts=Counter();cache={};started=time.monotonic();total=0
        for bank in banks:
            raw=read(bank);original_hash=hashlib.sha256(raw).hexdigest()
            if original_hash.lower()!=fingerprints[bank].lower():raise ValueError(f'Original bank checksum mismatch: {bank}')
            items=[]
            for meta,im in records(raw):
                rel=(Path(bank).with_suffix('')/f"{meta['ordinal']:04d}-{meta['texture_id']}.png").as_posix()
                dest=safe_join(args.output,rel);dest.parent.mkdir(parents=True,exist_ok=True)
                if args.originals:
                    orig=safe_join(args.originals,rel);orig.parent.mkdir(parents=True,exist_ok=True);im.save(orig)
                key=source_key(im);mode=profiles.get(key,{}).get('method','general-faithful')
                if mode not in ('rrdb-surface','general-faithful'):raise ValueError(f'Unsupported profile {mode}')
                reused=key in cache
                if reused:
                    prior,info=cache[key];shutil.copyfile(prior,dest)
                else:
                    result,info=enhance(im,mode,models,args.device)
                    result.save(dest,compress_level=6);cache[key]=(dest,info)
                meta.update(png=rel,png_sha256=digest_file(dest),source_rgba_sha256=key,enhancement={**info,'reused_identical_source':reused})
                items.append(meta);total+=1;counts[info['method']]+=1
                print(f"{total:04d} {info['method']:24} {'reuse' if reused else 'run  '} {rel} elapsed={time.monotonic()-started:.1f}s",flush=True)
            manifest['banks'].append({'source':bank,'original_sha256':original_hash,'textures':items})
        manifest.update(texture_count=total,unique_source_images=len(cache),method_counts=dict(counts),neural_texture_count=total-counts['constant-color-preserved'])
        if total!=1406:raise ValueError(f'Expected 1406 textures, got {total}')
        # Only a completed pack receives its final manifest; interrupted output is
        # deliberately not installable as a complete release.
        (args.output/'pack-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
        print(json.dumps({'complete':True,'textures':total,'unique':len(cache),'methods':dict(counts),'seconds':round(time.monotonic()-started,2)}),flush=True)
        return 0
    finally:
        if disc:disc.close()

if __name__=='__main__':
    try:raise SystemExit(main())
    except (OSError,ValueError,RuntimeError) as error:raise SystemExit(f'Enhancement failed: {error}')
