Write-Host "=== Stopping existing CompanyTaskManagement instances ===" -ForegroundColor Cyan
Get-NetTCPConnection -LocalPort 5231 -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }
Stop-Process -Name "CompanyTaskManagement" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Write-Host "=== Port 5231 freed. Starting app... ===" -ForegroundColor Green
Set-Location $PSScriptRoot
dotnet run --project CompanyTaskManagement
