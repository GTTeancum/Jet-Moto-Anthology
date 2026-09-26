"""Offline typeface identification against original Joyride sample geometry.

This compares possible outlines; it does not authorize a font replacement or
stage artwork. A match score alone cannot establish the original typeface.
"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy.ndimage import map_coordinates
from scipy.optimize import least_squares

LINES = ['A wide-open run.', 'If you get turned', 'around, keep the',
         'red buoys on your', 'right. The secret', 'to winning is not',
         'necessarily the', 'shortest path,', 'but the rhythm', 'of the waves.']
ROOT = Path(__file__).resolve().parents[1]


def main():
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--fonts', nargs='+', default=['arialbd.ttf', 'ARIALNB.TTF', 'ariblk.ttf',
                        'tahomabd.ttf', 'verdanab.ttf', 'trebucbd.ttf', 'arial.ttf'])
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    source = np.array(Image.open(ROOT/'reports/build12/menu-original-tim/ISLAND1/OVERV3.png'))
    target = source[96:196, 219:310, 0].astype(float).reshape(10, 10, 91)
    yy, xx = np.mgrid[:10, :91]
    # Four-by-four area samples in each original pixel; no hinted small-font
    # render. Compare underlying outlines with the baked antialiasing evidence.
    sub = (np.arange(4)+.5)/4
    xgrid = (xx[..., None, None]+sub[None, None, :, None])+219
    ygrid = yy[..., None, None]+sub[None, None, None, :]+96
    rows = []
    for name in args.fonts:
        path = Path('C:/Windows/Fonts')/name
        if not path.exists():
            continue
        font = ImageFont.truetype(str(path), 200)
        masks = []
        for line in LINES:
            canvas = Image.new('L', (2200, 300))
            ImageDraw.Draw(canvas).text((20, 200), line, font=font, fill=255, anchor='ls')
            masks.append(np.array(canvas, dtype=float)/255)

        def predict(v):
            x0, baseline, sx, sy, gain, bg = v
            cx = np.broadcast_to((xgrid-x0)/sx+19.5, (10,91,4,4))
            cy = np.broadcast_to((ygrid-baseline)/sy+199.5, (10,91,4,4))
            return np.array([map_coordinates(m, [cy, cx], order=1, mode='constant').mean(axis=(2,3))*gain+bg for m in masks])

        def residual(v):
            return (predict(v)-target).ravel()
        initial_sx = 77 / font.getlength(LINES[0])
        fits = [least_squares(residual, [223,baseline,initial_sx*k,.045,224,8],
                             bounds=([221,102,.015,.02,150,0],[225,106,.15,.09,255,32]),
                             x_scale='jac', max_nfev=150)
                for baseline in [103.5,104.0] for k in [.97,1.,1.03]]
        fit = min(fits, key=lambda f: np.mean(f.fun**2))
        prediction = predict(fit.x)
        rmse = float(np.sqrt(np.mean((prediction-target)**2)))
        row = {'font': name, 'parameters': fit.x.tolist(), 'rmse': rmse}
        rows.append(row)
        print(json.dumps(row), flush=True)
        Image.fromarray(np.rint(prediction.reshape(100,91)).clip(0,255).astype(np.uint8)).resize(
            (364,400), Image.Resampling.NEAREST).save(args.output/(name+'.png'))
    (args.output/'scores.json').write_text(json.dumps(sorted(rows,key=lambda r:r['rmse']),indent=2)+'\n')


if __name__ == '__main__':
    main()
