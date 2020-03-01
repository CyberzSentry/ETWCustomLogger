@ECHO OFF

IF "%~1"=="i" (
echo Installing the ETWLoggingService...
sc create "ETWLoggingService" binpath= %~dp0\ETWLogger.exe start= auto
sc start ETWLoggingService
)
IF "%~1"=="u" (
echo Uninstalling the ETWLoggingService...
sc stop ETWLoggingService
sc delete ETWLoggingService
)