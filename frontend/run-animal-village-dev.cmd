@echo off
cd /d "%~dp0"
"C:\Users\User\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe" "%~dp0node_modules\vite\bin\vite.js" --host 0.0.0.0 --port 5173
