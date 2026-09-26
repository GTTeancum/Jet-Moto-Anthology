$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Deployment.ps1')
$root=Join-Path $PSScriptRoot ('.build/deployment-test-'+[Guid]::NewGuid().ToString('N'))
$source=Join-Path $root 'publish'
$target=Join-Path $root 'existing game'
New-Item -ItemType Directory -Path $source,$target | Out-Null
[IO.File]::WriteAllText((Join-Path $source 'JetMoto.exe'),'new application fixture')
[IO.File]::WriteAllText((Join-Path $target 'JetMoto.exe'),'old application fixture')
foreach($name in @('carda.sav','settings.json','disc.cue')) {
    [IO.File]::WriteAllText((Join-Path $target $name),'preserve user data')
}
$passed=0
foreach($bad in @('../escape.txt','carda.sav','settings.json','disc.cue','Textures/Overrides/test.png')) {
    $rejected=$false
    try { Copy-JetMotoDeployment -PublishRoot $source -Destination $target -PublishedFiles @('JetMoto.exe',$bad) | Out-Null }
    catch { $rejected=$true }
    if(!$rejected -or [IO.File]::ReadAllText((Join-Path $target 'JetMoto.exe')) -ne 'old application fixture') {
        throw "Unsafe or partially applied deployment: $bad"
    }
    $passed++
}
$locked=[IO.File]::Open((Join-Path $target 'JetMoto.exe'),'Open','ReadWrite','None')
try {
    $rejected=$false
    try { Copy-JetMotoDeployment -PublishRoot $source -Destination $target -PublishedFiles @('JetMoto.exe') | Out-Null }
    catch { $rejected=$true }
    if(!$rejected){throw 'Locked application was not rejected.'}
    $passed++
} finally { $locked.Dispose() }
Copy-JetMotoDeployment -PublishRoot $source -Destination $target -PublishedFiles @('JetMoto.exe') | Out-Null
if([IO.File]::ReadAllText((Join-Path $target 'JetMoto.exe')) -ne 'new application fixture'){throw 'Application not deployed.'}
foreach($name in @('carda.sav','settings.json','disc.cue')) {
    if([IO.File]::ReadAllText((Join-Path $target $name)) -ne 'preserve user data'){throw "User data changed: $name"}
}
$passed++
Write-Host "PASS: $passed deployment cases; user data preserved and locked/unsafe writes refused."
