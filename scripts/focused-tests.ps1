<#
Focused, connected test selection. It never runs the whole solution.

  .\scripts\focused-tests.ps1 -Feature economy -List        # which test projects a feature selects
  .\scripts\focused-tests.ps1 -Feature economy               # build and run just those
  .\scripts\focused-tests.ps1 -Changed src/EngineCommon/EngineCommon.Graph -List
  .\scripts\focused-tests.ps1 -Changed src/EconomyEngine/EconomyEngine.Core/EconomyRuntime.Landed.cs
  .\scripts\focused-tests.ps1 -Feature relationship -Direct  # only the feature's own tests, not dependents

Selection rule ("connected"): the changed paths, or a feature's roots from scripts/focused-test-groups.json, are mapped to the
projects that own them; every test project (*Tests.csproj) whose ProjectReference closure contains one of those projects is
selected. A change to the shared graph library therefore selects the Graph, Economy, WorldEvents, Relationship and the runtime
and bridge tests that use it, and nothing else. -Direct keeps only test projects whose own folder is under a root.
#>
param(
    [string]$Feature,
    [string[]]$Changed,
    [switch]$List,
    [switch]$Direct
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$groupsPath = Join-Path $PSScriptRoot 'focused-test-groups.json'

function Resolve-Full([string]$path) { [System.IO.Path]::GetFullPath($path) }

# Project graph: csproj -> csprojs it references.
$projects = Get-ChildItem -Path (Join-Path $root 'src') -Recurse -Filter *.csproj |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$refs = @{}
foreach ($project in $projects) {
    $xml = [xml](Get-Content -LiteralPath $project.FullName -Raw)
    $targets = @()
    foreach ($node in $xml.SelectNodes('//ProjectReference')) {
        $targets += Resolve-Full (Join-Path $project.DirectoryName ($node.Include -replace '/', '\'))
    }
    $refs[$project.FullName] = $targets
}

function Get-Closure([string]$project) {
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $stack = [System.Collections.Generic.Stack[string]]::new()
    $stack.Push($project)
    while ($stack.Count -gt 0) {
        $current = $stack.Pop()
        if (-not $seen.Add($current)) { continue }
        if ($refs.ContainsKey($current)) { foreach ($next in $refs[$current]) { $stack.Push($next) } }
    }
    return $seen
}

# Roots: folders or files, from -Changed or a feature group.
$rootsIn = @()
if ($Feature) {
    $groups = Get-Content -LiteralPath $groupsPath -Raw | ConvertFrom-Json
    $names = @($groups.PSObject.Properties.Name)
    if ($names -notcontains $Feature) {
        Write-Error ("unknown feature '$Feature'. Known: " + ($names -join ', '))
    }
    $rootsIn += @($groups.$Feature)
}
if ($Changed) { $rootsIn += $Changed }
if ($rootsIn.Count -eq 0) { Write-Error 'give -Feature <name> or -Changed <path...> (see scripts/focused-tests.ps1 header)' }

$rootPaths = $rootsIn | ForEach-Object { Resolve-Full (Join-Path $root ($_ -replace '/', '\')) }

# A root owns the projects whose folder is at or under it, or (for a file) the nearest enclosing project.
$owned = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($path in $rootPaths) {
    foreach ($project in $projects) {
        $dir = $project.DirectoryName
        if ($dir.StartsWith($path, [StringComparison]::OrdinalIgnoreCase) -or
            $path.StartsWith($dir + '\', [StringComparison]::OrdinalIgnoreCase)) { [void]$owned.Add($project.FullName) }
    }
}
if ($owned.Count -eq 0) { Write-Error 'no project owns the given paths' }

$tests = $projects | Where-Object { $_.Name -like '*Tests.csproj' }
$selected = @()
foreach ($test in $tests) {
    if ($Direct) {
        $hit = $rootPaths | Where-Object { $test.DirectoryName.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }
        if ($hit) { $selected += $test }
        continue
    }
    $closure = Get-Closure $test.FullName
    foreach ($o in $owned) { if ($closure.Contains($o)) { $selected += $test; break } }
}
# EngineCommon.Tests reads the shipped sources of the engines and the game layer from disk (it references none of them), so the closure rule cannot see it:
# a change to any of those sources selects it too, unless -Direct. Without this, a change that breaks a repository-wide source guard would pass the focused run.
if (-not $Direct) {
    $scanned = '[\\/]src[\\/](EngineCommon|AIEngine|DialogueEngine|CalendarEngine|CombatEngine|RelationshipEngine|EconomyEngine|TutorialEngine|WorldEventsEngine|DiplomacyEngine|GameRuntime)[\\/]'
    $touchesScanned = $owned | Where-Object { $_ -match $scanned -and $_ -notmatch '\.(Tests|TestSupport)\.csproj$' }
    if ($touchesScanned) { $selected += $tests | Where-Object { $_.BaseName -eq 'EngineCommon.Tests' } }
}
$selected = $selected | Sort-Object FullName -Unique
if ($selected.Count -eq 0) { Write-Error 'no test project covers the selection' }

Write-Host ("selected {0} test project(s):" -f $selected.Count)
$selected | ForEach-Object { Write-Host ('  ' + $_.BaseName) }
if ($List) { return }

$failed = @()
foreach ($test in $selected) {
    Write-Host ("--- " + $test.BaseName)
    $out = & dotnet run --project $test.FullName 2>&1 | Select-String -Pattern 'Total:|\[FAIL\]|error '
    $out | ForEach-Object { Write-Host $_.Line.Trim() }
    if ($LASTEXITCODE -ne 0) { $failed += $test.BaseName }
}
if ($failed.Count -gt 0) { Write-Host ('FAILED: ' + ($failed -join ', ')); exit 1 }
Write-Host 'all selected test projects passed'
