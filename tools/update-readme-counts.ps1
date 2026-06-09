<#
.SYNOPSIS
  Keeps the test count in README.md in sync with the xUnit test project.
.DESCRIPTION
  Counts [Fact] and [Theory] test methods under KineticRESTIntegrator.Tests and
  rewrites the marked span in README.md:  <!--TESTS-->NNN<!--/TESTS-->
  A [Theory] counts once, regardless of how many [InlineData] cases it carries.
.PARAMETER Check
  Verify only (for a pre-commit hook or CI): exit 1 if the README number is stale,
  without modifying the file.
.EXAMPLE
  powershell -File tools\update-readme-counts.ps1
  powershell -File tools\update-readme-counts.ps1 -Check
#>
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$repo   = Split-Path -Parent $PSScriptRoot
$tests  = Join-Path $repo 'KineticRESTIntegrator.Tests'
$readme = Join-Path $repo 'README.md'

if (-not (Test-Path $tests))  { throw "Test project not found: $tests" }
if (-not (Test-Path $readme)) { throw "README not found: $readme" }

# Count xUnit test methods ([Fact] / [Theory]) across the test project, skipping bin/obj.
$count = 0
Get-ChildItem -Path $tests -Recurse -Filter *.cs |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    ForEach-Object {
        $text = [System.IO.File]::ReadAllText($_.FullName)
        $count += [regex]::Matches($text, '\[\s*(Fact|Theory)\b').Count
    }
if ($count -le 0) { throw "No [Fact]/[Theory] tests found under $tests" }

$content = [System.IO.File]::ReadAllText($readme)
$pattern = '(?s)<!--TESTS-->.*?<!--/TESTS-->'
if (-not [regex]::IsMatch($content, $pattern)) {
    throw "Marker not found in README.md. Add a span like: <!--TESTS-->0<!--/TESTS-->"
}

$current = [regex]::Match($content, '(?s)<!--TESTS-->(.*?)<!--/TESTS-->').Groups[1].Value

if ($Check) {
    if ($current -ne "$count") {
        Write-Error "README test count is stale (README says '$current', actual is $count). Run: tools\update-readme-counts.ps1"
        exit 1
    }
    Write-Host "README test count is current ($count)."
    exit 0
}

$updated = [regex]::Replace($content, $pattern, "<!--TESTS-->$count<!--/TESTS-->")
$updated = $updated -replace "`r`n", "`n"   # README is LF + no BOM
[System.IO.File]::WriteAllText($readme, $updated, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "README test count updated: $current -> $count"
