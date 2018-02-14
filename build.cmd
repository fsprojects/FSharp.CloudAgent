@echo off
cls

IF NOT EXIST packages\FAKE\tools\FAKE.exe  (
    .paket\paket.bootstrapper.exe
    .paket\paket.exe restore
)

cd .\src\FSharp.CloudAgent\
dotnet restore
cd ..\..\tests\FSharp.CloudAgent.Tests\
dotnet restore
cd ..\..\

IF NOT EXIST build.fsx (
  packages\FAKE\tools\FAKE.exe init.fsx
)
packages\FAKE\tools\FAKE.exe build.fsx %*
