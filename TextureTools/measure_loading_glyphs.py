"""Source-only Joyride paragraph segmentation for editable glyph reconstruction.

Transcription labels original pixel regions; it does not render replacement text
or use a system font for game artwork. Boundaries are provisional until reviewed.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import find_objects, label


WORDS = [
    ['A','wide-open','run.'], ['If','you','get','turned'],
    ['around,','keep','the'], ['red','buoys','on','your'],
    ['right.','The','secret'], ['to','winning','is','not'],
    ['necessarily','the'], ['shortest','path,'],
    ['but','the','rhythm'], ['of','the','waves.'],
]


def widths(char):
    # Only initializes segmentation; no glyph outlines come from these priors.
    if char in 'ilI.,': return 2.2
    if char in 'ftr': return 3.5
    if char in 'mw': return 7.5
    if char in 'AT': return 6.3
    if char == '-': return 3.
    return 5.2


def segment(red,text,x0,x1):
    area = red[:,x0:x1]
    alpha = np.clip((area.astype(float)-32)/192,0,1)
    active = np.where(alpha.max(axis=0)>.18)[0]
    if not len(active):
        raise ValueError(f'Empty word region: {text}')
    left,right = int(active[0]),int(active[-1]+1)
    # One shared column boundary splits adjacent glyphs. A low boundary cost
    # prefers cuts through background/antialias valleys over solid strokes.
    crossing = np.minimum(alpha[:,:-1],alpha[:,1:]).sum(axis=0)
    expected = np.array([widths(c) for c in text])
    expected *= (right-left)/expected.sum()
    states = {left:(0.,[])}
    for i,char in enumerate(text):
        nxt = {}
        for start,(cost,cuts) in states.items():
            for stop in range(start+1,min(right,start+11)+1):
                if i==len(text)-1 and stop!=right: continue
                if right-stop<len(text)-i-1: continue
                width_cost = ((stop-start-expected[i])/max(.9,expected[i]*.25))**2
                cut_cost = 0 if stop==right else 2.5*crossing[stop-1]
                score = cost+width_cost+cut_cost
                if stop not in nxt or score<nxt[stop][0]:
                    nxt[stop] = (score,cuts+[(start+x0,stop+x0)])
        states = nxt
    if right not in states: raise ValueError(f'No segmentation: {text}')
    return states[right][1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]/'reports/build12'
    source = root/'menu-original-tim/ISLAND1/OVERV3.png'
    meta = json.loads(Path(str(source)+'.json').read_text())
    if hashlib.sha256(source.read_bytes()).hexdigest()!=meta['pngSha256']:
        raise ValueError('Source changed')
    image = Image.open(source).convert('RGB')
    data = np.array(image)
    args.output.mkdir(parents=True,exist_ok=False)
    glyphs=[]
    for line,words in enumerate(WORDS):
        y0,y1 = 97+line*10,107+line*10
        red = data[y0:y1,:,0]
        occupied = red[:,219:310].max(axis=0)>64
        runs = [(219+s[0].start,219+s[0].stop) for s in find_objects(label(occupied)[0])]
        measured = []
        for start,stop in runs:
            if measured and start-measured[-1][1]<3:
                measured[-1][1]=stop
            else:
                measured.append([start,stop])
        if len(measured)!=len(words):
            raise ValueError(f'Word count does not match source whitespace on line {line}')
        panel = image.crop((219,y0,310,y1)).resize((910,100),Image.Resampling.NEAREST)
        overlay = Image.new('RGB',(910,130),(0,16,24));overlay.paste(panel,(0,30))
        draw = ImageDraw.Draw(overlay)
        for word,(x0,x1) in zip(words,measured):
            bounds = segment(red,word,x0,x1)
            for char,(left,right) in zip(word,bounds):
                index=len(glyphs)
                glyphs.append({'id':index,'character':char,'line':line,'word':word,
                               'bounds':[left,y0,right,y1]})
                px=(left-219)*10
                draw.line((px,30,px,129),fill=(24,160,192),width=1)
                draw.text((px+2,3),char,fill=(255,224,0))
        overlay.save(args.output/f'line-{line:02d}-segmentation.png')
    # Repeated characters are shown as original samples, never as a new font.
    for char in sorted({g['character'] for g in glyphs}):
        examples=[g for g in glyphs if g['character']==char]
        sheet=Image.new('RGB',(120*min(8,len(examples)),144*((len(examples)+7)//8)),(0,16,24))
        draw=ImageDraw.Draw(sheet)
        for i,g in enumerate(examples):
            tile=image.crop(tuple(g['bounds']))
            tile=tile.resize((tile.width*12,tile.height*12),Image.Resampling.NEAREST)
            x,y=i%8*120,i//8*144
            sheet.paste(tile,(x,y+24));draw.text((x+2,y+3),str(g['id']),fill=(255,224,0))
        sheet.save(args.output/f'character-{ord(char):03d}-samples.png')
    (args.output/'glyphs.json').write_text(json.dumps({'source':meta,'glyphs':glyphs,
        'status':'provisional source-region annotations; review before fitting outlines'},indent=2)+'\n')
    print('glyphs',len(glyphs),'distinct',len({g['character'] for g in glyphs}))


if __name__ == '__main__':
    main()
