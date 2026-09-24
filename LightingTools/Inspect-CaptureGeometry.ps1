param(
    [Parameter(Mandatory)][string]$Geometry,
    [Parameter(Mandatory)][double]$PixelX,
    [Parameter(Mandatory)][double]$PixelY
)
$ErrorActionPreference='Stop'
$trace=Get-Content -Raw -LiteralPath $Geometry | ConvertFrom-Json
if(!$trace.available -or $trace.truncated -or !$trace.framebuffer){throw 'Capture geometry is missing, truncated, or has no native framebuffer.'}
if($PixelX -lt 0 -or $PixelY -lt 0 -or $PixelX -ge $trace.width -or $PixelY -ge $trace.height){throw 'Pixel outside capture.'}
$fb=$trace.framebuffer
$sampleWidth=if($fb[4] -gt 0){$fb[2]*$trace.aspect/(4.0/3.0)}else{$fb[2]}
$x=$fb[0]+$fb[2]*0.5+(($PixelX+0.5)/$trace.width-0.5)*$sampleWidth
$y=$fb[1]+($PixelY+0.5)/$trace.height*$fb[3]
function Edge($a,$b,[double]$x,[double]$y){return ($b[0]-$a[0])*($y-$a[1])-($b[1]-$a[1])*($x-$a[0])}
$hits=@(foreach($triangle in $trace.triangles){
    $clip=$triangle.clip
    if($clip[2] -lt $fb[0] -or $clip[0] -ge $fb[0]+$fb[2] -or $clip[3] -lt $fb[1] -or $clip[1] -ge $fb[1]+$fb[3]){continue}
    $a,$b,$c=$triangle.vertices
    $area=Edge $a $b $c[0] $c[1]
    if([Math]::Abs($area) -lt 0.0001){continue}
    $u=(Edge $b $c $x $y)/$area
    $v=(Edge $c $a $x $y)/$area
    $w=1-$u-$v
    if($u -lt 0 -or $v -lt 0 -or $w -lt 0){continue}
    [ordered]@{order=$triangle.order;primitive=$triangle.primitive;kind=$triangle.kind;material=$triangle.material;
        semi=$triangle.semi;blend=$triangle.blend;plane=$triangle.plane;vertices=$triangle.vertices}
})
[ordered]@{sourceFrame=$trace.sourceFrame;captureFrame=$trace.captureFrame;point=@($x,$y);hits=$hits;
    note='Geometric candidates in submission order, not final ownership: texture alpha, mask tests and shader discard still apply.'} | ConvertTo-Json -Depth 8
