<#
.SYNOPSIS
  Keeps the generated numbers in README.md and CONTRIBUTING.md in sync with the
  source: the xUnit test count, and each project's version.
.DESCRIPTION
  Two spans are maintained, both delimited by HTML comments so they render
  invisibly in markdown:

    <!--TESTS-->NNN<!--/TESTS-->                 the test count
    <!--VER:ProjectName-->X.Y.Z<!--/VER-->       one project's version

  TEST COUNT. Counted to match what `dotnet test` reports, so a contributor who
  runs the suite sees the same number the docs claim. That means counting test
  *cases*, not attributes: every [Fact] is one case, and a [Theory] contributes
  one case per [InlineData]. (The previous rule counted a [Theory] once however
  many [InlineData] rows it carried, which is why the README said 118 while the
  runner said 120.)

  Known limitation: a [Theory] fed by [MemberData] or [ClassData] yields a case
  count that cannot be determined without running the tests. Such a theory is
  counted as 1, so the number will understate if one is ever added. There are
  none today.

  VERSIONS. Read from the <Version> element of each project's .csproj, so the
  docs cannot drift from what actually ships.
.PARAMETER Check
  Verify only (for a pre-commit hook or CI): exit 1 if any span is stale,
  without modifying the files.
.EXAMPLE
  powershell -File tools\update-readme-counts.ps1
  powershell -File tools\update-readme-counts.ps1 -Check
#>
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repo   = Split-Path -Parent $PSScriptRoot
$tests  = Join-Path $repo 'KineticRESTIntegrator.Tests'

if (-not (Test-Path $tests)) { throw "Test project not found: $tests" }

# Files carrying maintained spans. A file that doesn't exist is skipped.
$targets = @(
    (Join-Path $repo 'README.md'),
    (Join-Path $repo 'CONTRIBUTING.md')
) | Where-Object { Test-Path $_ }

if ($targets.Count -eq 0) { throw "No target documents found (README.md, CONTRIBUTING.md)." }

# Projects whose <Version> may appear in the docs, keyed by the name used in
# the marker: <!--VER:EpicorSvcs-->
$projects = [ordered]@{
    'EpicorSvcs'       = 'EpicorSvcs/EpicorSvcs.csproj'
    'RESTServices'     = 'RESTServices/RESTServices.csproj'
    'Keri.Files'       = 'Keri.Files/Keri.Files.csproj'
    'Keri.Mail'        = 'Keri.Mail/Keri.Mail.csproj'
    'KeriConfigurator' = 'KeriConfigurator/KeriConfigurator.csproj'
}

# ---------------------------------------------------------------------------
# Test count.
#
# Walks each file line by line, gathering each contiguous run of attribute
# lines (the block sitting above a method) and scoring it:
#   [Fact]                     -> 1 case
#   [Theory] with [InlineData] -> one case per [InlineData]
#   [Theory] without           -> 1 case (see the MemberData note in the header)
# Reading blocks rather than counting attributes file-wide makes the result
# independent of the order attributes are written in.
#
# Assumes one attribute per line, which is how this test suite is written. An
# attribute split across lines would end its block early and undercount.
# ---------------------------------------------------------------------------
function Get-BlockCaseCount([System.Collections.Generic.List[string]] $block) {
    if ($block.Count -eq 0) { return 0 }

    $facts  = @($block | Where-Object { $_ -match '^\[\s*Fact\b' }).Count
    $inline = @($block | Where-Object { $_ -match '^\[\s*InlineData\b' }).Count
    $theory = @($block | Where-Object { $_ -match '^\[\s*Theory\b' }).Count

    $cases = $facts
    if ($theory -gt 0) {
        if ($inline -gt 0) { $cases += $inline } else { $cases += 1 }
    }
    return $cases
}

$count = 0
Get-ChildItem -Path $tests -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $block = New-Object System.Collections.Generic.List[string]

        foreach ($line in [System.IO.File]::ReadAllLines($_.FullName)) {
            $trimmed = $line.Trim()
            if ($trimmed.StartsWith('[')) {
                $block.Add($trimmed)
            }
            else {
                # Any non-attribute line closes the block — normally the method
                # signature the attributes belong to.
                $count += Get-BlockCaseCount $block
                $block.Clear()
            }
        }
        $count += Get-BlockCaseCount $block
    }
if ($count -le 0) { throw "No [Fact]/[Theory] tests found under $tests" }

# ---------------------------------------------------------------------------
# Versions — straight from each .csproj's <Version> element.
# ---------------------------------------------------------------------------
$versions = @{}
foreach ($name in $projects.Keys) {
    $csproj = Join-Path $repo $projects[$name]
    if (-not (Test-Path $csproj)) { continue }
    $m = [regex]::Match([System.IO.File]::ReadAllText($csproj), '<Version>\s*([^<\s]+)\s*</Version>')
    if ($m.Success) { $versions[$name] = $m.Groups[1].Value }
}

# ---------------------------------------------------------------------------
# Apply (or check) every span in every target document.
# ---------------------------------------------------------------------------
$stale   = @()
$changes = @()

foreach ($file in $targets) {
    $name    = Split-Path -Leaf $file
    $content = [System.IO.File]::ReadAllText($file)
    $updated = $content

    # Test-count span.
    $testPattern = '(?s)<!--TESTS-->(.*?)<!--/TESTS-->'
    foreach ($m in [regex]::Matches($updated, $testPattern)) {
        if ($m.Groups[1].Value -ne "$count") {
            $stale += "$name test count: '$($m.Groups[1].Value)' -> $count"
        }
    }
    $updated = [regex]::Replace($updated, $testPattern, "<!--TESTS-->$count<!--/TESTS-->")

    # Version spans, one per project marker.
    foreach ($proj in $versions.Keys) {
        $verPattern = "(?s)<!--VER:$([regex]::Escape($proj))-->(.*?)<!--/VER-->"
        foreach ($m in [regex]::Matches($updated, $verPattern)) {
            if ($m.Groups[1].Value -ne $versions[$proj]) {
                $stale += "$name $proj version: '$($m.Groups[1].Value)' -> $($versions[$proj])"
            }
        }
        $updated = [regex]::Replace(
            $updated, $verPattern, "<!--VER:$proj-->$($versions[$proj])<!--/VER-->")
    }

    if ($updated -ne $content) { $changes += @{ Path = $file; Text = $updated } }
}

if ($Check) {
    if ($stale.Count -gt 0) {
        Write-Error ("Generated doc values are stale:`n  " + ($stale -join "`n  ") +
                     "`nRun: tools\update-readme-counts.ps1")
        exit 1
    }
    Write-Host "Docs are current (tests: $count; versions: " +
               (($versions.Keys | ForEach-Object { "$_ $($versions[$_])" }) -join ', ') + ")."
    exit 0
}

foreach ($change in $changes) {
    # Docs are LF with no BOM.
    $text = $change.Text -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($change.Path, $text, (New-Object System.Text.UTF8Encoding($false)))
}

if ($stale.Count -gt 0) {
    foreach ($s in $stale) { Write-Host "docs updated: $s" }
} else {
    Write-Host "docs already current (tests: $count)."
}
