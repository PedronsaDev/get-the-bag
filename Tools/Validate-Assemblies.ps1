param([string]$ProjectRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$ProjectRoot=[System.IO.Path]::GetFullPath($ProjectRoot)
$root=Join-Path $ProjectRoot 'Assets/_Project/Scripts'
$assemblies=@{}; $guids=@{}; $errors=[System.Collections.Generic.List[string]]::new()
Get-ChildItem $root -Recurse -Filter *.asmdef | ForEach-Object {
    $a=Get-Content $_.FullName -Raw|ConvertFrom-Json
    if($assemblies.ContainsKey($a.name)){$errors.Add("Duplicate assembly: $($a.name)")}
    $assemblies[$a.name]=$a
    if(Test-Path ($_.FullName+'.meta')){$g=[regex]::Match((Get-Content ($_.FullName+'.meta') -Raw),'guid: (\w+)').Groups[1].Value;$guids[$g]=$a.name}
}
$edges=@{}
foreach($name in $assemblies.Keys){
    $refs=@($assemblies[$name].references|ForEach-Object {if($_ -like 'GUID:*' -and $guids.ContainsKey($_.Substring(5))){$guids[$_.Substring(5)]}else{$_}})
    if(@($refs|Select-Object -Unique).Count -ne $refs.Count){$errors.Add("Duplicate references: $name")}
    $edges[$name]=@($refs|Where-Object {$assemblies.ContainsKey($_)})
    if($name -notlike '*.Editor' -and @($edges[$name]|Where-Object {$_ -like '*.Editor'}).Count){$errors.Add("Runtime references Editor: $name")}
}
$marks=@{}
function Visit([string]$name,[string[]]$trail){
    if($marks[$name] -eq 1){$errors.Add('Assembly cycle: '+(($trail+@($name))-join ' -> '));return}
    if($marks[$name] -eq 2){return}
    $marks[$name]=1
    foreach($next in $edges[$name]){Visit $next ($trail+@($name))}
    $marks[$name]=2
}
foreach($name in $assemblies.Keys){Visit $name @()}
$allowed=@{'Project.Definitions'=@('Project.Logging');'Project.Content'=@('Project.Definitions','Project.Logging');'Project.Core'=@('Project.Definitions','Project.Logging');'Project.Networking'=@('Project.Logging')}
foreach($name in $allowed.Keys){foreach($reference in $edges[$name]){if($reference -notin $allowed[$name]){$errors.Add("Layer violation: $name -> $reference")}}}
foreach($file in Get-ChildItem $root -Recurse -Filter *.cs){
    $dir=$file.Directory
    while($dir -and $dir.FullName.StartsWith($root,[System.StringComparison]::OrdinalIgnoreCase) -and -not (Get-ChildItem $dir.FullName -Filter *.asmdef -File)){$dir=$dir.Parent}
    if(-not $dir -or -not $dir.FullName.StartsWith($root,[System.StringComparison]::OrdinalIgnoreCase)){$errors.Add("Script outside explicit assembly: $($file.FullName)")}
}
if($errors.Count){$errors|ForEach-Object {Write-Output $_};throw 'Assembly validation failed.'}
Write-Output "Passed: $($assemblies.Count) assemblies, no project dependency cycles or layer violations, all scripts have explicit assembly ownership."
