import numpy as np
def material(g,o,images):
 b=g.b;colors=b[o+1];op=b[o+19];n=4 if op&8 else 3;uv=o+16+4*colors
 if op<0x20 or op>0x3f or not op&4 or uv+n*4>o+b[o+2]*4:return None
 clut=g.sh(uv+2)&0xffff;page=g.sh(uv+6)&0xffff;depth=(page>>7)&3;scale=(4,2,1,1)[depth];px=(page&15)*64;py=((page>>4)&1)*256
 cx=(clut&63)*16;cy=clut>>6
 cand=[]
 for im in images:
  if im['depth']!=depth or (depth<2 and (cx,cy)!=tuple(im['source_clut'][:2])):continue
  u0=(im['source_placement'][0]-px)*scale;v0=im['source_placement'][1]-py;coords=np.array([(b[uv+i*4],b[uv+i*4+1]) for i in range(n)])
  tw=g.u(uv+n*4) if b[o]&128 and uv+n*4+4<=o+b[o+2]*4 else 0
  ands=np.array([255^((tw&31)*8),255^(((tw>>5)&31)*8)]);ors=np.array([((tw>>10)&(tw&31))*8,((tw>>15)&((tw>>5)&31))*8]);actual=(coords&ands)|ors
  if all(u0<=u<u0+im['width'] and v0<=v<v0+im['height'] for u,v in actual):cand.append((im,coords))
 return cand[0] if len(cand)==1 else None
