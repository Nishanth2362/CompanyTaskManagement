# ============================================================
# KillAndRun.ps1 - Kill port 5231 and restart the app
# Right-click this file > "Run with PowerShell" to use it
# ============================================================

Write-Host "Stopping any process using port 5231..." -ForegroundColor Yellow

Get-NetTCPConnection -LocalPort 5231 -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess |
    Where-Object { $_ -gt 0 } |
    ForEach-Object { 
        taskkill /PID $_ /F 2>$null
        Write-Host "  Killed PID $_" -ForegroundColor Red
        
    }

# Also kill any leftover CompanyTaskManagement processes
Get-Process -Name "CompanyTaskManagement" -ErrorAction SilentlyContinue | 
    ForEach-Object { 
        $_.Kill()
        Write-Host "  Killed CompanyTaskManagement process" -ForegroundColor Red
    }

Start-Sleep -Seconds 1
Write-Host "Port 5231 is now free. Starting app..." -ForegroundColor Green

Set-Location "$PSScriptRoot\CompanyTaskManagement"
dotnet run --project CompanyTaskManagement.csproj
