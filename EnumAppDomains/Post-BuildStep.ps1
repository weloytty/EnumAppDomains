[CmdLetBinding()]
param([string]$InputManifest,
[string]$UpdateResource)


$procArch = "x64"
if([intptr]::Size -eq 4){$procArch = "x86"}

Gci Env:\


$mtPath = $(Join-Path "$($env:WindowsSdkVerBinPath)" "$procArch\mt.exe")


Write-Output "Running "$mtPath" -manifest "$InputManifest" -updateresource:"$UpdateREsource""
& "$mtPath" -manifest "$InputManifest" -updateresource:"$UpdateREsource"

