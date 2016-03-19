
 @set target=%1
 @if "%target%" == "" set target=Test

 @call "%VS120COMNTOOLS%\vsvars32.bat"
 @path = %path%;%LIBPATH%

 @call MSBuild.exe EventStoreKit.build  /nologo /t:%target% /v:n /l:FileLogger,Microsoft.Build.Engine;logfile="build.log";encoding=utf-8
 