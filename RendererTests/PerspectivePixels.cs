using RecompOne.Runtime.Hle;

// Actual OpenGL readback versus analytical projective interpolation. Called for
// every backend at 1x, 2x and 4x; no screenshots or source-string assertions substitute for pixels.
static class PerspectivePixels
{
    public static void Run(GlCore core,string label,Action<bool,string> check)
    {
        void Check(bool condition,string name)=>check(condition,label+": "+name);
        ushort[] tex=new ushort[64*64];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)tex[y*64+x]=(ushort)((x/2)|((y/2)<<5)|(3<<10));
        core.WriteVram(640,256,64,64,tex);
        var flags=new PrimFlags{Textured=true,RawTexture=true,TPage=0x11A};
        HleVertex V(float x,float y,float u,float v,float z)=>new(){X=x,Y=y,U=u,V=v,Z=z,HasGteZ=true,R=128,G=128,B=128};
        HleVertex a=V(4,4,0,0,100),b=V(60,4,63,0,800),c=V(4,60,0,63,100);
        ushort[] Draw(HleVertex a,HleVertex b,HleVertex c,PrimFlags f)
        {
            core.FillRect(0,0,64,64,0);core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63});
            core.DrawTri(a,b,c,f);core.Flush();var result=new ushort[4096];core.ReadVram(0,0,64,64,result);return result;
        }
        ushort[] precise=Draw(a,b,c,flags);
        HleVertex Flat(HleVertex v){v.HasGteZ=false;return v;}
        ushort[] affine=Draw(Flat(a),Flat(b),Flat(c),flags);
        int Difference(ushort x,ushort y)=>Math.Max(Math.Abs((x&31)-(y&31)),Math.Max(Math.Abs(((x>>5)&31)-((y>>5)&31)),Math.Abs(((x>>10)&31)-((y>>10)&31))));
        int perspectiveErrors=0,affineErrors=0,samples=0;
        // ReadVram downsamples the internal target. A one-texel/5-bit-level
        // tolerance covers the selected subpixel at all three tested resolutions.
        for(int y=10;y<=34;y+=4)for(int x=10;x<=34;x+=4)
        {
            if(x+y>52)continue;samples++;
            double lb=(x+.5-4)/56,lc=(y+.5-4)/56,la=1-lb-lc;
            double inv=la/a.Z+lb/b.Z+lc/c.Z;
            int u=Math.Clamp((int)Math.Floor((lb*63/b.Z)/inv),0,63),v=Math.Clamp((int)Math.Floor((lc*63/c.Z)/inv),0,63);
            int au=Math.Clamp((int)Math.Floor(lb*63),0,63),av=Math.Clamp((int)Math.Floor(lc*63),0,63);
            if(Difference(precise[y*64+x],tex[v*64+u])>1)perspectiveErrors++;
            if(Difference(affine[y*64+x],tex[av*64+au])>1)affineErrors++;
        }
        Check(samples>20&&perspectiveErrors==0,$"perspective UV matches analytical 1/Z reference ({samples} samples, {perspectiveErrors} errors)");
        Check(affineErrors==0,"flat/2D UV matches affine reference");
        Check(!precise.AsSpan().SequenceEqual(affine)&&Difference(precise[20*64+20],affine[20*64+20])>=5,"unequal depths visibly differ from affine negative control");
        a.Z=100000;b.Z=800000;c.Z=100000;
        Check(Draw(a,b,c,flags).Zip(precise).Max(pair=>Difference(pair.First,pair.Second))<=1,"changing depth units does not change perspective mapping");
        a=V(-512,-200,0,0,100);b=V(1400,-200,63,0,6000);c=V(-512,700,0,63,800);
        var clipped=Draw(a,b,c,flags);var clipFlat=Draw(Flat(a),Flat(b),Flat(c),flags);
        Check(clipped.All(pixel=>pixel!=0)&&clipFlat.All(pixel=>pixel!=0),"offscreen vertices retain complete viewport coverage with perspective correction");
        int clipErrors=0;
        for(int y=8;y<56;y+=8)for(int x=8;x<56;x+=8)
        {
            double lb=(x+.5+512)/1912,lc=(y+.5+200)/900,la=1-lb-lc,inv=la/a.Z+lb/b.Z+lc/c.Z;
            int u=Math.Clamp((int)Math.Floor((lb*63/b.Z)/inv),0,63),v=Math.Clamp((int)Math.Floor((lc*63/c.Z)/inv),0,63);
            if(Difference(clipped[y*64+x],tex[v*64+u])>1)clipErrors++;
        }
        Check(clipErrors==0,$"clipped perspective UV still matches 1/Z reference ({clipErrors} errors)");
        a=V(4,4,0,0,100);b=V(60,4,63,0,800);c=V(4,60,0,63,100);
        a.Z=b.Z=c.Z=64;
        Check(Draw(a,b,c,flags).AsSpan().SequenceEqual(affine),"front-facing equal-depth geometry is unchanged");
        a.Z=100;b.Z=800;c.Z=100;
        foreach(int corner in new[]{0,1,2})foreach(float bad in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
        {
            var aa=a;var bb=b;var cc=c;if(corner==0)aa.Z=bad;else if(corner==1)bb.Z=bad;else cc.Z=bad;
            Check(Draw(aa,bb,cc,flags).AsSpan().SequenceEqual(affine),$"invalid W={bad} corner={corner}: entire triangle stays safe, no missing/stretching wedge");
        }
        for(int corner=0;corner<3;corner++)
        {
            var aa=a;var bb=b;var cc=c;if(corner==0)aa.HasGteZ=false;else if(corner==1)bb.HasGteZ=false;else cc.HasGteZ=false;
            Check(Draw(aa,bb,cc,flags).AsSpan().SequenceEqual(affine),$"missing depth flag corner={corner}: consistent whole-triangle fallback");
        }
        var shade=new PrimFlags{Gouraud=true};a.R=a.G=a.B=64;b.R=b.G=b.B=224;c.R=c.G=c.B=64;
        var depthShade=Draw(a,b,c,shade);var flatShade=Draw(Flat(a),Flat(b),Flat(c),shade);
        Check(depthShade.Zip(flatShade).Max(pair=>Difference(pair.First,pair.Second))<=1,"original screen-linear Gouraud lighting is preserved with perspective UV");
        a=V(4,56,0,63,100);b=V(60,56,63,63,100);c=V(25,8,0,0,400);var d=V(39,8,63,0,400);
        ushort[] Quad(bool diagonal,bool depth)
        {
            core.FillRect(0,0,64,64,0);core.SetDrawEnv(new HleDrawEnv{ClipX1=63,ClipY1=63});
            HleVertex A=depth?a:Flat(a),B=depth?b:Flat(b),C=depth?c:Flat(c),D=depth?d:Flat(d);
            if(diagonal){core.DrawTri(A,B,D,flags);core.DrawTri(A,D,C,flags);}
            else {core.DrawTri(A,B,C,flags);core.DrawTri(B,D,C,flags);}
            core.Flush();var p=new ushort[4096];core.ReadVram(0,0,64,64,p);return p;
        }
        var q0=Quad(false,true);var q1=Quad(true,true);var f0=Quad(false,false);var f1=Quad(true,false);
        int covered=0,seamErrors=0,affineChanges=0;
        for(int i=0;i<q0.Length;i++)if(q0[i]!=0&&q1[i]!=0){covered++;if(Difference(q0[i],q1[i])>1)seamErrors++;if(Difference(f0[i],f1[i])>2)affineChanges++;}
        Check(covered>1000&&seamErrors==0,$"planar quad has no diagonal-dependent texture warp ({covered} covered pixels, {seamErrors} seam errors)");
        Check(affineChanges>100,"changing diagonal in affine negative control causes measurable texture warp");
    }
}
