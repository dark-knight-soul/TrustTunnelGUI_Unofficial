@echo off
rem Builds the portable and installer packages into dist\ (see scripts\package.ps1).
rem   build.cmd        release packages
rem   build.cmd -Dev   development executables with self-tests in dist\dev
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0package.ps1" %*
exit /b %errorlevel%
