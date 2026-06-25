@pushd %~dp0
@dotnet run %~dp0\tools\Build.cs -- %*
@popd