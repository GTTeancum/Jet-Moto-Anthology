#!/usr/bin/env python3
"""Batch decode original Jet Moto TMS records and create source-addressed 4x PNGs.

Requires Python 3.10+ and Pillow. No AI model, emulator, VRAM dump or pixel hash
matching. PNG names are original bank path / ordinal-originalTextureId.png.
This utility reads an extracted original asset tree, or the supported USA CUE.
Original files are never modified. --output must be a NEW/empty directory unless
--force is explicit. Transparency categories are retained, not interpolated.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path
from PIL import Image


def u32(data: bytes, offset: int) -> int:
    return struct.unpack_from('<I', data, offset)[0]


def records(bank: bytes):
    if len(bank) < 16 or u32(bank,0) != 0x50535854 or u32(bank,4) != 0x43:
        raise ValueError('Not Jet Moto TMS v0x43')
    count = u32(bank,12)
    if count > 4096 or 16+count*4 > len(bank): raise ValueError('Invalid record count')
    at = 16+count*4
    for ordinal in range(count):
        if at+12 > len(bank): raise ValueError('Truncated TIM')
        length = u32(bank,at); start=at+4; end=start+length
        if length < 20 or end>len(bank) or u32(bank,start)!=16: raise ValueError('Invalid TIM header')
        flags=u32(bank,start+4); depth=flags&3; p=start+8; palette=None; clut=None
        if flags&8:
            size,x,y,w,h=struct.unpack_from('<IHHHH',bank,p)
            if size!=12+w*h*2 or p+size>end: raise ValueError('Invalid CLUT')
            palette=struct.unpack_from('<'+'H'*(w*h),bank,p+12);clut=(x,y,w,h);p+=size
        size,x,y,words,h=struct.unpack_from('<IHHHH',bank,p)
        if size!=12+words*h*2 or p+size!=end: raise ValueError('Invalid pixel block')
        raw=bank[p+12:end]
        if depth==0:
            if palette is None or len(palette)!=16: raise ValueError('Unsupported 4-bit palette')
            values=[palette[n] for b in raw for n in (b&15,b>>4)];w=words*4
        elif depth==1:
            if palette is None or len(palette)!=256: raise ValueError('Unsupported 8-bit palette')
            values=[palette[b] for b in raw];w=words*2
        elif depth==2:
            values=struct.unpack('<'+'H'*(len(raw)//2),raw);w=words
        else: raise ValueError('24-bit TMS not supported by this pack')
        rgba=bytearray(w*h*4)
        for i,v in enumerate(values):
            # Exact native 5-bit -> modulation-domain expansion (31 becomes 248).
            rgba[i*4:i*4+4]=bytes(((v&31)*8,((v>>5)&31)*8,((v>>10)&31)*8,
                                  0 if v==0 else 128 if v&0x8000 else 255))
        ident=u32(bank,16+ordinal*4)
        yield dict(ordinal=ordinal,texture_id=f'{ident:08X}',tim_offset=start,tim_length=length,
                   depth=depth,width=w,height=h,source_placement=[x,y],source_clut=clut),Image.frombytes('RGBA',(w,h),bytes(rgba))
        at=end
    if at != len(bank): raise ValueError('Unexpected bank trailing data')


def upscale(image: Image.Image) -> Image.Image:
    """Lanczos 4x RGB with occupancy premultiplication; nearest original STP class.

    An STP value of 128 is a PS1 material bit, NOT half opacity. It must not be
    fed into an ordinary RGBA resizer as translucent coverage. Only zero/nonzero
    occupancy is used for RGB edge filtering; the original categories are restored.
    """
    size=(image.width*4,image.height*4)
    alpha=image.getchannel('A')
    occupied=alpha.point(lambda a: 255 if a else 0)
    color=image.copy();color.putalpha(occupied)
    color=color.resize(size,Image.Resampling.LANCZOS)
    color.putalpha(alpha.resize(size,Image.Resampling.NEAREST))
    return color


class Disc:
    def __init__(self,cue: Path):
        text=cue.read_text(encoding='utf-8-sig')
        m=re.search(r'^FILE\s+"([^"\r\n]+)"\s+BINARY\s+TRACK\s+01\s+MODE2/2352\s+INDEX\s+01\s+00:00:00',text,re.I|re.M)
        if not m:raise ValueError('Expected USA multi-BIN CUE: track 01 MODE2/2352, INDEX 01 00:00:00')
        self.file=(cue.parent/m[1]).open('rb')
        pvd=self.extent(16,2048)
        if pvd[0]!=1 or pvd[1:6]!=b'CD001':raise ValueError('ISO9660 primary descriptor missing')
        self.files={}
        self.walk(pvd[156:156+pvd[156]],'',set())
    def extent(self,lba: int,size: int)->bytes:
        out=bytearray()
        for sector in range((size+2047)//2048):
            self.file.seek((lba+sector)*2352+24);block=self.file.read(2048)
            if len(block)!=2048:raise ValueError('Truncated data track')
            out.extend(block)
        return bytes(out[:size])
    def walk(self,record: bytes,prefix: str,visited: set):
        lba=u32(record,2);size=u32(record,10)
        if lba in visited:return
        visited.add(lba);buf=self.extent(lba,size);o=0
        while o<len(buf):
            length=buf[o]
            if not length:o=(o//2048+1)*2048;continue
            r=buf[o:o+length];o+=length
            if len(r)<34:raise ValueError('Invalid ISO record')
            name=r[33:33+r[32]]
            if name in (b'\0',b'\1'):continue
            name=name.decode('ascii').split(';')[0]
            if '/' in name or '\\' in name or name in ('.','..'):raise ValueError('Unsafe ISO filename')
            path=f'{prefix}/{name}'.lstrip('/')
            if r[25]&2:self.walk(r,path,visited)
            else:self.files[path]=(u32(r,2),u32(r,10))
    def read(self,name: str)->bytes:return self.extent(*self.files[name])
    def close(self):self.file.close()


def main()->int:
    parser=argparse.ArgumentParser(description=__doc__)
    group=parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--cue',type=Path);group.add_argument('--assets-root',type=Path)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--originals',type=Path,help='Optional separate directory for 1x authoring PNGs')
    parser.add_argument('--force',action='store_true',help='Explicitly permit overwriting previously generated PNGs')
    args=parser.parse_args()
    if args.output.exists() and any(args.output.iterdir()) and not args.force:
        parser.error('Output is not empty. Use a new directory or explicitly --force.')
    if args.originals and args.originals.resolve()==args.output.resolve():parser.error('Originals and 4x output must differ')
    args.output.mkdir(parents=True,exist_ok=True)
    disc=Disc(args.cue) if args.cue else None
    try:
        if disc:
            exe=disc.read('SCUS_943.09')
            if hashlib.sha256(exe).hexdigest()!='f1ad5aa4a092c9fc2a7f2d6795951a60a4419d02a0a3d0ea2a200fabc7a0ce48':raise ValueError('Different game revision')
            banks=sorted(n for n in disc.files if n.endswith('.TMS'))
            read=disc.read
        else:
            root=args.assets_root.resolve()
            banks=sorted(p.relative_to(root).as_posix() for p in root.rglob('*.TMS'))
            read=lambda n:(root/n).read_bytes()
        manifest={'format':1,'game':'Jet Moto USA SCUS_943.09','scale':4,
            'method':'Pillow Lanczos RGB/occupancy; nearest native alpha category (0/128/255); not AI',
            'identity':'original bank path / record ordinal / original texture ID; no VRAM matching',
            'banks':[]}
        total=0
        for bank in banks:
            data=read(bank);items=[]
            for meta,image in records(data):
                rel=Path(bank).with_suffix('')/f"{meta['ordinal']:04d}-{meta['texture_id']}.png"
                output=args.output/rel;output.parent.mkdir(parents=True,exist_ok=True)
                enlarged=upscale(image);enlarged.save(output,compress_level=6)
                if args.originals:
                    original=args.originals/rel;original.parent.mkdir(parents=True,exist_ok=True);image.save(original)
                meta['png']=rel.as_posix();meta['png_sha256']=hashlib.sha256(output.read_bytes()).hexdigest()
                items.append(meta);total+=1
            manifest['banks'].append({'source':bank,'original_sha256':hashlib.sha256(data).hexdigest(),'textures':items})
            print(f'{bank}: {len(items)} textures',flush=True)
        manifest['texture_count']=total
        (args.output/'pack-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
        print(f'Completed {total} native textures from {len(banks)} banks. Original disc unchanged.')
        return 0
    finally:
        if disc:disc.close()

if __name__=='__main__':
    try:raise SystemExit(main())
    except (OSError,ValueError,struct.error) as e:raise SystemExit(f'Failed: {e}')
