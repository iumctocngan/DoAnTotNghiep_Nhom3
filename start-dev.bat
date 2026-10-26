@echo off
title CMS Edu Development Starter
echo Dang khoi dong he thong CMS Edu...

start "CMS Edu - AI Module (Port 8000)" cmd /k "cd /d %~dp0module-ai\module2-rag\src && python -m uvicorn main:app --host 0.0.0.0 --port 8000"
start "CMS Edu - Backend (Port 5151)" cmd /k "cd /d %~dp0backend\src\CmsEdu.Api && dotnet run"
start "CMS Edu - Frontend (Port 5173)" cmd /k "cd /d %~dp0frontend && npm run dev"

echo Tat ca cac dich vu da duoc khoi dong.
