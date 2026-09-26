param([Parameter(Mandatory)][string]$Archive)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$archivePath=(Resolve-Path -LiteralPath $Archive).Path
$zip=[IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $manifestEntries=@($zip.Entries | Where-Object { $_.FullName.Replace('\','/') -like '*/SHA256-MANIFEST.json' })
    if($manifestEntries.Count -ne 1){throw 'Expected one package manifest.'}
    $manifestEntry=$manifestEntries[0]
    $prefix=$manifestEntry.FullName.Substring(0,$manifestEntry.FullName.Length-'SHA256-MANIFEST.json'.Length).Replace('\','/')
    $reader=[IO.StreamReader]::new($manifestEntry.Open())
    try {$manifest=$reader.ReadToEnd() | ConvertFrom-Json}finally{$reader.Dispose()}
    $expected=@{}
    foreach($record in $manifest){
        if($expected.ContainsKey($record.path)){throw 'Duplicate manifest path.'}
        $expected[$record.path]=$record
    }
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $checked=0
    foreach($entry in $zip.Entries){
        # .NET Framework's ZIP reader exposes Windows separators, while modern
        # .NET exposes forward slashes for the same archive entries.
        $name=$entry.FullName.Replace('\','/')
        if($name.EndsWith('/')){continue}
        if(!$name.StartsWith($prefix,[StringComparison]::Ordinal)){throw 'Entry outside package root.'}
        $relative=$name.Substring($prefix.Length)
        if(!$seen.Add($relative) -or $relative -match '(^|/)\.\.(/|$)' -or $relative.Contains('\') -or $relative.Contains(':') -or
           $relative -match '(^|/)(logs|saves|memcards|Overrides)/|\.(bin|cue|iso|chd|sav|mcr|mcd|log)$|(^|/)(settings\.json|interface\.ini)$'){
            throw "Unsafe or user-data archive entry: $relative"
        }
        if($relative -eq 'SHA256-MANIFEST.json'){continue}
        $record=$expected[$relative]
        if(!$record -or $entry.Length -ne $record.bytes){throw "Unknown/wrong-size archive entry: $relative"}
        $stream=$entry.Open()
        $sha=[Security.Cryptography.SHA256]::Create()
        try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$sha.Dispose();$stream.Dispose()}
        if($hash -ne $record.sha256){throw "Archive hash mismatch: $relative"}
        $checked++
    }
    if($checked -ne $expected.Count){throw 'Missing manifest entries.'}
    foreach($required in @('JetMoto.exe','run.bat','READ-ME.txt','release-verification.json')){
        if(!$expected.ContainsKey($required)){throw "Missing required file: $required"}
    }
    $gateEntry=$zip.Entries | Where-Object { $_.FullName.Replace('\','/') -eq ($prefix+'release-verification.json') }
    $reader=[IO.StreamReader]::new($gateEntry.Open())
    try {$gate=$reader.ReadToEnd() | ConvertFrom-Json}finally{$reader.Dispose()}
    if($gate.singleFile){
        if($expected['JetMoto.exe'].sha256 -ne $gate.exeSha256){throw 'Single-file gate hash mismatch.'}
        if(@($expected.Keys | Where-Object {$_ -match '\.(dll|pdb)$'}).Count){throw 'Loose dependencies in single-file package.'}
    } else {
        foreach($required in @('JetMoto.dll','coreclr.dll')){
            if(!$expected.ContainsKey($required)){throw "Missing legacy runtime file: $required"}
        }
    }
    Write-Host "PASS: $checked archive files verified by SHA256; no disc images, saves, settings, logs or user overrides."
}finally{$zip.Dispose()}
$hashLine=(Get-Content -LiteralPath ($archivePath+'.sha256') -Raw).Trim()
if($hashLine.Split(' ')[0] -ne (Get-FileHash -LiteralPath $archivePath).Hash){throw 'Archive sidecar hash mismatch.'}
Write-Host 'PASS: archive SHA256 sidecar verified.'
