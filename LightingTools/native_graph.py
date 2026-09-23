from pathlib import Path
import numpy as np,struct,json,collections,sys
class Graph:
 def __init__(self,filename,all_variants=False):
  self.all_variants=all_variants;self.b=filename if isinstance(filename,bytes) else Path(filename).read_bytes();self.base=self.u(12);self.roots=[self.ptr(28+i*4) for i in range(self.u(24))];self.meshes=[];self.bad=[];self.seen=set()
 def u(self,o):return struct.unpack_from('<I',self.b,o)[0]
 def sh(self,o):return struct.unpack_from('<h',self.b,o)[0]
 def ptr(self,o):
  v=self.u(o)-self.base
  if not 0<=v<len(self.b):raise ValueError(('ptr',hex(o),hex(v)))
  return v
 def walk(self,o,path=(),shift=None,tr=None,rot=None,lod=0):
  if o in path or len(path)>64:raise ValueError('cycle')
  shift=self.u(16) if shift is None else shift
  tr=np.zeros(3) if tr is None else tr;rot=np.eye(3) if rot is None else rot
  b=self.b;t=b[o];path=path+(o,)
  if t==8: return
  ident=(o,shift,tuple(tr),tuple(rot.ravel()))
  if ident in self.seen: return
  self.seen.add(ident)
  if t==0:
   vp=self.ptr(o+4);pp=self.ptr(o+12);count=self.u(o+16);polys=[]
   for i in range(count):
    n=(self.u(pp+12)>>16)&7;n=3 if n==3 else 4 if n==4 else 0
    if n:
     ix=struct.unpack_from('<'+str(n)+'h',b,pp+4);pts=np.array([struct.unpack_from('<hhh',b,vp+k*8) for k in ix],float)
     coords=(pts/(2**shift))@rot.T+tr
     polys.append((pp,coords))
    pp+=b[pp+2]*4
   self.meshes.append(dict(offset=o,root=self.roots.index(path[0]),path=path,type=t,shift=shift,tr=tr.tolist(),rotation=rot.tolist(),polys=polys,lod=lod))
   return
  count=0;start=0
  if t==1:count=b[o+20];start=24
  elif t==2:count=self.u(o+16) if self.all_variants else min(1,self.u(o+16));start=20
  elif t==3:count=b[o+11] if self.all_variants else min(1,b[o+11]);start=32
  elif t==4:
   count=b[o+18];start=20;tr=tr+rot@(np.array(struct.unpack_from('<iii',b,o+4))/2**shift)
  elif t==5:
   count=b[o+38];start=40;trans=np.array(struct.unpack_from('<iii',b,o+24));r=np.array(struct.unpack_from('<9h',b,o+4)).reshape(3,3)/4096
   tr=tr+rot@(trans/2**shift);rot=rot@r.T
  elif t==9:count=b[o+6] if self.all_variants else min(1,b[o+6]);start=16
  elif t==11:count=2;start=8
  elif t==12:
   count=struct.unpack_from('<H',b,o+6)[0];start=8;newshift=struct.unpack_from('<H',b,o+4)[0];shift=newshift
  elif t in (6,7,10,13):return
  else:raise ValueError(('type',t,hex(o)))
  for i in range(count):
   child=self.ptr(o+start+i*4)
   if t==2:child=self.ptr(child+8)
   try:self.walk(child,path,shift,tr,rot,lod+i if t==2 else lod)
   except Exception as e:self.bad.append((hex(o),str(e)))
 def all(self):
  for o in self.roots:
   try:self.walk(o)
   except Exception as e:self.bad.append((hex(o),str(e)))
