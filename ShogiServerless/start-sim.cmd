@setlocal
@pushd %~dp0
start "Azure Functions Simulator" asrs-emulator start
if not exist .azurite md .azurite
@pushd .azurite
start "Azure Storage Simulator" "C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\Extensions\Microsoft\Azure Storage Emulator\azurite.exe"
