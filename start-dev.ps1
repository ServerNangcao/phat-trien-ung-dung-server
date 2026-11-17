# ──────────────────────────────────────────────────────────
# Script chạy đồng thời cả Backend và Frontend (Windows PowerShell)
# ──────────────────────────────────────────────────────────

Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  HỆ THỐNG QUẢN LÝ SỰ KIỆN VÀ BÁN VÉ (TICKETING)   " -ForegroundColor Cyan
Write-Host "====================================================" -ForegroundColor Cyan

$ProjectRoot = $PSScriptRoot
if (-not $ProjectRoot) { $ProjectRoot = (Get-Location).Path }

# 1. Kiểm tra Docker
Write-Host "1. Đang kiểm tra hạ tầng Docker..." -ForegroundColor Yellow
$dockerReady = $false
try {
    docker info >$null 2>&1
    if ($LASTEXITCODE -eq 0) { $dockerReady = $true }
} catch {}

if (-not $dockerReady) {
    Write-Host "[CẢNH BÁO] Docker Desktop hiện chưa chạy trên máy!" -ForegroundColor Red
    Write-Host "-> Để chạy với Backend thật, bạn cần mở ứng dụng 'Docker Desktop' trước." -ForegroundColor Yellow
    Write-Host "-> Nếu chỉ muốn test giao diện không cần Docker, sửa VITE_DATA_MODE=demo trong frontend/.env.development" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "-> Đang khởi động PostgreSQL & Redis qua Docker..." -ForegroundColor Green
    if (Get-Command docker-compose -ErrorAction SilentlyContinue) {
        docker-compose -f "$ProjectRoot\docker-compose.yml" up -d ticketing-db ticketing-redis
    } else {
        docker compose -f "$ProjectRoot\docker-compose.yml" up -d ticketing-db ticketing-redis
    }
}

# 2. Mở cửa sổ Terminal chạy Backend .NET (Hỗ trợ Hot-Reload)
Write-Host "2. Đang mở Terminal chạy Backend API..." -ForegroundColor Green
Start-Process powershell -WorkingDirectory "$ProjectRoot" -ArgumentList "-NoExit", "-Command", "Write-Host '--- BACKEND API (PORT 8080) ---' -ForegroundColor Green; dotnet watch --project backend/src/Api/Ticketing.Api"

# 3. Mở cửa sổ Terminal chạy Frontend Vite React 19
Write-Host "3. Đang mở Terminal chạy Frontend Client..." -ForegroundColor Green
Start-Process powershell -WorkingDirectory "$ProjectRoot\frontend" -ArgumentList "-NoExit", "-Command", "Write-Host '--- FRONTEND CLIENT (PORT 5173) ---' -ForegroundColor Cyan; npm run dev"

Write-Host ""
Write-Host "====================================================" -ForegroundColor Cyan
Write-Host "  HỆ THỐNG ĐÃ GỬI LỆNH KHỞI CHẠY!" -ForegroundColor Yellow
Write-Host "  - Giao diện Web:    http://localhost:5173" -ForegroundColor White
Write-Host "  - Backend API:      http://localhost:8080" -ForegroundColor White
Write-Host "  - Tài liệu OpenAPI: http://localhost:8080/openapi/v1.json" -ForegroundColor White
Write-Host "====================================================" -ForegroundColor Cyan