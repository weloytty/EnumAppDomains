[CmdletBinding()]
param([string[]]$ProcessName,
    [int[]]$ProcessId,
    [switch]$ShowGac,
    [switch]$ShowHeap,
    [switch]$HideThreads)

begin {
    Set-StrictMode -Version Latest
    Write-Verbose "Process Id  : '$processId' (null: $($null -eq $processId)"
    Write-Verbose "Process Name: '$processName' (Null: $($null -eq $processName))"
    if ($null -ne $ProcessId -and $null -ne $ProcessName) {throw "Can't specify process name and id. Pick one or the other"}
    if ($null -eq $ProcessId -and $null -eq $ProcessName) {throw "Choose process name or process id."}
    $eadArgs = @()
    $eadCommand = ""
    if (Test-Path -Path ".\EnumAppDomains.exe" -PathType Leaf) {$eadCommand = $(Get-Item ".\EnumAppDomains.exe").FullName}
    if ($eadCommand -eq "") {
        $cmdInfo = $(Get-Command "EnumAppdomains")
        if (-not ($cmdInfo)) {throw "Can't find EnumAppDomains.exe"}
        $eadCommand = $cmdInfo.Source
    }

    if ($ShowGac) {
        $eadArgs += '-g'
    }
    if ($ShowHeap) {
        $eadArgs += '-h'
    }
    if ($HideThreads) {
        $eadArgs += '-t'
    }


}
process {
    if ($ProcessName -ne '') {
        foreach ($procName in $ProcessName) {
            $proc = Get-Process -Name $procName
            if ($proc) {
                $ProcessId += $proc.Id
            } else {Write-Warning "Can't find process $procName.  Check elevation."}

        }
        
    }
    if ($ProcessId -ne 0) {
        foreach ($currId in $ProcessId) {
            & "$eadCommand" --pid $currId $eadArgs
        }
        
    }
}