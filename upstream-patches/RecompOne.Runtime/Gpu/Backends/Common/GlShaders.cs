using Silk.NET.OpenGL;

namespace RecompOne.Runtime.Hle;

internal static class GlShaders
{
    public const string FullscreenVs = """
                                       #version 330 core
                                       layout(location = 0) in vec2 aPos;
                                       out vec2 vUv;
                                       void main() {
                                           vUv = aPos * 0.5 + 0.5;
                                           gl_Position = vec4(aPos, 0.0, 1.0);
                                       }
                                       """;

    public const string PresentFs = """
                                    #version 330 core
                                    in vec2 vUv;
                                    uniform sampler2D uVram;
                                    uniform vec2 uOrigin;
                                    uniform vec2 uSize;
                                    uniform vec2 uTexSize;
                                    out vec4 oColor;
                                    void main() {
                                        vec2 t = (uOrigin + vUv * uSize) / uTexSize;
                                        oColor = vec4(texture(uVram, t).rgb, 1.0);
                                    }
                                    """;

    public const string Present24Fs = """
                                      #version 330 core
                                      in vec2 vUv;
                                      uniform sampler2D uVram;
                                      uniform vec2 uOrigin;
                                      uniform vec2 uSize;
                                      uniform int uScale;
                                      out vec4 oColor;

                                      int u5(float f) { return int(floor(f * 31.0 + 0.5)); }
                                      int texel16(int lin) {
                                          vec4 p = texelFetch(uVram, ivec2((lin & 1023) * uScale, ((lin >> 10) & 511) * uScale), 0);
                                          return u5(p.r) | (u5(p.g) << 5) | (u5(p.b) << 10) | (int(ceil(p.a)) << 15);
                                      }
                                      int byteAt(int b) {
                                          int t = texel16(b >> 1);
                                          return (b & 1) == 0 ? (t & 0xff) : ((t >> 8) & 0xff);
                                      }
                                      void main() {
                                          int px = int(floor(vUv.x * uSize.x));
                                          int py = int(floor(vUv.y * uSize.y));
                                          int ty = int(uOrigin.y) + py;
                                          int base = (ty * 1024 + int(uOrigin.x)) * 2 + px * 3;
                                          oColor = vec4(float(byteAt(base)) / 255.0, float(byteAt(base + 1)) / 255.0,
                                                        float(byteAt(base + 2)) / 255.0, 1.0);
                                      }
                                      """;

    public const string PrimVs = """
                                 #version 330 core
                                 layout(location = 0) in vec2  inPos;
                                 layout(location = 1) in vec3  inColorF;
                                 layout(location = 2) in float inClutF;
                                 layout(location = 3) in float inTexpageF;
                                 layout(location = 4) in vec2  inUV;
                                 layout(location = 5) in float inW;
layout(location=6) in vec4 inWorldQ;
layout(location=7) in vec4 inSurface;
out vec4 vWorldQ;
flat out vec4 vSurface;

                                 noperspective out vec4 vColor;
                                 out vec3 vUVQ;
                                 flat out ivec2 clutBase;
                                 flat out ivec2 pageBase;
                                 flat out int   texMode;
                                 flat out int   vRepClut;

                                 uniform vec2 uVertexOffset;
                                 uniform vec2 uPosBias;
                                 uniform vec2 uFbInv;

                                 void main() {
 vWorldQ=inWorldQ;vSurface=inSurface;
                                     vec2 p = (inPos + uVertexOffset + uPosBias) * uFbInv - 1.0;
                                     // Keep screen-space clipping independent of camera depth.
                                     // Interpolate U/Z, V/Z and 1/Z, then divide per fragment.
                                     gl_Position = vec4(p, 0.0, 1.0);

                                     vUVQ = vec3(inUV, 1.0) / inW;
                                     int inClut = int(inClutF + 0.5);
                                     int inTexpage = int(inTexpageF + 0.5);

                                     vColor = vec4(inColorF, 0.0) / 255.0;
                                     vRepClut = (inTexpage >> 12) & 1;

                                     if ((inTexpage & 0x8000) != 0) {
                                         texMode = 4;
                                     } else if ((inTexpage & 0x4000) != 0) {
                                         texMode = 5;
                                     } else if ((inTexpage & 0x2000) != 0) {
                                         texMode = 6;
                                     } else {
                                         texMode = (inTexpage >> 7) & 3;
                                         pageBase = ivec2((inTexpage & 0xf) * 64, ((inTexpage >> 4) & 1) * 256);
                                         clutBase = ivec2((inClut & 0x3f) * 16, (inClut >> 6) & 0x1ff);
                                     }
                                 }
""";

    public const string PrimFs = """
                                 #version 330 core
                                 noperspective in vec4 vColor;
                                 in vec3 vUVQ;
                                 flat in ivec2 clutBase;
                                 flat in ivec2 pageBase;
                                 flat in int   texMode;
                                 flat in int   vRepClut;

                                 layout(location = 0, index = 0) out vec4 FragColor;
                                 layout(location = 0, index = 1) out vec4 BlendColor;

                                 uniform sampler2D uVram;
                                 uniform sampler2D uDest;
                                 uniform sampler2D uExtTex;
                                 uniform sampler2D uRepTex;
                                 uniform sampler2D uRepClut;
                                 uniform vec4  uRepRect;
                                    uniform float uRepCoverage;
                                 uniform float uRepClutCount;
                                 uniform ivec4 uTexWindow;
                                 uniform vec4  uBlend;
                                 uniform vec4  uBlendOpaque = vec4(1.0, 1.0, 1.0, 0.0);
                                 uniform float uSetMask;
                                 uniform int   uCheckMask;
                                 uniform int   uScale;
                                 uniform vec2  uPosBias;

                                 int u5(float f) { return int(floor(f * 31.0 + 0.5)); }
                                 vec4 fetch(ivec2 c) { return texelFetch(uVram, (c & ivec2(1023, 511)) * uScale, 0); }
                                 int fetch16(ivec2 c) {
                                     vec4 p = fetch(c);
                                     return u5(p.r) | (u5(p.g) << 5) | (u5(p.b) << 10) | (int(ceil(p.a)) << 15);
                                 }
                                 vec3 quant5(ivec3 c8) {
                                     // Jet Moto: retain PS1 color precision, with no spatial dither.
                                     return vec3(min(c8 >> 3, 31)) / 31.0;
                                 }

                                 in vec4 vWorldQ;
flat in vec4 vSurface;
// Native, original-geometry-bound lighting. Unmarked primitives are bit-identical.
uniform sampler2D uWorldHeight;
uniform sampler2D uWaterDetail;
uniform sampler2D uRiderShadow;
uniform vec4 uWorldBounds;
uniform vec2 uWorldHeightRange;
uniform vec3 uWorldLight;
uniform vec3 uWorldEye;
uniform float uWorldTime;
uniform float uEffectTime;
uniform vec3 uWaterTint;
float originalHeight(vec2 p) {
    vec2 uv=(p-uWorldBounds.xy)/uWorldBounds.zw;
    if(uv.x<0.0||uv.y<0.0||uv.x>1.0||uv.y>1.0)return -100000.0;
    vec4 h=texture(uWorldHeight,uv);
    if(h.b<0.05)return -100000.0;
    return uWorldHeightRange.x+dot(h.rg,vec2(65280.0,255.0))/65535.0*uWorldHeightRange.y;
}
// Both native water layers share a world-anchored, mipmapped material field.
vec3 texturedWater(vec3 source,vec3 p,vec3 L,float sun,float ao) {
    vec2 uv=p.xy/48.0;
    mat2 turn=mat2(.80,.60,-.60,.80);
    vec4 broad=texture(uWaterDetail,uv+vec2(.009,-.006)*uWorldTime);
    vec2 bend=(broad.rg*2.0-1.0)*.06;
    vec4 crossing=texture(uWaterDetail,turn*uv*2.73+bend+vec2(-.013,.008)*uWorldTime);
    vec4 fine=texture(uWaterDetail,uv*7.31-bend+vec2(.018,.011)*uWorldTime);
    vec2 slope=(broad.rg*2.0-1.0)*.65
        +mat2(.80,-.60,.60,.80)*(crossing.rg*2.0-1.0)*.38
        +(fine.rg*2.0-1.0)*.14;
    vec3 N=normalize(vec3(-slope,1.0));
    vec3 V=normalize(uWorldEye-p);
    float height=(broad.a-.5)*1.4+(crossing.a-.5)*.9+(fine.a-.5)*.35;
    float mottling=(broad.b-.5)*.65;
    float body=clamp(.80+mottling+height*.90,.35,1.12);
    float ridges=smoothstep(.02,.22,height);
    float broken=ridges*smoothstep(.18,.78,crossing.b+fine.a*.25);
    float grazing=pow(1.0-clamp(dot(N,V),0.0,1.0),4.0);
    float glint=pow(max(dot(N,normalize(L+V)),0.0),24.0)*sun;
    float illumination=(.80+.20*max(dot(N,L),0.0)*sun)*(1.0-.05*ao);
    // Reflection contrast follows the ripple crests; color stays in the source hue.
    float reflection=broken*(.10+.16*grazing+.16*glint);
    return clamp(source*(body*illumination+reflection),0.0,1.0);
}
vec3 lightOriginalSurface(vec3 color) {
    if(vSurface.w>4.1 && vSurface.w<4.5 && vWorldQ.w<=0.0)discard;
    if(vWorldQ.w<=0.0)return color;
    if(vSurface.w>3.5 && vSurface.w<4.5 && vWorldQ.w<0.00001)
        return (dot(uWaterTint,uWaterTint)>0.0001 ? uWaterTint : color)*0.65;
    if(vSurface.w<0.5 || vWorldQ.w<=0.0)return color;
    vec3 p=vWorldQ.xyz/vWorldQ.w;
    vec3 n=normalize(vSurface.xyz);
    vec3 L=normalize(uWorldLight);
    float diffuse=max(dot(n,L),0.0);
    if(vSurface.w>2.5 && vSurface.w<3.5){
        float riderLight=0.70+0.24*diffuse+0.06*max(dot(n,normalize(uWorldEye-p)),0.0);
        return clamp(color*riderLight,0.0,1.0);
    }
    vec3 lightRight=normalize(cross(vec3(0.0,0.0,1.0),L));
    vec3 lightUp=cross(L,lightRight);
    vec3 relative=p-uWorldEye;
    vec2 shadowUv=vec2(dot(relative,lightRight),dot(relative,lightUp))/128.0+0.5;
    float receiverDepth=dot(relative,L)/512.0+0.5;
    float shadow=0.;
    // Native rider planes and rasterized silhouettes have different precision.
    // Receive rider-cast shadows on the scene only; riders still receive sunlight
    // without unstable silhouette or height-field receiver darkening.
    if(vSurface.w<2.5 && shadowUv.x>0.001 && shadowUv.y>0.001 && shadowUv.x<0.999 && shadowUv.y<0.999){
        for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++){
            vec4 caster=texture(uRiderShadow,shadowUv+(vec2(float(sx),float(sy))-.5)/1024.0);
            float casterDepth=dot(caster.rg,vec2(65280.0,255.0))/65535.0;
            shadow+=.25*step(.5,caster.b)*step(receiverDepth+.0003,casterDepth);
        }
    }
    float distanceAlong=2.0;
    for(int k=0;k<9;k++){
        vec3 samplePoint=p+L*distanceAlong;
        shadow=max(shadow,smoothstep(1.5,4.0,originalHeight(samplePoint.xy)-samplePoint.z));
        distanceAlong*=2.0;
    }
    float ao=0.0;
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy+vec2(5.0,0.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy-vec2(5.0,0.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy+vec2(0.0,5.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy-vec2(0.0,5.0))-p.z);
    float sun=1.0-shadow;
    if(vSurface.w>3.5 && vSurface.w<4.5)
        return texturedWater(dot(uWaterTint,uWaterTint)>0.0001 ? uWaterTint : color,p,L,sun,ao);
    if(vSurface.w>1.5 && vSurface.w<2.5)
        return texturedWater(clamp(color,0.0,1.0),p,L,sun,ao);
    float ambient=0.38+0.16*max(n.z,0.0);
    return clamp(color*(ambient*(1.0-0.10*ao)+0.66*diffuse*sun),0.0,1.0);
}

vec4 sampleCoverageEffect(vec2 t) {
    vec4 base=texture(uRepTex,t);
    if(uRepCoverage>1.5) {
        float alpha=0.0;
        float aa=max(length(fwidth(t))*.5,.001);
        for(int i=0;i<24;i++) {
            float seed=fract(sin(float(i)*17.13+4.7)*43758.5453);
            float age=fract(uEffectTime*(.68+seed*.24)+float(i)/24.0);
            float side=fract(seed*13.7)*2.0-1.0;
            vec2 center=vec2(.5+side*age*.46,.96-age*1.55+age*age*.62);
            vec2 d=(t-center)*vec2(1.0,.50);
            float radius=.003+seed*.003;
            float fade=smoothstep(0.0,.12,age)*(1.0-smoothstep(.65,1.0,age));
            alpha=max(alpha,(1.0-smoothstep(radius,radius+aa,length(d)))*fade*.55);
        }
        vec3 tint=texture(uRepTex,vec2(.5,.72)).rgb;
        return vec4(tint,alpha);
    }
    float motion=smoothstep(0.02,0.20,uEffectTime);
    float phase=fract(uEffectTime*.85);
    float phase2=fract(phase+.5);
    // Cross-faded transport makes droplets travel up the plume, with no reset flash.
    float weight=sin(phase*3.14159265); weight*=weight;
    float weight2=1.0-weight;
    vec2 lateral=vec2(.035*sin(t.y*9.0+uEffectTime*1.7),0.0);
    vec2 q1=t+lateral+vec2(0.0,(phase-.5)*.48);
    vec2 q2=t-lateral+vec2(0.0,(phase2-.5)*.48);
    vec4 a=texture(uRepTex,q1);
    vec4 b=texture(uRepTex,q2);
    float edge=smoothstep(0.0,.06,t.x)*smoothstep(0.0,.06,1.0-t.x)
              *smoothstep(0.0,.04,t.y)*smoothstep(0.0,.04,1.0-t.y);
    float alpha=a.a*weight+b.a*weight2;
    vec3 rgb=(a.rgb*a.a*weight+b.rgb*b.a*weight2)/max(alpha,.0001);
    return mix(base,vec4(rgb,alpha*edge),motion);
}

void main() {
                                     vec2 vUV = vUVQ.xy / vUVQ.z;
                                     if (uCheckMask != 0 && texelFetch(uDest, ivec2(gl_FragCoord.xy), 0).a >= 0.5) discard;

                                     if (texMode == 4) {
                                         FragColor = vec4(lightOriginalSurface(quant5(ivec3(vColor.rgb * 255.0 + 0.5))), uSetMask);
                                         BlendColor = uBlend;
                                         return;
                                     }

                                     if (texMode == 5) {
                                         vec4 img = texture(uExtTex, vUV);
                                         if (img.a < 0.5) discard;
                                         ivec3 e8 = (ivec3(img.rgb * 255.0 + 0.5) * ivec3(vColor.rgb * 255.0 + 0.5)) >> 7;
                                         FragColor = vec4(lightOriginalSurface(quant5(e8)), uSetMask);
                                         BlendColor = uBlend;
                                         return;
                                     }

                                     int rawU = dFdx(vUV.x) < 0.0 ? int(ceil(vUV.x - 0.0001)) : int(floor(vUV.x + 0.0001));
                                     int rawV = dFdy(vUV.y) < 0.0 ? int(ceil(vUV.y - 0.0001)) : int(floor(vUV.y + 0.0001));
                                     ivec2 uv = (ivec2(rawU, rawV) & uTexWindow.xy) | uTexWindow.zw;
                                     uv &= ivec2(0xff);

                                     if (texMode == 6) {
                                         // Apply bitwise PS1 texture-window semantics to the integer
                                         // texel address, retaining fractional coordinates for native 4x art.
                                         vec2 whole = floor(vUV);
                                         ivec2 mapped = ((ivec2(whole) & uTexWindow.xy) | uTexWindow.zw) & ivec2(255);
                                         vec2 fuv = vec2(mapped) + fract(vUV);
                                         vec2 t = (fuv - uRepRect.xy) / uRepRect.zw;
                                         if (uRepCoverage > 0.5) {
                                             vec4 img = sampleCoverageEffect(t);
                                             if (img.a <= 0.0) discard;
                                             ivec3 e8 = (ivec3(img.rgb * 255.0 + 0.5) * ivec3(vColor.rgb * 255.0 + 0.5)) >> 7;
                                             FragColor = vec4(lightOriginalSurface(quant5(e8)), uSetMask);
                                             BlendColor = vec4(uBlend.rgb * img.a, 1.0 - img.a + uBlend.a * img.a);
                                             return;
                                         }
                                         vec4 img = texture(uRepTex, t);
                                         if (img.a < 0.5) discard;
                                         ivec3 e8 = (ivec3(img.rgb * 255.0 + 0.5) * ivec3(vColor.rgb * 255.0 + 0.5)) >> 7;
                                         float stp = img.a < 0.95 ? 1.0 : 0.0;
                                         FragColor = vec4(lightOriginalSurface(quant5(e8)), max(stp, uSetMask));
                                         BlendColor = stp > 0.5 ? uBlend : uBlendOpaque;
                                         return;
                                     }

                                     vec4 texel;

                                     if (texMode == 0) {
                                         int s = fetch16(ivec2(pageBase.x + (uv.x >> 2), pageBase.y + uv.y));
                                         int idx = (s >> ((uv.x & 3) << 2)) & 0xf;
                                         texel = vRepClut != 0
                                             ? texture(uRepClut, vec2((float(idx) + 0.5) / uRepClutCount, 0.5))
                                             : fetch(ivec2(clutBase.x + idx, clutBase.y));
                                     } else if (texMode == 1) {
                                         int s = fetch16(ivec2(pageBase.x + (uv.x >> 1), pageBase.y + uv.y));
                                         int idx = (s >> ((uv.x & 1) << 3)) & 0xff;
                                         texel = vRepClut != 0
                                             ? texture(uRepClut, vec2((float(idx) + 0.5) / uRepClutCount, 0.5))
                                             : fetch(ivec2(clutBase.x + idx, clutBase.y));
                                     } else {
                                         texel = fetch(ivec2(pageBase.x + uv.x, pageBase.y + uv.y));
                                     }

                                     if (vRepClut != 0 && texMode != 2) {
                                         if (texel.a < 0.5) discard;
                                         ivec3 e8 = (ivec3(texel.rgb * 255.0 + 0.5) * ivec3(vColor.rgb * 255.0 + 0.5)) >> 7;
                                         float stp = texel.a < 0.95 ? 1.0 : 0.0;
                                         FragColor = vec4(lightOriginalSurface(quant5(e8)), max(stp, uSetMask));
                                         BlendColor = stp > 0.5 ? uBlend : uBlendOpaque;
                                         return;
                                     }

                                     if (texel.rgb == vec3(0.0) && texel.a < 0.5) discard;
                                     ivec3 t8 = ivec3(texel.rgb * 31.0 + 0.5) << 3;
                                     ivec3 c8 = (t8 * ivec3(vColor.rgb * 255.0 + 0.5)) >> 7;
                                     FragColor = vec4(lightOriginalSurface(quant5(c8)), max(texel.a, uSetMask));
                                     BlendColor = texel.a >= 0.5 ? uBlend : uBlendOpaque;
                                 }
""";

    public const string FullscreenVs120 = """
                                          #version 120
                                          attribute vec2 aPos;
                                          varying vec2 vUv;
                                          void main() {
                                              vUv = aPos * 0.5 + 0.5;
                                              gl_Position = vec4(aPos, 0.0, 1.0);
                                          }
                                          """;

    public const string PresentFs120 = """
                                       #version 120
                                       varying vec2 vUv;
                                       uniform sampler2D uVram;
                                       uniform vec2 uOrigin;
                                       uniform vec2 uSize;
                                       uniform vec2 uTexSize;
                                       void main() {
                                           vec2 t = (uOrigin + vUv * uSize) / uTexSize;
                                           gl_FragColor = vec4(texture2D(uVram, t).rgb, 1.0);
                                       }
                                       """;

    public const string Present24Fs120 = """
                                         #version 120
                                         varying vec2 vUv;
                                         uniform sampler2D uVram;
                                         uniform vec2 uOrigin;
                                         uniform vec2 uSize;
                                         uniform vec2 uVramSize;
                                         uniform float uScale;

                                         float u5(float f) { return floor(f * 31.0 + 0.5); }

                                         float texel16(float lin) {
                                             float x = mod(lin, 1024.0);
                                             float y = floor(lin / 1024.0);
                                             vec2 uv = (vec2(x, y) * uScale + 0.5) / uVramSize;
                                             vec4 p = texture2D(uVram, uv);
                                             return u5(p.r) + u5(p.g) * 32.0 + u5(p.b) * 1024.0 + ceil(p.a) * 32768.0;
                                         }

                                         float byteAt(float b) {
                                             float t = texel16(floor(b * 0.5));
                                             return mod(b, 2.0) < 0.5 ? mod(t, 256.0) : floor(t / 256.0);
                                         }

                                         void main() {
                                             float px = floor(vUv.x * uSize.x);
                                             float py = floor(vUv.y * uSize.y);
                                             float ty = uOrigin.y + py;
                                             float base = (ty * 1024.0 + uOrigin.x) * 2.0 + px * 3.0;
                                             gl_FragColor = vec4(byteAt(base) / 255.0, byteAt(base + 1.0) / 255.0, byteAt(base + 2.0) / 255.0, 1.0);
                                         }
                                         """;

    public const string BlitVs120 = """
                                    #version 120
                                    attribute vec2 aPos;
                                    uniform vec4 uDstRect;
                                    uniform vec4 uSrcRect;
                                    varying vec2 vSrc;
                                    void main() {
                                        vec2 unit = aPos * 0.5 + 0.5;
                                        vSrc = uSrcRect.xy + unit * uSrcRect.zw;
                                        vec2 p = uDstRect.xy + unit * uDstRect.zw;
                                        gl_Position = vec4(p * 2.0 - 1.0, 0.0, 1.0);
                                    }
                                    """;

    public const string BlitFs120 = """
                                    #version 120
                                    varying vec2 vSrc;
                                    uniform sampler2D uSrc;
                                    void main() { gl_FragColor = texture2D(uSrc, vSrc); }
                                    """;

    public const string PrimVs120 = """
                                    #version 120
                                    attribute vec2  inPos;
                                    attribute float inW;
attribute vec4 inWorldQ;
attribute vec4 inSurface;
varying vec4 vWorldQ;
varying vec4 vSurface;
                                    attribute vec3  inColorF;
                                    attribute float inClutF;
                                    attribute float inTexpageF;
                                    attribute vec2  inUV;

                                    varying vec4  vColor;
                                    varying vec3  vUVQ;
                                    varying vec2  vClutBase;
                                    varying vec2  vPageBase;
                                    varying float vTexMode;
                                    varying float vRepClut;

                                    uniform vec2 uVertexOffset;
                                    uniform vec2 uPosBias;
                                    uniform vec2 uFbInv;

                                    float bitAt(float v, float bit) { return floor(mod(v / bit, 2.0)); }

                                    void main() {
 vWorldQ=inWorldQ;vSurface=inSurface;
                                        vec2 p = (inPos + uVertexOffset + uPosBias) * uFbInv - 1.0;
                                        // Keep screen-space clipping independent of camera depth.
                                     // Interpolate U/Z, V/Z and 1/Z, then divide per fragment.
                                     gl_Position = vec4(p, 0.0, 1.0);

                                        float tp = floor(inTexpageF + 0.5);
                                        float clut = floor(inClutF + 0.5);

                                        vColor = vec4(inColorF / 255.0, 0.0);
                                        vRepClut = bitAt(tp, 4096.0);
                                        vUVQ = vec3(inUV, 1.0) / inW;
                                        vClutBase = vec2(0.0);
                                        vPageBase = vec2(0.0);

                                        if (bitAt(tp, 32768.0) > 0.5) {
                                            vTexMode = 4.0;
                                        } else if (bitAt(tp, 16384.0) > 0.5) {
                                            vTexMode = 5.0;
                                        } else if (bitAt(tp, 8192.0) > 0.5) {
                                            vTexMode = 6.0;
                                        } else {
                                            vTexMode = floor(mod(tp / 128.0, 4.0));
                                            vPageBase = vec2(mod(tp, 16.0) * 64.0, bitAt(tp, 16.0) * 256.0);
                                            vClutBase = vec2(mod(clut, 64.0) * 16.0, mod(floor(clut / 64.0), 512.0));
                                        }
                                    }
""";

    //gl 2.1 has no dual source blending =/ has to do by hand
    public const string PrimFs120 = """
                                    #version 120
                                    varying vec4  vColor;
                                    varying vec3  vUVQ;
                                    varying vec2  vClutBase;
                                    varying vec2  vPageBase;
                                    varying float vTexMode;
                                    varying float vRepClut;

                                    uniform sampler2D uVram;
                                    uniform sampler2D uDest;
                                    uniform sampler2D uExtTex;
                                    uniform sampler2D uRepTex;
                                    uniform sampler2D uRepClut;
                                    uniform vec4  uRepRect;
                                    uniform float uRepCoverage;
                                    uniform float uRepClutCount;
                                    uniform vec4  uTexWindow;
                                    uniform float uSetMask;
                                    uniform float uCheckMask;
                                    uniform float uScale;
                                    uniform vec2  uPosBias;
                                    uniform vec2  uVramSize;
                                    uniform vec2  uDestSize;
                                    uniform float uSemiTrans;
                                    uniform float uBlendMode;

                                    float u5(float f) { return floor(f * 31.0 + 0.5); }

                                    // GLSL 1.20 has no bitwise integer operators. Reconstruct exactly
                                    // eight bits; modulo(mask+1) is wrong for non-contiguous windows.
                                    float windowByte(float v, float andMask, float orMask) {
                                        float result = 0.0;
                                        for (int bit = 0; bit < 8; ++bit) {
                                            float weight = exp2(float(bit));
                                            float vb = mod(floor(v / weight), 2.0);
                                            float ab = mod(floor(andMask / weight), 2.0);
                                            float ob = mod(floor(orMask / weight), 2.0);
                                            result += max(vb * ab, ob) * weight;
                                        }
                                        return result;
                                    }

                                    vec4 fetch(vec2 c) {
                                        vec2 w = vec2(mod(c.x, 1024.0), mod(c.y, 512.0));
                                        return texture2D(uVram, (w * uScale + 0.5) / uVramSize);
                                    }

                                    float fetch16(vec2 c) {
                                        vec4 p = fetch(c);
                                        return u5(p.r) + u5(p.g) * 32.0 + u5(p.b) * 1024.0 + ceil(p.a) * 32768.0;
                                    }

                                    vec3 quant5(vec3 c8) {
                                        // Same undithered conversion as the modern shader.
                                        return min(floor(c8 / 8.0), 31.0) / 31.0;
                                    }

                                    vec3 blendWith(vec3 src, vec3 dst) {
                                        if (uBlendMode < 0.5) return (dst + src) * 0.5;
                                        if (uBlendMode < 1.5) return dst + src;
                                        if (uBlendMode < 2.5) return dst - src;
                                        return dst + src * 0.25;
                                    }

                                    varying vec4 vWorldQ;
varying vec4 vSurface;
// Native, original-geometry-bound lighting. Unmarked primitives are bit-identical.
uniform sampler2D uWorldHeight;
uniform sampler2D uWaterDetail;
uniform sampler2D uRiderShadow;
uniform vec4 uWorldBounds;
uniform vec2 uWorldHeightRange;
uniform vec3 uWorldLight;
uniform vec3 uWorldEye;
uniform float uWorldTime;
uniform float uEffectTime;
uniform vec3 uWaterTint;
float originalHeight(vec2 p) {
    vec2 uv=(p-uWorldBounds.xy)/uWorldBounds.zw;
    if(uv.x<0.0||uv.y<0.0||uv.x>1.0||uv.y>1.0)return -100000.0;
    vec4 h=texture2D(uWorldHeight,uv);
    if(h.b<0.05)return -100000.0;
    return uWorldHeightRange.x+dot(h.rg,vec2(65280.0,255.0))/65535.0*uWorldHeightRange.y;
}
// Both native water layers share a world-anchored, mipmapped material field.
vec3 texturedWater(vec3 source,vec3 p,vec3 L,float sun,float ao) {
    vec2 uv=p.xy/48.0;
    mat2 turn=mat2(.80,.60,-.60,.80);
    vec4 broad=texture2D(uWaterDetail,uv+vec2(.009,-.006)*uWorldTime);
    vec2 bend=(broad.rg*2.0-1.0)*.06;
    vec4 crossing=texture2D(uWaterDetail,turn*uv*2.73+bend+vec2(-.013,.008)*uWorldTime);
    vec4 fine=texture2D(uWaterDetail,uv*7.31-bend+vec2(.018,.011)*uWorldTime);
    vec2 slope=(broad.rg*2.0-1.0)*.65
        +mat2(.80,-.60,.60,.80)*(crossing.rg*2.0-1.0)*.38
        +(fine.rg*2.0-1.0)*.14;
    vec3 N=normalize(vec3(-slope,1.0));
    vec3 V=normalize(uWorldEye-p);
    float height=(broad.a-.5)*1.4+(crossing.a-.5)*.9+(fine.a-.5)*.35;
    float mottling=(broad.b-.5)*.65;
    float body=clamp(.80+mottling+height*.90,.35,1.12);
    float ridges=smoothstep(.02,.22,height);
    float broken=ridges*smoothstep(.18,.78,crossing.b+fine.a*.25);
    float grazing=pow(1.0-clamp(dot(N,V),0.0,1.0),4.0);
    float glint=pow(max(dot(N,normalize(L+V)),0.0),24.0)*sun;
    float illumination=(.80+.20*max(dot(N,L),0.0)*sun)*(1.0-.05*ao);
    // Reflection contrast follows the ripple crests; color stays in the source hue.
    float reflection=broken*(.10+.16*grazing+.16*glint);
    return clamp(source*(body*illumination+reflection),0.0,1.0);
}
vec3 lightOriginalSurface(vec3 color) {
    if(vSurface.w>4.1 && vSurface.w<4.5 && vWorldQ.w<=0.0)discard;
    if(vWorldQ.w<=0.0)return color;
    if(vSurface.w>3.5 && vSurface.w<4.5 && vWorldQ.w<0.00001)
        return (dot(uWaterTint,uWaterTint)>0.0001 ? uWaterTint : color)*0.65;
    if(vSurface.w<0.5 || vWorldQ.w<=0.0)return color;
    vec3 p=vWorldQ.xyz/vWorldQ.w;
    vec3 n=normalize(vSurface.xyz);
    vec3 L=normalize(uWorldLight);
    float diffuse=max(dot(n,L),0.0);
    if(vSurface.w>2.5 && vSurface.w<3.5){
        float riderLight=0.70+0.24*diffuse+0.06*max(dot(n,normalize(uWorldEye-p)),0.0);
        return clamp(color*riderLight,0.0,1.0);
    }
    vec3 lightRight=normalize(cross(vec3(0.0,0.0,1.0),L));
    vec3 lightUp=cross(L,lightRight);
    vec3 relative=p-uWorldEye;
    vec2 shadowUv=vec2(dot(relative,lightRight),dot(relative,lightUp))/128.0+0.5;
    float receiverDepth=dot(relative,L)/512.0+0.5;
    float shadow=0.;
    // Native rider planes and rasterized silhouettes have different precision.
    // Receive rider-cast shadows on the scene only; riders still receive sunlight
    // without unstable silhouette or height-field receiver darkening.
    if(vSurface.w<2.5 && shadowUv.x>0.001 && shadowUv.y>0.001 && shadowUv.x<0.999 && shadowUv.y<0.999){
        for(int sy=0;sy<2;sy++)for(int sx=0;sx<2;sx++){
            vec4 caster=texture2D(uRiderShadow,shadowUv+(vec2(float(sx),float(sy))-.5)/1024.0);
            float casterDepth=dot(caster.rg,vec2(65280.0,255.0))/65535.0;
            shadow+=.25*step(.5,caster.b)*step(receiverDepth+.0003,casterDepth);
        }
    }
    float distanceAlong=2.0;
    for(int k=0;k<9;k++){
        vec3 samplePoint=p+L*distanceAlong;
        shadow=max(shadow,smoothstep(1.5,4.0,originalHeight(samplePoint.xy)-samplePoint.z));
        distanceAlong*=2.0;
    }
    float ao=0.0;
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy+vec2(5.0,0.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy-vec2(5.0,0.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy+vec2(0.0,5.0))-p.z);
    ao+=smoothstep(2.0,10.0,originalHeight(p.xy-vec2(0.0,5.0))-p.z);
    float sun=1.0-shadow;
    if(vSurface.w>3.5 && vSurface.w<4.5)
        return texturedWater(dot(uWaterTint,uWaterTint)>0.0001 ? uWaterTint : color,p,L,sun,ao);
    if(vSurface.w>1.5 && vSurface.w<2.5)
        return texturedWater(clamp(color,0.0,1.0),p,L,sun,ao);
    float ambient=0.38+0.16*max(n.z,0.0);
    return clamp(color*(ambient*(1.0-0.10*ao)+0.66*diffuse*sun),0.0,1.0);
}

vec4 sampleCoverageEffect(vec2 t) {
    vec4 base=texture2D(uRepTex,t);
    if(uRepCoverage>1.5) {
        float alpha=0.0;
        float aa=max(length(fwidth(t))*.5,.001);
        for(int i=0;i<24;i++) {
            float seed=fract(sin(float(i)*17.13+4.7)*43758.5453);
            float age=fract(uEffectTime*(.68+seed*.24)+float(i)/24.0);
            float side=fract(seed*13.7)*2.0-1.0;
            vec2 center=vec2(.5+side*age*.46,.96-age*1.55+age*age*.62);
            vec2 d=(t-center)*vec2(1.0,.50);
            float radius=.003+seed*.003;
            float fade=smoothstep(0.0,.12,age)*(1.0-smoothstep(.65,1.0,age));
            alpha=max(alpha,(1.0-smoothstep(radius,radius+aa,length(d)))*fade*.55);
        }
        vec3 tint=texture2D(uRepTex,vec2(.5,.72)).rgb;
        return vec4(tint,alpha);
    }
    float motion=smoothstep(0.02,0.20,uEffectTime);
    float phase=fract(uEffectTime*.85);
    float phase2=fract(phase+.5);
    // Cross-faded transport makes droplets travel up the plume, with no reset flash.
    float weight=sin(phase*3.14159265); weight*=weight;
    float weight2=1.0-weight;
    vec2 lateral=vec2(.035*sin(t.y*9.0+uEffectTime*1.7),0.0);
    vec2 q1=t+lateral+vec2(0.0,(phase-.5)*.48);
    vec2 q2=t-lateral+vec2(0.0,(phase2-.5)*.48);
    vec4 a=texture2D(uRepTex,q1);
    vec4 b=texture2D(uRepTex,q2);
    float edge=smoothstep(0.0,.06,t.x)*smoothstep(0.0,.06,1.0-t.x)
              *smoothstep(0.0,.04,t.y)*smoothstep(0.0,.04,1.0-t.y);
    float alpha=a.a*weight+b.a*weight2;
    vec3 rgb=(a.rgb*a.a*weight+b.rgb*b.a*weight2)/max(alpha,.0001);
    return mix(base,vec4(rgb,alpha*edge),motion);
}

void main() {
                                        vec2 vUV = vUVQ.xy / vUVQ.z;
                                        vec2 destUv = gl_FragCoord.xy / uDestSize;
                                        vec4 dstTexel = texture2D(uDest, destUv);
                                        if (uCheckMask > 0.5 && dstTexel.a >= 0.5) discard;

                                        vec3 rgb;
                                        float stp;
                                        float mask;
                                        float coverage = 1.0;

                                        if (vTexMode > 3.5 && vTexMode < 4.5) {
                                            rgb = vColor.rgb * 255.0;
                                            stp = 1.0;
                                            mask = uSetMask;
                                        } else if (vTexMode > 4.5 && vTexMode < 5.5) {
                                            vec4 img = texture2D(uExtTex, vUV);
                                            if (img.a < 0.5) discard;
                                            rgb = floor(img.rgb * 255.0 + 0.5) * floor(vColor.rgb * 255.0 + 0.5) / 128.0;
                                            stp = 1.0;
                                            mask = uSetMask;
                                        } else {
                                            vec2 whole = floor(vUV);
                                            vec2 fuv = vec2(windowByte(whole.x, uTexWindow.x, uTexWindow.z),
                                                            windowByte(whole.y, uTexWindow.y, uTexWindow.w)) + fract(vUV);


                                            float rawU = dFdx(vUV.x) < 0.0 ? ceil(vUV.x - 0.0001) : floor(vUV.x + 0.0001);
                                            float rawV = dFdy(vUV.y) < 0.0 ? ceil(vUV.y - 0.0001) : floor(vUV.y + 0.0001);

                                            if (vTexMode > 5.5) {
                                                vec2 t = (fuv - uRepRect.xy) / uRepRect.zw;
                                                vec4 img = uRepCoverage > 0.5 ? sampleCoverageEffect(t) : texture2D(uRepTex, t);
                                                if (uRepCoverage > 0.5) {
                                                    if (img.a <= 0.0) discard;
                                                    coverage = img.a;
                                                    stp = 1.0;
                                                    mask = uSetMask;
                                                } else {
                                                    if (img.a < 0.5) discard;
                                                    stp = img.a < 0.95 ? 1.0 : 0.0;
                                                    mask = max(stp, uSetMask);
                                                }
                                                rgb = floor(img.rgb * 255.0 + 0.5) * floor(vColor.rgb * 255.0 + 0.5) / 128.0;
                                            } else {
                                                vec2 uv = vec2(windowByte(rawU, uTexWindow.x, uTexWindow.z),
                                                               windowByte(rawV, uTexWindow.y, uTexWindow.w));
                                                uv = vec2(mod(uv.x, 256.0), mod(uv.y, 256.0));
                                                vec4 texel;

                                                if (vTexMode < 0.5) {
                                                    float s = fetch16(vec2(vPageBase.x + floor(uv.x / 4.0), vPageBase.y + uv.y));
                                                    float lane = mod(uv.x, 4.0);
                                                    float div = lane < 0.5 ? 1.0 : (lane < 1.5 ? 16.0 : (lane < 2.5 ? 256.0 : 4096.0));
                                                    float idx = mod(floor(s / div), 16.0);
                                                    texel = vRepClut > 0.5
                                                        ? texture2D(uRepClut, vec2((idx + 0.5) / uRepClutCount, 0.5))
                                                        : fetch(vec2(vClutBase.x + idx, vClutBase.y));
                                                } else if (vTexMode < 1.5) {
                                                    float s = fetch16(vec2(vPageBase.x + floor(uv.x / 2.0), vPageBase.y + uv.y));
                                                    float div = mod(uv.x, 2.0) < 0.5 ? 1.0 : 256.0;
                                                    float idx = mod(floor(s / div), 256.0);
                                                    texel = vRepClut > 0.5
                                                        ? texture2D(uRepClut, vec2((idx + 0.5) / uRepClutCount, 0.5))
                                                        : fetch(vec2(vClutBase.x + idx, vClutBase.y));
                                                } else {
                                                    texel = fetch(vec2(vPageBase.x + uv.x, vPageBase.y + uv.y));
                                                }

                                                if (vRepClut > 0.5 && vTexMode < 1.5) {
                                                    if (texel.a < 0.5) discard;
                                                    rgb = floor(texel.rgb * 255.0 + 0.5) * floor(vColor.rgb * 255.0 + 0.5) / 128.0;
                                                    stp = texel.a < 0.95 ? 1.0 : 0.0;
                                                } else {
                                                    if (texel.r == 0.0 && texel.g == 0.0 && texel.b == 0.0 && texel.a < 0.5) discard;
                                                    vec3 t8 = floor(texel.rgb * 31.0 + 0.5) * 8.0;
                                                    rgb = t8 * floor(vColor.rgb * 255.0 + 0.5) / 128.0;
                                                    stp = texel.a >= 0.5 ? 1.0 : 0.0;
                                                }
                                                mask = max(stp, uSetMask);
                                            }
                                        }

                                        vec3 outRgb = lightOriginalSurface(quant5(floor(rgb)));
                                        if (uSemiTrans * stp > 0.5) outRgb = blendWith(outRgb, dstTexel.rgb);
                                        outRgb = clamp(mix(dstTexel.rgb, outRgb, coverage), 0.0, 1.0);
                                        gl_FragColor = vec4(outRgb, mask);
                                    }
""";

    private static readonly (uint Index, string Name)[] PrimAttribs =
    [
        (0, "inPos"), (1, "inColorF"), (2, "inClutF"), (3, "inTexpageF"), (4, "inUV"), (5, "inW"), (6, "inWorldQ"), (7, "inSurface")
    ];

    public static uint BuildPrim(GL gl, string vsSrc, string fsSrc, string name)
    {
        return Build(gl, vsSrc, fsSrc, name, PrimAttribs);
    }

    public static uint BuildFullscreen(GL gl, string vsSrc, string fsSrc, string name)
    {
        return Build(gl, vsSrc, fsSrc, name, [(0, "aPos")]);
    }

    public static uint Build(GL gl, string vsSrc, string fsSrc, string name, out string? error)
    {
        error = null;
        var vs = CompileStage(gl, ShaderType.VertexShader, vsSrc, name, out var vsLog);
        var fs = CompileStage(gl, ShaderType.FragmentShader, fsSrc, name, out var fsLog);
        if (vs == 0 || fs == 0)
        {
            error = vsLog ?? fsLog;
            if (vs != 0) gl.DeleteShader(vs);
            if (fs != 0) gl.DeleteShader(fs);
            return 0;
        }

        var prog = gl.CreateProgram();
        gl.AttachShader(prog, vs);
        gl.AttachShader(prog, fs);
        gl.LinkProgram(prog);
        gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out var ok);
        if (ok == 0)
        {
            error = gl.GetProgramInfoLog(prog);
            gl.DeleteProgram(prog);
            prog = 0;
        }

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return prog;
    }

    private static uint CompileStage(GL gl, ShaderType type, string src, string name, out string? log)
    {
        log = null;
        var sh = gl.CreateShader(type);
        gl.ShaderSource(sh, Ascii(src));
        gl.CompileShader(sh);
        gl.GetShader(sh, ShaderParameterName.CompileStatus, out var ok);
        if (ok == 0)
        {
            log = $"{type}: {gl.GetShaderInfoLog(sh)}";
            gl.DeleteShader(sh);
            return 0;
        }

        return sh;
    }

    public static uint Build(GL gl, string vsSrc, string fsSrc, string name,
        (uint Index, string Name)[]? attribs = null)
    {
        var vs = CompileStage(gl, ShaderType.VertexShader, vsSrc, name);
        var fs = CompileStage(gl, ShaderType.FragmentShader, fsSrc, name);
        if (vs == 0 || fs == 0) return 0;

        var prog = gl.CreateProgram();
        gl.AttachShader(prog, vs);
        gl.AttachShader(prog, fs);
        if (attribs != null)
            foreach (var (index, attrib) in attribs)
                gl.BindAttribLocation(prog, index, attrib);
        gl.LinkProgram(prog);
        gl.GetProgram(prog, ProgramPropertyARB.LinkStatus, out var ok);
        if (ok == 0)
        {
            Console.WriteLine($"[GlBackend] link failed ({name}): {gl.GetProgramInfoLog(prog)}");
            gl.DeleteProgram(prog);
            prog = 0;
        }

        gl.DeleteShader(vs);
        gl.DeleteShader(fs);
        return prog;
    }

    private static string Ascii(string s)
    {
        var a = s.ToCharArray();
        for (var i = 0; i < a.Length; i++)
            if (a[i] > 0x7F)
                a[i] = ' ';
        return new string(a);
    }

    private static uint CompileStage(GL gl, ShaderType type, string src, string name)
    {
        var sh = gl.CreateShader(type);
        gl.ShaderSource(sh, Ascii(src));
        gl.CompileShader(sh);
        gl.GetShader(sh, ShaderParameterName.CompileStatus, out var ok);
        if (ok == 0)
        {
            Console.WriteLine($"[GlBackend] compile failed ({name} {type}) {gl.GetShaderInfoLog(sh)}");
            gl.DeleteShader(sh);
            return 0;
        }

        return sh;
    }
}
