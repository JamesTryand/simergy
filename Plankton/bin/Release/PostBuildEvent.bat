@echo off
copy "C:\Documents and Settings\Steve\My Documents\Visual Studio Projects\Simbiosis\Plankton\bin\Release\Plankton.dll" "C:\Documents and Settings\Steve\My Documents\Visual Studio Projects\Simbiosis\Simbiosis\bin\Debug"
if errorlevel 1 goto CSharpReportError
goto CSharpEnd
:CSharpReportError
echo Project error: A tool returned an error code from the build event
exit 1
:CSharpEnd