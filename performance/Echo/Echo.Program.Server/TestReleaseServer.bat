@ECHO OFF

REM current directory를 배치 파일 경로로 지정
CD /D %~DP0

CD bin
CD Release

"Echo.Program.Server.exe" ../../Config.Server.sample.json