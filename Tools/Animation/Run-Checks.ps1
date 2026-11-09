param(
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
$reportFolder = Join-Path $ProjectPath '.codex/animation-checks'
New-Item -ItemType Directory -Path $reportFolder -Force | Out-Null

$preflight = & unity command script eval --code 'if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new System.Exception("Run checks in stopped Edit Mode with a clean scene."); return true;' --caller plugin --skill unity-cli --project-path $ProjectPath --format json
$reply = ($preflight | Out-String) | ConvertFrom-Json
if ($LASTEXITCODE -ne 0 -or !$reply.success) { throw ($preflight | Out-String) }

$checks = @(
    'VerifyGroupedWorldReload',
    'VerifyGroupedBodyReload',
    'VerifyGroupedPistolEmptyPlayback',
    'VerifyGroupedReloadValidation',
    'VerifyOwnerAnimationVisibility'
)

$total = 0
foreach ($name in $checks) {
    $file = Join-Path $PSScriptRoot "Checks/$name.cs"
    $raw = & unity command script run --file $file --entry "$name.Main" --caller plugin --skill unity-cli --project-path $ProjectPath --format json
    $reply = ($raw | Out-String) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or !$reply.success) { throw ($raw | Out-String) }
    $report = $reply.result | ConvertFrom-Json
    $failed = @($report.checks | Where-Object { !$_.passed })
    if (!$report.checks -or $failed.Count) { throw "$name failed: $($failed | ConvertTo-Json -Compress)" }
    $count = @($report.checks).Count
    $total += $count
    Write-Output "$name : $count passed"
}
Write-Output "$total checks passed. Reports: $reportFolder"
