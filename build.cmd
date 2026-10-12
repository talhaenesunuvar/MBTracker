@echo off
rem Windows ile gelen C# derleyicisini kullanir, ek kurulum gerekmez.
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%FW%\csc.exe" /nologo /codepage:65001 /target:winexe /optimize+ /out:"%~dp0InternetSayaci.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%FW%\System.Runtime.dll" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Foundation.winmd" ^
  /r:"%WINDIR%\System32\WinMetadata\Windows.Networking.winmd" ^
  "%~dp0Program.cs"
