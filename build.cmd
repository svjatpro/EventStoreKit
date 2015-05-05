
 @set target=%1
 @if "%target%" == "" set target=Test

 @call "%VS100COMNTOOLS%\vsvars32.bat"
 @path = %path%;%LIBPATH%

 set BUILD_NUMBER=0.9.3.0

 @call MSBuild.exe EventStoreKit.build  /nologo /t:%target% /v:n /l:FileLogger,Microsoft.Build.Engine;logfile="build.log";encoding=utf-8
 