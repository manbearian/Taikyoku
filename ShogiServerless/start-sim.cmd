@setlocal
@pushd %~dp0
start "Azure Functions Simulator" asrs-emulator start
start "Azure Storage Simulator" "C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe"