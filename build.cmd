@echo off
cls

IF NOT EXIST packages\FAKE\tools\FAKE.exe  (
    .paket\paket.bootstrapper.exe
    .paket\paket.exe restore
)

IF NOT EXIST build.fsx (
  packages\FAKE\tools\FAKE.exe init.fsx
)
packages\FAKE\tools\FAKE.exe build.fsx %*
