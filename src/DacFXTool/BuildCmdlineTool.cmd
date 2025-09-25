
dotnet publish -o bin\Release\net8.0\x64\publish -f net8.0 -c Release  --no-self-contained

if %errorlevel% equ 1 goto notbuilt

rd bin\Release\net8.0\x64\publish\runtimes\linux /Q /S
rd bin\Release\net8.0\x64\publish\runtimes\unix /Q /S
rd bin\Release\net8.0\x64\publish\runtimes\win-arm /Q /S
rd bin\Release\net8.0\x64\publish\runtimes\win-x86 /Q /S

"C:\Program Files\7-Zip\7z.exe" -mm=Deflate -mfb=258 -mpass=15 a dacfxtool.exe.zip .\bin\Release\net8.0\x64\publish\*

move /Y dacfxtool.exe.zip ..\lib\

goto end

:notbuilt
echo Build error

:end
