"""Fit restored boost-lens artwork into each original DAD3 sprite footprint."""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np
from PIL import Image
from build_native_pack import Disc, records
from refine_hud_buoys import TRACKS


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--cue',type=Path,required=True)
    p.add_argument('--art',type=Path,required=True)
    p.add_argument('--output',type=Path,required=True)
    p.add_argument('--stage',type=Path)
    args=p.parse_args();args.output.mkdir(parents=True,exist_ok=True)
    art=Image.open(args.art).convert('RGBA')
    assert art.getchannel('A').getextrema()[0]==0,'Expected transparent generated artwork'
    art.save(args.output/'boost-lens-art.png')
    lens=art.crop(art.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox())
    disc=Disc(args.cue);manifest=[]
    for track in TRACKS:
        bank=f'{track}/{track}.TMS';data=disc.read(bank)
        for meta,source in records(data):
            if int(meta['texture_id'],16)!=0xDAD3:continue
            box=source.getchannel('A').getbbox();x0,y0,x1,y1=(v*4 for v in box)
            tile=np.array(lens.resize((x1-x0,y1-y0),Image.Resampling.LANCZOS))
            tile[:,:,3]=np.where(tile[:,:,3]>=128,255,0)
            tile[tile[:,:,3]==0,:3]=0
            result=Image.new('RGBA',(source.width*4,source.height*4))
            result.paste(Image.fromarray(tile),(x0,y0))
            assert result.getchannel('A').getbbox()==(x0,y0,x1,y1)
            relative=f'{track}/{track}/{meta["ordinal"]:04d}-0000DAD3.png'
            out=args.output/relative;out.parent.mkdir(parents=True,exist_ok=True);result.save(out)
            if args.stage:
                target=args.stage/relative;target.parent.mkdir(parents=True,exist_ok=True)
                backup=args.output/'previous'/relative;backup.parent.mkdir(parents=True,exist_ok=True)
                if target.exists() and not backup.exists():backup.write_bytes(target.read_bytes())
                target.write_bytes(out.read_bytes())
            manifest.append(dict(track=track,output=relative,sourceBounds=box,
                sourceSha256=hashlib.sha256(data).hexdigest(),outputSha256=hashlib.sha256(out.read_bytes()).hexdigest()))
    assert len(manifest)==10
    (args.output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print(f'Packaged {len(manifest)} 32x32 boost sprites; original occupied bounds and opaque alpha preserved.')


if __name__=='__main__':main()
