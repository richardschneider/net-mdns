dotnet build -c Release --framework netcoreapp2 ./test

:Loop
dotnet test --logger "console;verbosity=normal" -c Release --no-restore --no-build --framework netcoreapp2 ./test --filter TestCategory!=IPv6
if %errorlevel% equ 0 goto :Loop
echo Connection established
