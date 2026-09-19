param(
    [string]$BaselineRef = 'd667dc99723922e29e852378e36fd598965a15fc',
    [string[]]$Filters = @('*'),
    [string]$ReportPath = 'artifacts/throughput-parity-warmed'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    # Compile both versions with identical settings. Never replace working-tree sources.
    # Distinct namespaces let BenchmarkDotNet generate unambiguous return types.
    $sourceRoot = Join-Path $repoRoot 'artifacts/throughput-sources'
    if (Test-Path $sourceRoot) { throw 'Throughput snapshots already exist. Use the generated benchmark project to rerun them.' }
    foreach ($version in @('Before', 'After')) {
        $destination = Join-Path $sourceRoot $version
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        if ($version -eq 'Before') {
            $files = git ls-tree -r --name-only $BaselineRef -- src/BbQ.Outcome
            if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve baseline commit' }
            foreach ($file in $files | Where-Object { $_.EndsWith('.cs') }) {
                $contents = git show "${BaselineRef}:$file"
                if ($LASTEXITCODE -ne 0) { throw "Cannot read $file" }
                Set-Content -LiteralPath (Join-Path $destination (Split-Path $file -Leaf)) -Value ($contents -replace 'BbQ\.Outcome', "Parity.$version") -Encoding utf8
            }
        } else {
            foreach ($file in Get-ChildItem src/BbQ.Outcome -Filter '*.cs') {
                $contents = Get-Content -LiteralPath $file.FullName -Raw
                Set-Content -LiteralPath (Join-Path $destination $file.Name) -Value ($contents -replace 'BbQ\.Outcome', "Parity.$version") -Encoding utf8
            }
        }
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>Outcome.$version</AssemblyName>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $destination "$version.csproj") -Encoding utf8
    }
    Get-ChildItem $sourceRoot -Recurse -Filter '*.cs' | Get-FileHash |
        Select-Object Path, Hash | ConvertTo-Json | Set-Content (Join-Path $sourceRoot 'source-hashes.json')
    dotnet run -c Release --project tests/BbQ.Outcome.ThroughputBenchmarks -- --filter @Filters --launchCount 2 --warmupCount 30 --iterationCount 12 --iterationTime 200 --affinity 1 --exporters json --artifacts $ReportPath
    if ($LASTEXITCODE -ne 0) { throw 'Throughput benchmark run failed' }
} finally {
    Pop-Location
}
