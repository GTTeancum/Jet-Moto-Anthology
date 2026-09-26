"""Source-guided map-line candidates with reviewed per-track profiles; never auto-stages.

Separates neutral bright map linework from local color, fits bounded splines to
its source contours, and composites it over the authoring preview. Text and
colored markers are explicitly protected. Visual review is still required.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.interpolate import splprep, splev
from scipy.ndimage import distance_transform_edt, grey_opening, maximum_filter, minimum_filter
from scipy.spatial import cKDTree

from trace_overview_lettering import contours, rasterize, topology


PROTECTED = {
    'title': [126, 16, 196, 35], 'resort': [100, 34, 156, 54],
    'sand dunes': [174, 67, 248, 81], 'freeway': [44, 108, 101, 123],
    'island': [64, 162, 112, 179], 'jump': [132, 184, 169, 199],
    'sand bars': [40, 199, 113, 214],
    'start finish and arrow': [105, 49, 139, 77],
    'northwest checkpoint': [63, 58, 81, 83],
    'west checkpoint': [38, 153, 64, 177],
    'south checkpoint': [130, 160, 148, 186],
    'southeast checkpoint and grapple': [156, 146, 181, 174],
    'inner checkpoint and grapple': [138, 115, 165, 145],
    'east checkpoint and grapple': [177, 118, 213, 145],
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--preview',type=Path,help='Optional existing title composition; otherwise use verified originals')
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--min-points',type=int,default=32)
    parser.add_argument('--profile',type=Path,help='Reviewed source path, map bounds and protected rectangles')
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]/'reports/build12'
    profile = json.loads(args.profile.read_text()) if args.profile else {
        'source': 'ISLAND1/OVERV3', 'name': 'Joyride',
        'bounds': [20,34,214,204], 'protected': PROTECTED}
    protected = profile['protected']
    if not 0 <= profile.get('strength',1) <= 1 or not 0 <= profile.get('insetOutputPixels',0) <= 1:
        raise ValueError('Strength and subpixel inset must be in [0,1]')
    source_name = profile['source']
    source_path = root/'menu-original-tim'/f'{source_name}.png'
    meta = json.loads(Path(str(source_path)+'.json').read_text())
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != meta['pngSha256']:
        raise ValueError('Original source changed')
    source = np.asarray(Image.open(source_path).convert('RGB'),dtype=float)
    bg = grey_opening(source,size=(3,3,1))
    # Select one actual RGB sample with the strongest neutral intensity in
    # each neighborhood, instead of inventing a maximum for each channel.
    fg = source.copy()
    strength = source.min(axis=2)
    best = strength.copy()
    padded = np.pad(source,((2,2),(2,2),(0,0)),mode='edge')
    for oy in range(5):
        for ox in range(5):
            candidate = padded[oy:oy+240,ox:ox+320]
            score = candidate.min(axis=2)
            better = (score>best)&(candidate.max(axis=2)-score<64)
            fg[better] = candidate[better]
            best[better] = score[better]
    coverage = np.clip(((source-bg)/np.maximum(fg-bg,8)).min(axis=2),0,1)
    eligible = np.zeros((240,320),dtype=bool)
    x0,y0,x1,y1 = profile['bounds']
    eligible[y0:y1,x0:x1] = True
    for x0,y0,x1,y1 in protected.values():
        eligible[y0:y1,x0:x1] = False
    coverage *= eligible&(best>=144)&(source.max(axis=2)-source.min(axis=2)<72)
    extracted = contours(coverage,.5)
    paths = []
    for path in extracted:
        path = path[np.linalg.norm(path-np.roll(path,1,axis=0),axis=1)>1e-8]
        if len(path)>=args.min_points:
            paths.append(path)
    fitted,records = [],[]
    for original in paths:
        # Threshold crossings can meet exactly on a source sample, producing
        # consecutive coincident vertices that FITPACK cannot parameterize.
        if len(original)<8:
            fitted.append(original)
            records.append({'points':len(original),'smoothed':False,'reason':'small contour retained'})
            continue
        closed = np.vstack([original,original[0]])
        chosen = original
        deviation = 0.
        for factor in [.03,.01,.003]:
            tck,u = splprep(closed.T,s=len(original)*factor,per=True,k=3)
            sample = np.array(splev(np.linspace(0,1,len(original)*8,endpoint=False),tck)).T
            # Symmetric geometric deviation bound relative to densely sampled
            # source polygon edges. This is a geometry bound, not acceptance.
            nxt = np.roll(original,-1,axis=0)
            dense = np.concatenate([original+(nxt-original)*t for t in np.linspace(0,1,8,endpoint=False)])
            deviation = max(cKDTree(dense).query(sample)[0].max(),cKDTree(sample).query(dense)[0].max())
            if deviation<=.35:
                chosen = sample
                break
        else:
            deviation=0.
        fitted.append(chosen)
        records.append({'points':len(original),'smoothed':chosen is not original,'maxDeviation':float(deviation)})
    raw_alpha = np.asarray(rasterize(paths,(320,240),0),dtype=float)/255
    alpha = np.asarray(rasterize(fitted,(320,240),0),dtype=float)/255
    original_topology = topology(raw_alpha>=.5)
    spline_topology = topology(alpha>=.5)
    topology_fallbacks = []
    if spline_topology != original_topology:
        # A bounded geometric deviation can still join neighboring strokes.
        # Restore source contours, largest deviation first, until the global
        # component/hole counts match. At worst every contour is restored.
        # This is a preservation guard, not a visual quality acceptance test.
        for index in sorted(range(len(records)),
                            key=lambda i: records[i].get('maxDeviation',0),reverse=True):
            if not records[index]['smoothed']:
                continue
            fitted[index] = paths[index]
            records[index].update(smoothed=False,maxDeviation=0.,
                                  reason='source contour restored by global topology guard')
            topology_fallbacks.append(index)
            alpha = np.asarray(rasterize(fitted,(320,240),0),dtype=float)/255
            spline_topology = topology(alpha>=.5)
            if spline_topology == original_topology:
                break
        if spline_topology != original_topology:
            raise ValueError('Could not preserve source contour topology')
    # Some thin pale borders need less coverage than the filled contour mask.
    # Apply a reviewed subpixel inset, retaining the original source as context.
    inset = profile.get('insetOutputPixels', 0)
    if inset:
        alpha = alpha*(1-inset)+minimum_filter(alpha,size=3)*inset
    args.output.mkdir(parents=True,exist_ok=False)
    Image.fromarray(np.rint(alpha*255).astype(np.uint8)).save(args.output/'map-line-alpha.png')
    # Keep this editable as actual source-coordinate geometry.
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 320 240">',
           f'<title>Source-guided {profile["name"]} map-line candidate</title>',
           '<path fill="#d8d8d8" fill-rule="evenodd" d="']
    for poly in fitted:
        svg.append('M '+' L '.join(f'{x:.4f} {y:.4f}' for x,y in poly)+' Z')
    svg+=['"/>','</svg>']
    (args.output/'map-lines.svg').write_text('\n'.join(svg)+'\n',encoding='ascii')
    def enlarge(a):
        return np.asarray(Image.fromarray(np.rint(a).clip(0,255).astype(np.uint8)).resize((1280,960),Image.Resampling.LANCZOS),dtype=float)
    new_rgb = enlarge(bg)*(1-alpha[:,:,None])+enlarge(fg)*alpha[:,:,None]
    selected = raw_alpha.reshape(240,4,320,4).max(axis=(1,3))>.02
    support = maximum_filter(selected,size=3)&eligible
    support4 = np.repeat(np.repeat(support,4,axis=0),4,axis=1)
    eligible4 = np.repeat(np.repeat(eligible,4,axis=0),4,axis=1)
    support4 |= (alpha>0)&eligible4
    # Taper into protected labels/markers instead of making a visible hard
    # switch from reconstructed borders to the original raster at box edges.
    blend = np.clip(distance_transform_edt(eligible4)/12,0,1)*support4*profile.get('strength',1)
    outputs=[]
    for stem in [Path(source_name).name,Path(source_name).name+'L']:
        if args.preview:
            preview_image = Image.open(args.preview/f'{stem}-title-preview.png').convert('RGB')
        else:
            original_path = source_path.with_name(stem+'.png')
            original_meta = json.loads(Path(str(original_path)+'.json').read_text())
            if hashlib.sha256(original_path.read_bytes()).hexdigest() != original_meta['pngSha256']:
                raise ValueError('Original variant changed')
            preview_image = Image.open(original_path).convert('RGB').resize((1280,960),Image.Resampling.LANCZOS)
        if preview_image.size != (1280,960):
            raise ValueError('Expected a 4x composition')
        rgb = np.array(preview_image)
        rgb[support4] = np.rint(new_rgb[support4]).clip(0,255).astype(np.uint8)
        # Constrain local average RGB contributions to the source. Without this,
        # thin gray sampled strokes became opaque white bands in the first
        # study. Correction applies only to whole, eligible source blocks.
        target = np.asarray(preview_image,dtype=float)
        target_mean = target.reshape(240,4,320,4,3).mean(axis=(1,3))
        corrected = rgb.astype(float)
        for _ in range(20):
            mean = corrected.reshape(240,4,320,4,3).mean(axis=(1,3))
            residual = target_mean-mean
            # Continuous correction avoids painting a grid of source-sized
            # color blocks onto the new vector contours.
            delta = np.stack([np.asarray(Image.fromarray(residual[:,:,c].astype(np.float32)).resize(
                (1280,960),Image.Resampling.BICUBIC)) for c in range(3)],axis=2)
            corrected[support4] = np.clip(corrected[support4]+.75*delta[support4],0,255)
        corrected = target*(1-blend[:,:,None])+corrected*blend[:,:,None]
        rgb = np.rint(corrected).astype(np.uint8)
        for box in protected.values():
            x0,y0,x1,y1 = [v*4 for v in box]
            if not np.array_equal(rgb[y0:y1,x0:x1],target[y0:y1,x0:x1]):
                raise ValueError('Protected content changed')
        if np.any(rgb[~eligible4] != target[~eligible4]):
            raise ValueError('Edit escaped reviewed map bounds')
        out = args.output/f'{stem}-map-study.png'
        Image.fromarray(rgb).save(out)
        outputs.append({'file':out.name,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()})
    report = {'source':meta,'profile':profile,'protected':protected,'contours':records,
              'shortOrDegenerateContoursRetainedAsSource':len(extracted)-len(paths),
              'minimumContourPoints':args.min_points,
              'originalContourTopology':original_topology,'splineTopology':spline_topology,
              'topologyFallbackContours':topology_fallbacks,
              'insetMaskTopology':topology(alpha>=.5),
              'outputs':outputs,'status':'offline map-line authoring study; not accepted or staged',
              'limitations':'local background estimate under linework; paragraph/labels/markers/footer unreconstructed'}
    (args.output/'manifest.json').write_text(json.dumps(report,indent=2)+'\n',encoding='ascii')
    print(json.dumps({k:report[k] for k in ['originalContourTopology','splineTopology','status']}))
    print('contours',len(paths),'smoothed',sum(r['smoothed'] for r in records))


if __name__ == '__main__':
    main()
