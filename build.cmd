@echo off
cls

.paket\paket.exe restore

cd .\src\FSharp.CloudAgent\
dotnet restore
cd ..\..\tests\FSharp.CloudAgent.Tests\
dotnet restore
cd ..\..\

packages\build\FAKE\tools\FAKE.exe build.fsx %*
