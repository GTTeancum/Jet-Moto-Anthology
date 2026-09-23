#!/usr/bin/env python3
"""Verify the cumulative original-ID pack, including explicit authored effect coverage against its 1x source PNGs.
Optional --baseline points to Build06's filtered pack for numeric comparison.
"""
from pathlib import Path, PurePosixPath
from collections import Counter
import argparse,json,hashlib
import numpy as np
from PIL import Image
from replace_effects import ALLOW

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--pack',required=True,type=Path);p.add_argument('--originals',required=True,type=Path)
    p.add_argument('--baseline',type=Path);p.add_argument('--report',required=True,type=Path)
    a=p.parse_args();m=json.loads((a.pack/'pack-manifest.json').read_text());names=set();methods=Counter();duplicates={};stats=[];alpha_pixels=0;coverage_pixels=0;effect_count=0
    assert m['build']=='09' and m['scale']==4 and len(m['banks'])==34
    for bank in m['banks']:
        for t in bank['textures']:
            rel=t['png'];assert rel not in names;names.add(rel)
            assert rel==str(PurePosixPath(bank['source']).with_suffix(''))+f"/{t['ordinal']:04d}-{t['texture_id']}.png"
            data=(a.pack/rel).read_bytes();h=hashlib.sha256(data).hexdigest();assert h==t['png_sha256'],rel
            orig=Image.open(a.originals/rel);out=Image.open(a.pack/rel)
            assert orig.mode==out.mode=='RGBA' and orig.size==(t['width'],t['height']) and out.size==(orig.width*4,orig.height*4),rel
            src=np.asarray(orig);dst=np.asarray(out)
            effect=(bank['source'],t['ordinal'],t['texture_id']) in ALLOW
            if effect:
                side=Path(str(a.pack/rel)+'.material.json');meta=json.loads(side.read_text())
                assert hashlib.sha256(side.read_bytes()).hexdigest()==t['enhancement']['material_sha256'],rel
                assert meta['format']=='jetmoto-effect-material-1' and meta['alphaMode']=='coverage',rel
                assert meta['sourceKey']==bank['source']+f"#{t['ordinal']}:{t['texture_id']}",rel
                assert meta['sourceWidth']==t['width'] and meta['sourceHeight']==t['height'],rel
                al=dst[:,:,3];assert len(np.unique(al))>=32,rel
                assert not al[:6,:].any() and not al[-6:,:].any() and not al[:,:6].any() and not al[:,-6:].any(),rel
                coverage_pixels+=out.width*out.height;effect_count+=1
            else:
                assert not Path(str(a.pack/rel)+'.material.json').exists(),rel
                assert np.isin(dst[:,:,3],[0,128,255]).all(),rel
                assert np.array_equal(dst[:,:,3],np.repeat(np.repeat(src[:,:,3],4,0),4,1)),rel
                alpha_pixels+=out.width*out.height
            key=hashlib.sha256(str(orig.size).encode('ascii')+orig.tobytes()).hexdigest();assert key==t['source_rgba_sha256'],rel
            if key in duplicates:assert duplicates[key]==h,rel
            duplicates[key]=h;method=t['enhancement']['method'];methods[method]+=1
            if method=='constant-color-preserved':assert not t['enhancement']['neural'] and np.array_equal(dst,np.repeat(np.repeat(src,4,0),4,1)),rel
            elif effect:assert not t['enhancement']['neural'] and method.startswith('authored-particle-')
            else:assert t['enhancement']['neural'] and method in ('general-faithful','rrdb-surface','rrdb-rider-ensemble','rrdb-rider-original-decal')
            item={'png':rel,'method':method}
            if a.baseline:
                old=np.asarray(Image.open(a.baseline/rel));occupied=dst[:,:,3]!=0
                difference=np.abs(dst[:,:,:3].astype(np.int16)-old[:,:,:3].astype(np.int16));changed=np.any(difference,axis=2)&occupied
                item.update(changed_visible_pixels=int(changed.sum()),visible_pixels=int(occupied.sum()),mae_vs_filtered=float(difference[occupied].mean()) if occupied.any() else 0.0)
                if t['enhancement']['neural']:assert changed.any(),f'Neural file is visibly identical to filtered baseline: {rel}'
            stats.append(item)
    assert len(names)==m['texture_count']==1406 and len(duplicates)==m['unique_source_images']==551
    assert len(list(a.pack.rglob('*.png')))==1406 and dict(methods)==m['method_counts']
    assert m['neural_texture_count']==1375 and effect_count==15 and methods['constant-color-preserved']==16
    report={'pass':True,'textures':len(names),'banks':34,'unique_source_images':len(duplicates),'methods':dict(methods),'neural_texture_count':m['neural_texture_count'],'exact_alpha_pixels_checked':alpha_pixels,'authored_coverage_pixels_checked':coverage_pixels,'effect_materials':effect_count,'identity_and_hashes':'all matched','dimensions':'every image exactly 4x in each axis','duplicate_consistency':'all identical originals produce byte-identical replacements','baseline_note':'Numerical difference is not an objective image-quality score; inspect comparisons and gameplay.','files':stats}
    a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(report,indent=2)+'\n')
    print(json.dumps({k:v for k,v in report.items() if k!='files'},indent=2))

if __name__=='__main__':main()
