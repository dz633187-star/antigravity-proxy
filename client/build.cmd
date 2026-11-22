@echo off
setlocal
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

echo [1/3] Compiling AntigravityProxy.exe...
"%CSC%" /nologo /target:winexe /win32icon:"%~dp0AntigravityProxy.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /out:"%~dp0AntigravityProxy.exe" "%~dp0AntigravityProxy.cs"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to compile AntigravityProxy.exe
    exit /b %errorlevel%
)

echo [2/3] Compiling Uninstall.exe...
"%CSC%" /nologo /target:winexe /win32icon:"%~dp0AntigravityProxy.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:"%~dp0Uninstall.exe" "%~dp0Uninstall.cs"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to compile Uninstall.exe
    exit /b %errorlevel%
)

echo [3/3] Compiling AntigravityProxySetup.exe (self-contained)...
"%CSC%" /nologo /target:winexe /win32icon:"%~dp0AntigravityProxy.ico" /r:Microsoft.CSharp.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /resource:"%~dp0AntigravityProxy.exe",AntigravityProxy.exe /resource:"%~dp0Uninstall.exe",Uninstall.exe /resource:"%~dp0AntigravityProxy.ico",AntigravityProxy.ico /out:"%~dp0AntigravityProxySetup.exe" "%~dp0Installer.cs"
if %errorlevel% neq 0 (
    echo [ERROR] Failed to compile AntigravityProxySetup.exe
    exit /b %errorlevel%
)

echo [OK] All targets compiled successfully!
