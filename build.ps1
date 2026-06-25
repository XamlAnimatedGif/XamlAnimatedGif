#!/usr/bin/env pwsh
try {
    Push-Location $PSScriptRoot
    dotnet run "./tools/Build.cs" -- $args
    if ($LASTEXITCODE) { Throw "Build failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}