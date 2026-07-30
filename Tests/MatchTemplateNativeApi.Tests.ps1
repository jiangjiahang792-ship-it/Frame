$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$matchToolPath = Join-Path $projectRoot 'Native\MatchTool.dll'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Read-UInt16 {
    param([byte[]]$Bytes, [int]$Offset)
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-UInt32 {
    param([byte[]]$Bytes, [int]$Offset)
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-RvaToOffset {
    param(
        [uint32]$Rva,
        [object[]]$Sections
    )

    foreach ($section in $Sections) {
        $start = [uint32]$section.VirtualAddress
        $span = [uint32][Math]::Max($section.VirtualSize, $section.RawSize)
        if ($Rva -ge $start -and $Rva -lt ($start + $span)) {
            return [int]($section.RawPointer + ($Rva - $start))
        }
    }

    throw "RVA 0x$($Rva.ToString('X')) is outside PE sections."
}

function Read-AsciiNullString {
    param([byte[]]$Bytes, [int]$Offset)

    $end = $Offset
    while ($end -lt $Bytes.Length -and $Bytes[$end] -ne 0) {
        $end++
    }

    return [Text.Encoding]::ASCII.GetString($Bytes, $Offset, $end - $Offset)
}

function Get-PeExportNames {
    param([string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    Assert-True -Condition ($bytes.Length -gt 0x100) -Message 'MatchTool.dll is too small to contain a valid PE image.'
    Assert-True -Condition ((Read-UInt16 $bytes 0) -eq 0x5A4D) -Message 'MatchTool.dll must be a valid PE image.'

    $peOffset = [int](Read-UInt32 $bytes 0x3C)
    Assert-True -Condition ((Read-UInt32 $bytes $peOffset) -eq 0x00004550) -Message 'MatchTool.dll has an invalid PE signature.'

    $fileHeaderOffset = $peOffset + 4
    $sectionCount = [int](Read-UInt16 $bytes ($fileHeaderOffset + 2))
    $optionalHeaderSize = [int](Read-UInt16 $bytes ($fileHeaderOffset + 16))
    $optionalHeaderOffset = $fileHeaderOffset + 20
    $magic = Read-UInt16 $bytes $optionalHeaderOffset
    $dataDirectoryOffset = if ($magic -eq 0x20B) { $optionalHeaderOffset + 112 } else { $optionalHeaderOffset + 96 }
    $exportRva = [uint32](Read-UInt32 $bytes $dataDirectoryOffset)
    Assert-True -Condition ($exportRva -ne 0) -Message 'MatchTool.dll must contain an export table.'

    $sectionHeaderOffset = $optionalHeaderOffset + $optionalHeaderSize
    $sections = @()
    for ($index = 0; $index -lt $sectionCount; $index++) {
        $offset = $sectionHeaderOffset + $index * 40
        $sections += [pscustomobject]@{
            VirtualSize = Read-UInt32 $bytes ($offset + 8)
            VirtualAddress = Read-UInt32 $bytes ($offset + 12)
            RawSize = Read-UInt32 $bytes ($offset + 16)
            RawPointer = Read-UInt32 $bytes ($offset + 20)
        }
    }

    $exportOffset = Convert-RvaToOffset $exportRva $sections
    $nameCount = [int](Read-UInt32 $bytes ($exportOffset + 24))
    $namesRva = [uint32](Read-UInt32 $bytes ($exportOffset + 32))
    $namesOffset = Convert-RvaToOffset $namesRva $sections

    $names = New-Object 'System.Collections.Generic.List[string]'
    for ($index = 0; $index -lt $nameCount; $index++) {
        $nameRva = [uint32](Read-UInt32 $bytes ($namesOffset + $index * 4))
        $nameOffset = Convert-RvaToOffset $nameRva $sections
        $names.Add((Read-AsciiNullString $bytes $nameOffset))
    }

    return $names
}

Assert-True -Condition (Test-Path -LiteralPath $matchToolPath -PathType Leaf) -Message 'Native MatchTool.dll must exist.'
$exports = Get-PeExportNames $matchToolPath

$requiredExports = @(
    'CreateMatchEngine',
    'ReleaseMatchEngine',
    'LearnPatternMem',
    'MatchMem',
    'GetMatchApiVersion',
    'GetMatchStatusMessage',
    'SavePatternFile',
    'LoadPatternFile'
)

foreach ($exportName in $requiredExports) {
    Assert-True -Condition ($exports -contains $exportName) -Message "MatchTool.dll missing required export: $exportName"
}

Write-Host "MatchTool native API check passed: $($requiredExports.Count) exports found."
