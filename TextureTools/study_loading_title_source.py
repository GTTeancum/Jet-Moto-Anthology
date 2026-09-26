"""Fit the game's larger original JOYRIDE title to its loading-title samples.

Produces offline comparison crops only. Does not stage a replacement, substitute
a system font, modify the source artwork, or assert typography equivalence.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, map_coordinates
from scipy.optimize import least_squares

from trace_overview_lettering import contours, rasterize


def verified(path):
    meta = json.loads(Path(str(path)+'.json').read_text())
    if hashlib.sha256(path.read_bytes()).hexdigest() != meta['pngSha256']:
        raise ValueError(f'Changed source: {path}')
    return meta, np.array(Image.open(path).convert('RGB'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]/'reports/build12'
    source_meta, source = verified(root/'menu-original-tim/ISLAND1/OVERV3.png')
    atlas_meta, atlas = verified(root/'menu-loading-lettering-sources/TRACKS0/0001-00005B3E.png')
    if source_meta['source'] != 'ISLAND1/OVERV3.TIM' or atlas_meta['source'] != 'PICKTRAC/TRACKS0.TMS':
        raise ValueError('Unreviewed source identity')
    glyph = (atlas[0:15, 93:164, 0] > 100).astype(float)
    crop = source[17:33, 128:194]
    target = crop[:,:,0].astype(float)
    yy, xx = np.mgrid[17:33,128:194]
    offsets = (np.arange(4)+.5)/4
    xx = np.broadcast_to(xx[...,None,None]+offsets[None,None,:,None],(16,66,4,4))
    yy = np.broadcast_to(yy[...,None,None]+offsets[None,None,None,:],(16,66,4,4))

    def prediction(v):
        x0,y0,sx,sy,gain,bg,sigma = v
        field = gaussian_filter(glyph, sigma=sigma)
        coords = [(yy-y0)/sy-.5,(xx-x0)/sx-.5]
        return map_coordinates(field,coords,order=1,mode='constant').mean(axis=(2,3))*gain+bg

    fit = least_squares(lambda v:(prediction(v)-target).ravel(),
                        [130,19,.835,.7,216,8,.3],
                        bounds=([127,16,.7,.4,150,0,.01],[135,23,1.,1.,248,40,1.]),
                        x_scale='jac',max_nfev=200)
    args.output.mkdir(parents=True,exist_ok=False)
    Image.fromarray(crop).resize((528,128),Image.Resampling.NEAREST).save(args.output/'original-loading-title.png')
    # Contours are obtained from the larger native artwork, not from an
    # unrelated typeface. Smoothing remains an unaccepted authoring choice.
    curve_alpha = np.asarray(rasterize(contours(glyph,.5),(71,15),1),dtype=float)/255
    out_y,out_x = np.mgrid[:64,:264]
    x0,y0,sx,sy,gain,bg,sigma = fit.x
    alpha = map_coordinates(curve_alpha,
                            [((17+(out_y+.5)/4-y0)/sy)*4-.5,
                             ((128+(out_x+.5)/4-x0)/sx)*4-.5],
                            order=1,mode='constant')
    colors = np.array([0,24,40])*(1-alpha[:,:,None])+216*alpha[:,:,None]
    Image.fromarray(np.rint(colors).clip(0,255).astype(np.uint8)).save(args.output/'source-atlas-title-study.png')
    Image.fromarray(np.rint(colors).clip(0,255).astype(np.uint8)).resize(
        (528,128),Image.Resampling.NEAREST).save(args.output/'source-atlas-title-inspection.png')
    Image.fromarray(np.rint(prediction(fit.x)).clip(0,255).astype(np.uint8)).resize(
        (528,128),Image.Resampling.NEAREST).save(args.output/'atlas-projection-to-source.png')
    result = {'loadingSource':source_meta,'atlasSource':atlas_meta,
              'atlasBounds':[93,0,71,15],'loadingBounds':[128,17,66,16],
              'parameters':fit.x.tolist(),'sourceProjectionRmse':float(np.sqrt(np.mean(fit.fun**2))),
              'status':'offline title-only study; requires visual comparison; not staged',
              'background':'flat diagnostic background, not reconstructed loading background'}
    (args.output/'manifest.json').write_text(json.dumps(result,indent=2)+'\n',encoding='ascii')
    print(json.dumps({'parameters':fit.x.tolist(),'rmse':result['sourceProjectionRmse']}))


if __name__ == '__main__':
    main()
