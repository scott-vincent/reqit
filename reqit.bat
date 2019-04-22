@echo off
setlocal
cd reqit/bin/Release/netcoreapp2.2/publish 
dotnet reqit.dll %*
endlocal
