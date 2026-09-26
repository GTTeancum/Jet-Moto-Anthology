"""Offline editable JOYRIDE outline study traced from the original track atlas.

Glyph bounds, stems, counters and descender follow the source atlas. Curves
between raster samples are authoring estimates requiring visual review. This
is not a new typeface, an accepted full-screen replacement, or a deployment.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import map_coordinates


PATHS = [
    # Each glyph lists its outer contour followed by any original counter.
    ['M 4 1 L 7 1 L 7 10.5 L 4.5 13 L 1.5 13 L 4 10.5 Z'],
    ['M 15 1 C 21.67 1 21.67 11 15 11 C 8.33 11 8.33 1 15 1 Z',
     'M 15 3 C 17.67 3 17.67 9 15 9 C 12.33 9 12.33 3 15 3 Z'],
    ['M 20 1 L 24 1 L 26.75 5 L 29 1 L 31.5 1 L 28 6.5 L 28 11 L 25 11 L 25 6.5 Z'],
    ['M 32 1 L 37.5 1 C 40.5 1 42 2.3 42 4 C 42 5.8 40.8 6.4 39.5 6.6 '
     'L 42 11 L 38 11 L 36.5 7 L 35 7 L 35 11 L 32 11 Z',
     'M 35 3 L 37.5 3 C 39.5 3 39.5 5.5 37.5 5.5 L 35 5.5 Z'],
    ['M 43 1 L 46 1 L 46 11 L 43 11 Z'],
    ['M 49 1 L 54 1 C 60.67 1 60.67 11 54 11 L 49 11 Z',
     'M 52 3 L 54 3 C 57.33 3 57.33 9 54 9 L 52 9 Z'],
    ['M 61 1 L 69 1 L 69 3 L 64 3 L 64 5 L 67 5 L 67 7 L 64 7 L 64 9 L 69 9 L 69 11 L 61 11 Z'],
]


def points(path):
    words = iter(path.split())
    result = []
    for command in words:
        if command in ('M', 'L'):
            result.append(np.array([float(next(words)),float(next(words))]))
        elif command == 'C':
            p0 = result[-1]
            p1,p2,p3 = [np.array([float(next(words)),float(next(words))]) for _ in range(3)]
            for t in np.linspace(0,1,49)[1:]:
                result.append((1-t)**3*p0+3*(1-t)**2*t*p1+3*(1-t)*t*t*p2+t**3*p3)
        elif command != 'Z':
            raise ValueError(command)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fit',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    meta = json.loads(args.fit.read_text())
    args.output.mkdir(parents=True,exist_ok=False)
    svg = ['<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 71 15">',
           '<title>Unaccepted JOYRIDE outline study from original PICKTRAC/TRACKS0.TMS</title>',
           '<g fill="#d8d8d8" fill-rule="evenodd">']
    mask = Image.new('L',(71*64,15*64))
    draw = ImageDraw.Draw(mask)
    for glyph in PATHS:
        svg.append('<path d="'+' '.join(glyph)+'"/>')
        for i,path in enumerate(glyph):
            draw.polygon([tuple(p*64) for p in points(path)],fill=255 if i==0 else 0)
    svg += ['</g>','</svg>']
    (args.output/'joyride-title-source-trace.svg').write_text('\n'.join(svg)+'\n',encoding='ascii')
    # Render at the fitted original loading position, with explicit source
    # coordinate bounds. Background is diagnostic, not copied into the game.
    x0,y0,sx,sy,*_ = meta['parameters']
    yy,xx = np.mgrid[:128,:528]
    alpha = map_coordinates(np.array(mask,dtype=float)/255,
                            [((17+(yy+.5)/8-y0)/sy)*64-.5,
                             ((128+(xx+.5)/8-x0)/sx)*64-.5],order=1,mode='constant')
    rgb = np.rint(np.array([0,24,40])*(1-alpha[:,:,None])+216*alpha[:,:,None]).astype(np.uint8)
    Image.fromarray(rgb).save(args.output/'title-trace-inspection.png')
    Image.fromarray(rgb).resize((264,64),Image.Resampling.LANCZOS).save(args.output/'title-trace-4x.png')
    (args.output/'manifest.json').write_text(json.dumps({
        'sources':[meta['loadingSource'],meta['atlasSource']],
        'fittedPlacement':meta['parameters'][:4],
        'method':'editable source-guided outlines; no external font or neural inference',
        'status':'offline title study; not accepted or staged',
        'limitations':'estimated curves between original samples; paragraph and map still pending',
    },indent=2)+'\n',encoding='ascii')


if __name__ == '__main__':
    main()
