param(
    [Parameter(Mandatory = $true)]
    [string] $NupkgPath,
    [Parameter(Mandatory = $true)]
    [string] $CorePdbPath,
    [Parameter(Mandatory = $true)]
    [string] $SymbolPackagePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Windows PowerShell 5.1 defaults to a BOM-capable StreamWriter encoding.
# PowerShell 7 defaults to UTF-8 without BOM. Both are accepted only when this
# script writes XML with an explicit UTF-8 (no BOM) encoder.
if ($null -eq $PSVersionTable -or [int]$PSVersionTable.PSVersion.Major -lt 5) {
    throw "create-core-snupkg.ps1 requires Windows PowerShell 5.1 or PowerShell 7+."
}

$ExpectedPackageId = 'SmartEnum'
$ExpectedPackageVersion = '1.0.0-preview.1'
$ExpectedDllEntry = 'lib/net10.0/SmartEnum.dll'
$ExpectedPdbEntry = 'lib/net10.0/SmartEnum.pdb'
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
$ZipTimestamp = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Convert-RvaToFileOffset([byte[]] $pe, [uint32] $rva, [int] $eLfanew) {
    $sectionCount = [BitConverter]::ToUInt16($pe, $eLfanew + 6)
    $optionalHeaderSize = [BitConverter]::ToUInt16($pe, $eLfanew + 20)
    $sectionStart = $eLfanew + 24 + $optionalHeaderSize
    for ($i = 0; $i -lt $sectionCount; $i++) {
        $section = $sectionStart + ($i * 40)
        $virtualSize = [BitConverter]::ToUInt32($pe, $section + 8)
        $virtualAddress = [BitConverter]::ToUInt32($pe, $section + 12)
        $rawSize = [BitConverter]::ToUInt32($pe, $section + 16)
        $rawPointer = [BitConverter]::ToUInt32($pe, $section + 20)
        $span = [Math]::Max($virtualSize, $rawSize)
        if ($rva -ge $virtualAddress -and $rva -lt ($virtualAddress + $span)) {
            return [int]($rawPointer + ($rva - $virtualAddress))
        }
    }

    throw "PE RVA 0x$($rva.ToString('X')) is not mapped to a file offset."
}

function Get-AlignedOffset([int] $offset) {
    $remainder = $offset % 4
    if ($remainder -eq 0) {
        return $offset
    }

    return $offset + (4 - $remainder)
}

function Read-MetadataStreamHeaders([byte[]] $data, [int] $rootOffset) {
    $signature = [Text.Encoding]::ASCII.GetString($data, $rootOffset, 4)
    if ($signature -ne 'BSJB') {
        throw "Metadata root at offset $rootOffset is not BSJB (got '$signature')."
    }

    $versionLength = [BitConverter]::ToInt32($data, $rootOffset + 12)
    if ($versionLength -lt 0 -or ($rootOffset + 16 + $versionLength) -gt $data.Length) {
        throw 'Metadata version string is truncated.'
    }

    $cursor = Get-AlignedOffset ($rootOffset + 16 + $versionLength)
    $streamCount = [BitConverter]::ToUInt16($data, $cursor + 2)
    $cursor += 4
    $streams = @{}
    for ($i = 0; $i -lt $streamCount; $i++) {
        $streamOffset = [BitConverter]::ToInt32($data, $cursor)
        $streamSize = [BitConverter]::ToInt32($data, $cursor + 4)
        $cursor += 8
        $nameStart = $cursor
        while ($cursor -lt $data.Length -and $data[$cursor] -ne 0) {
            $cursor++
        }
        if ($cursor -ge $data.Length) {
            throw 'Metadata stream name is truncated.'
        }

        $name = [Text.Encoding]::ASCII.GetString($data, $nameStart, $cursor - $nameStart)
        $nameBytesIncludingNull = ($cursor - $nameStart) + 1
        $cursor = $nameStart + (Get-AlignedOffset $nameBytesIncludingNull)
        $streams[$name] = @{
            Offset = $rootOffset + $streamOffset
            Size   = $streamSize
        }
    }

    return $streams
}

function Get-CodeViewDebugId([byte[]] $pe) {
    if ($pe.Length -lt 64 -or $pe[0] -ne 0x4D -or $pe[1] -ne 0x5A) {
        throw 'Packed Core file is not a PE image (MZ header missing).'
    }

    $eLfanew = [BitConverter]::ToInt32($pe, 0x3C)
    if ([BitConverter]::ToUInt32($pe, $eLfanew) -ne 0x00004550) {
        throw 'Packed Core file is missing the PE signature.'
    }

    $optionalHeader = $eLfanew + 24
    $magic = [BitConverter]::ToUInt16($pe, $optionalHeader)
    if ($magic -eq 0x10B) {
        $debugDirectoryFileOffset = $optionalHeader + 144
    }
    elseif ($magic -eq 0x20B) {
        $debugDirectoryFileOffset = $optionalHeader + 160
    }
    else {
        throw "Unsupported PE optional header magic 0x$($magic.ToString('X'))."
    }

    $debugRva = [BitConverter]::ToUInt32($pe, $debugDirectoryFileOffset)
    $debugSize = [BitConverter]::ToUInt32($pe, $debugDirectoryFileOffset + 4)
    if ($debugRva -eq 0 -or $debugSize -lt 28) {
        throw 'Core DLL has no PE debug directory. Portable PDB identity cannot be verified.'
    }

    $debugOffset = Convert-RvaToFileOffset $pe $debugRva $eLfanew
    $entryCount = [int]($debugSize / 28)
    for ($i = 0; $i -lt $entryCount; $i++) {
        $entry = $debugOffset + ($i * 28)
        $type = [BitConverter]::ToUInt32($pe, $entry + 12)
        if ($type -ne 2) {
            continue
        }

        $pointer = [int][BitConverter]::ToUInt32($pe, $entry + 24)
        if (($pointer + 24) -gt $pe.Length) {
            throw 'CodeView debug data is truncated.'
        }

        $signature = [BitConverter]::ToUInt32($pe, $pointer)
        if ($signature -ne 0x53445352) {
            throw "CodeView debug data is not RSDS (signature 0x$($signature.ToString('X')))."
        }

        $guidBytes = New-Object byte[] 16
        [Array]::Copy($pe, $pointer + 4, $guidBytes, 0, 16)
        return @{
            Guid  = New-Object Guid (, $guidBytes)
            Age   = [BitConverter]::ToUInt32($pe, $pointer + 20)
            Stamp = [BitConverter]::ToUInt32($pe, $entry + 4)
        }
    }

    throw 'Core DLL debug directory has no CodeView RSDS entry.'
}

function Get-PortablePdbId([byte[]] $pdb) {
    if ($pdb.Length -lt 16) {
        throw 'Core PDB is too small to be a portable PDB.'
    }

    $magic = [Text.Encoding]::ASCII.GetString($pdb, 0, 4)
    if ($magic -ne 'BSJB') {
        throw "Core PDB is not a portable PDB (magic '$magic', expected BSJB)."
    }

    $streams = Read-MetadataStreamHeaders $pdb 0
    if (-not $streams.Contains('#Pdb')) {
        throw 'Portable PDB is missing the #Pdb stream (PDB ID).'
    }

    $pdbStream = $streams['#Pdb']
    if ([int]$pdbStream.Size -lt 20) {
        throw '#Pdb stream is too small to contain a PDB ID.'
    }

    $guidBytes = New-Object byte[] 16
    [Array]::Copy($pdb, [int]$pdbStream.Offset, $guidBytes, 0, 16)
    return @{
        Guid  = New-Object Guid (, $guidBytes)
        Stamp = [BitConverter]::ToUInt32($pdb, [int]$pdbStream.Offset + 16)
    }
}

function Get-PeModuleVersionId([byte[]] $pe) {
    $eLfanew = [BitConverter]::ToInt32($pe, 0x3C)
    $optionalHeader = $eLfanew + 24
    $magic = [BitConverter]::ToUInt16($pe, $optionalHeader)
    if ($magic -eq 0x10B) {
        $cliDirectoryOffset = $optionalHeader + 208
    }
    elseif ($magic -eq 0x20B) {
        $cliDirectoryOffset = $optionalHeader + 224
    }
    else {
        throw "Unsupported PE optional header magic 0x$($magic.ToString('X'))."
    }

    $cliRva = [BitConverter]::ToUInt32($pe, $cliDirectoryOffset)
    if ($cliRva -eq 0) {
        throw 'Core DLL has no CLI header; MVID cannot be read.'
    }

    $cliOffset = Convert-RvaToFileOffset $pe $cliRva $eLfanew
    $metadataRva = [BitConverter]::ToUInt32($pe, $cliOffset + 8)
    $metadataOffset = Convert-RvaToFileOffset $pe $metadataRva $eLfanew
    $streams = Read-MetadataStreamHeaders $pe $metadataOffset
    $tableStream = $null
    if ($streams.Contains('#~')) {
        $tableStream = $streams['#~']
    }
    elseif ($streams.Contains('#-')) {
        $tableStream = $streams['#-']
    }
    else {
        throw 'Core DLL metadata tables stream is missing.'
    }

    if (-not $streams.Contains('#GUID')) {
        throw 'Core DLL #GUID heap is missing.'
    }

    $tables = [int]$tableStream.Offset
    $heapSizes = $pe[$tables + 6]
    $wideStrings = ($heapSizes -band 1) -ne 0
    $wideGuids = ($heapSizes -band 2) -ne 0
    $valid = [BitConverter]::ToUInt64($pe, $tables + 8)
    if (($valid -band [uint64]1) -eq 0) {
        throw 'Core DLL Module table is missing.'
    }

    $presentTableCount = 0
    $bit = 0
    while ($bit -lt 64) {
        $mask = [uint64]1 -shl $bit
        if (($valid -band $mask) -ne 0) {
            $presentTableCount++
        }
        $bit++
    }

    $stringIndexSize = 2
    if ($wideStrings) {
        $stringIndexSize = 4
    }

    $guidIndexSize = 2
    if ($wideGuids) {
        $guidIndexSize = 4
    }

    $moduleRow = $tables + 24 + ($presentTableCount * 4)
    $mvidIndexOffset = $moduleRow + 2 + $stringIndexSize
    if ($guidIndexSize -eq 2) {
        $mvidIndex = [BitConverter]::ToUInt16($pe, $mvidIndexOffset)
    }
    else {
        $mvidIndex = [BitConverter]::ToUInt32($pe, $mvidIndexOffset)
    }

    if ($mvidIndex -eq 0) {
        throw 'Core DLL Module MVID heap index is 0.'
    }

    $guidOffset = [int]$streams['#GUID'].Offset + [int](($mvidIndex - 1) * 16)
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($pe, $guidOffset, $guidBytes, 0, 16)
    return New-Object Guid (, $guidBytes)
}

function Get-ZipEntryBytes(
    [string] $packagePath,
    [string] $entryName) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $normalized = $entryName.Replace('\', '/')
        $entry = $archive.Entries |
            Where-Object { $_.FullName.Replace('\', '/') -eq $normalized } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "Package '$packagePath' does not contain '$entryName'."
        }

        $stream = $entry.Open()
        try {
            $buffer = New-Object System.IO.MemoryStream
            try {
                $stream.CopyTo($buffer)
                return $buffer.ToArray()
            }
            finally {
                $buffer.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-NuspecText([string] $packagePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($packagePath)
    try {
        $entry = $archive.Entries |
            Where-Object { $_.FullName.EndsWith('.nuspec', [StringComparison]::OrdinalIgnoreCase) } |
            Select-Object -First 1
        if ($null -eq $entry) {
            throw "No .nuspec entry found in $packagePath"
        }

        $reader = New-Object System.IO.StreamReader($entry.Open(), $Utf8NoBom, $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-MetadataChild([System.Xml.XmlNode] $metadata, [string] $localName) {
    foreach ($child in $metadata.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.LocalName -eq $localName) {
            return $child
        }
    }

    return $null
}

function Convert-NupkgNuspecToSymbolNuspec([string] $nuspecText) {
    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $false
    $document.LoadXml($nuspecText)

    if ($null -eq $document.DocumentElement -or $document.DocumentElement.LocalName -ne 'package') {
        throw 'nupkg nuspec root element is not <package>.'
    }

    $metadata = $null
    foreach ($child in $document.DocumentElement.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.LocalName -eq 'metadata') {
            $metadata = $child
            break
        }
    }

    if ($null -eq $metadata) {
        throw 'nupkg nuspec is missing <metadata>.'
    }

    $idNode = Get-MetadataChild $metadata 'id'
    $versionNode = Get-MetadataChild $metadata 'version'
    if ($null -eq $idNode -or [string]::IsNullOrWhiteSpace($idNode.InnerText)) {
        throw 'nupkg nuspec is missing <id>.'
    }

    if ($null -eq $versionNode -or [string]::IsNullOrWhiteSpace($versionNode.InnerText)) {
        throw 'nupkg nuspec is missing <version>.'
    }

    $id = $idNode.InnerText.Trim()
    $version = $versionNode.InnerText.Trim()
    if ($id -ne $ExpectedPackageId) {
        throw "nupkg PackageId '$id' does not match required '$ExpectedPackageId'."
    }

    if ($version -ne $ExpectedPackageVersion) {
        throw "nupkg Version '$version' does not match required '$ExpectedPackageVersion'."
    }

    $forbidden = @(
        'authors',
        'owners',
        'requireLicenseAcceptance',
        'license',
        'licenseUrl',
        'icon',
        'readme'
    )
    $toRemove = New-Object System.Collections.Generic.List[System.Xml.XmlNode]
    foreach ($child in $metadata.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $forbidden -contains $child.LocalName) {
            [void]$toRemove.Add($child)
        }
    }

    foreach ($node in $toRemove) {
        [void]$metadata.RemoveChild($node)
    }

    $existingTypes = Get-MetadataChild $metadata 'packageTypes'
    if ($null -ne $existingTypes) {
        [void]$metadata.RemoveChild($existingTypes)
    }

    $namespaceUri = $document.DocumentElement.NamespaceURI
    $packageTypes = $document.CreateElement('packageTypes', $namespaceUri)
    $packageType = $document.CreateElement('packageType', $namespaceUri)
    [void]$packageType.SetAttribute('name', 'SymbolsPackage')
    [void]$packageTypes.AppendChild($packageType)
    [void]$metadata.AppendChild($packageTypes)

    $memory = New-Object System.IO.MemoryStream
    try {
        $settings = New-Object System.Xml.XmlWriterSettings
        $settings.Encoding = $Utf8NoBom
        $settings.Indent = $true
        $settings.IndentChars = '  '
        $settings.NewLineChars = "`n"
        $settings.OmitXmlDeclaration = $false
        $writer = [System.Xml.XmlWriter]::Create($memory, $settings)
        try {
            $document.Save($writer)
        }
        finally {
            $writer.Dispose()
        }

        return $Utf8NoBom.GetString($memory.ToArray())
    }
    finally {
        $memory.Dispose()
    }
}

function Add-ZipTextEntry(
    [System.IO.Compression.ZipArchive] $archive,
    [string] $entryName,
    [string] $content) {
    $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = $ZipTimestamp
    $bytes = $Utf8NoBom.GetBytes($content)
    $stream = $entry.Open()
    try {
        $stream.Write($bytes, 0, $bytes.Length)
    }
    finally {
        $stream.Dispose()
    }
}

function Add-ZipFileEntry(
    [System.IO.Compression.ZipArchive] $archive,
    [string] $entryName,
    [byte[]] $content) {
    $entry = $archive.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
    $entry.LastWriteTime = $ZipTimestamp
    $stream = $entry.Open()
    try {
        $stream.Write($content, 0, $content.Length)
    }
    finally {
        $stream.Dispose()
    }
}

if (-not (Test-Path -LiteralPath $NupkgPath)) {
    throw "Package not found: $NupkgPath"
}
if (-not (Test-Path -LiteralPath $CorePdbPath)) {
    throw "Core PDB not found: $CorePdbPath"
}

$onDiskDllPath = [System.IO.Path]::ChangeExtension($CorePdbPath, '.dll')
if (-not (Test-Path -LiteralPath $onDiskDllPath)) {
    throw "Core DLL was not found beside the Core PDB: $onDiskDllPath"
}

$packedDll = Get-ZipEntryBytes -packagePath $NupkgPath -entryName $ExpectedDllEntry
$onDiskDll = [System.IO.File]::ReadAllBytes($onDiskDllPath)
$pdbBytes = [System.IO.File]::ReadAllBytes($CorePdbPath)

$packedCodeView = Get-CodeViewDebugId $packedDll
$onDiskCodeView = Get-CodeViewDebugId $onDiskDll
$pdbId = Get-PortablePdbId $pdbBytes
$packedMvid = Get-PeModuleVersionId $packedDll
$onDiskMvid = Get-PeModuleVersionId $onDiskDll

if ($packedCodeView.Guid -ne $onDiskCodeView.Guid -or [uint32]$packedCodeView.Stamp -ne [uint32]$onDiskCodeView.Stamp) {
    throw "Packed Core DLL CodeView ID ($($packedCodeView.Guid) stamp $($packedCodeView.Stamp)) does not match the on-disk Core DLL beside the PDB ($($onDiskCodeView.Guid) stamp $($onDiskCodeView.Stamp))."
}

if ($packedMvid -ne $onDiskMvid) {
    throw "Packed Core DLL MVID $packedMvid does not match on-disk Core DLL MVID $onDiskMvid."
}

# Portable PDB matching uses the 20-byte PDB ID: CodeView GUID + IMAGE_DEBUG_DIRECTORY.TimeDateStamp.
# RSDS Age stays 1 for these portable PDBs and is not the last 4 bytes of DebugMetadataHeader.Id.
if ($packedCodeView.Guid -ne $pdbId.Guid -or [uint32]$packedCodeView.Stamp -ne [uint32]$pdbId.Stamp) {
    throw "Core DLL CodeView PDB ID ($($packedCodeView.Guid) stamp $($packedCodeView.Stamp)) does not match portable PDB #Pdb ID ($($pdbId.Guid) stamp $($pdbId.Stamp))."
}

if ($packedMvid -eq [Guid]::Empty) {
    throw 'Core DLL MVID is empty.'
}

# MVID identifies the ECMA-335 module. Debugger symbol matching uses the
# CodeView / #Pdb ID, which is a different GUID. Do not require equality.
Write-Host "Core symbol identity: PDB $($packedCodeView.Guid) stamp $($packedCodeView.Stamp); RSDS age $($packedCodeView.Age); MVID $packedMvid"

$symbolNuspec = Convert-NupkgNuspecToSymbolNuspec (Get-NuspecText -packagePath $NupkgPath)

if (Test-Path -LiteralPath $SymbolPackagePath) {
    Remove-Item -LiteralPath $SymbolPackagePath -Force
}

$contentTypes = @"
<?xml version="1.0" encoding="utf-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="nuspec" ContentType="application/octet" />
  <Default Extension="pdb" ContentType="application/octet" />
  <Default Extension="xml" ContentType="application/octet" />
  <Default Extension="psmdcp" ContentType="application/vnd.openxmlformats-package.core-properties+xml" />
</Types>
"@

$archive = [System.IO.Compression.ZipFile]::Open(
    $SymbolPackagePath,
    [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Add-ZipTextEntry -archive $archive -entryName 'SmartEnum.nuspec' -content $symbolNuspec
    Add-ZipFileEntry -archive $archive -entryName $ExpectedPdbEntry -content $pdbBytes
    Add-ZipTextEntry -archive $archive -entryName '[Content_Types].xml' -content $contentTypes
}
finally {
    $archive.Dispose()
}

$verify = [System.IO.Compression.ZipFile]::OpenRead($SymbolPackagePath)
try {
    $names = @(
        $verify.Entries |
            ForEach-Object { $_.FullName.Replace('\', '/') } |
            Sort-Object -Culture 127
    )
    $expected = @($ExpectedPdbEntry, 'SmartEnum.nuspec', '[Content_Types].xml') | Sort-Object -Culture 127
    if ($names.Count -ne $expected.Count) {
        throw "Symbol package entry count mismatch. Actual: $($names -join ', ')"
    }

    for ($i = 0; $i -lt $expected.Count; $i++) {
        if ($names[$i] -ne $expected[$i]) {
            throw "Symbol package entries mismatch. Expected '$($expected -join ', ')'; actual '$($names -join ', ')'."
        }
    }
}
finally {
    $verify.Dispose()
}

Write-Host "Created Core symbol package: $SymbolPackagePath"
