Param(
    [string]$Script = "build.cake",
    [string]$Target = "Default",
    [string]$Configuration = "Release",
    [string]$Verbosity = "Verbose",
    [switch]$Nightly
)

# Define the experimental flag.
#$Experimental = "";
#if($Nightly.IsPresent) {
#    $Experimental = "-experimental"
#}
$Experimental = "-experimental"

$TOOLS_DIR = Join-Path $PSScriptRoot "tools"
$NUGET_EXE = Join-Path $TOOLS_DIR "nuget.exe"
$CAKE_EXE = Join-Path $TOOLS_DIR "Cake/Cake.exe"
$PACKAGES_CONFIG = Join-Path $TOOLS_DIR "packages.config"

# Make sure tools folder exists
if ((Test-Path $PSScriptRoot) -and !(Test-Path $TOOLS_DIR)) {
    New-Item -path $TOOLS_DIR -name logfiles -itemtype directory
}

# Try find NuGet.exe in path if not exists
if (!(Test-Path $NUGET_EXE)) {
    "Trying to find nuget.exe in path"
    $NUGET_EXE_IN_PATH = &where.exe nuget.exe
    if ($NUGET_EXE_IN_PATH -ne $null -and (Test-Path $NUGET_EXE_IN_PATH)) {
        "Found $($NUGET_EXE_IN_PATH)"
        $NUGET_EXE = $NUGET_EXE_IN_PATH 
    }
}

# Try download NuGet.exe if not exists
if (!(Test-Path $NUGET_EXE)) {
    Invoke-WebRequest -Uri http://nuget.org/nuget.exe -OutFile $NUGET_EXE
}

# Make sure NuGet exists where we expect it.
if (!(Test-Path $NUGET_EXE)) {
    Throw "Could not find NuGet.exe"
}

# Save nuget.exe path to environment to be available to child processed
$ENV:NUGET_EXE = $NUGET_EXE

# Restore tools from NuGet.
Push-Location
Set-Location $TOOLS_DIR

# Restore packages
if (Test-Path $PACKAGES_CONFIG)
{
    Invoke-Expression "&`"$NUGET_EXE`" install -ExcludeVersion"
}
# Install just Cake if missing config
else
{
    Invoke-Expression "&`"$NUGET_EXE`" install Cake -ExcludeVersion"
}
Pop-Location
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}

# Make sure that Cake has been installed.
if (!(Test-Path $CAKE_EXE)) {
    Throw "Could not find Cake.exe"
}

# Start Cake
Invoke-Expression "$CAKE_EXE `"$Script`" -target=`"$Target`" -configuration=`"$Configuration`" -verbosity=`"$Verbosity`" $Experimental"
exit $LASTEXITCODE
