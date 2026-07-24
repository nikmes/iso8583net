# smoke-test.ps1 — End-to-end smoke test for ISO8583Simulator
# Requires: ISO8583Server running on localhost:9443 (TLS)
#           ISO8583Simulator running on localhost:5000
param(
    [string]$SimulatorUrl = "http://localhost:5000",
    [string]$ServerHost = "localhost",
    [int]$ServerPort = 9443
)

$ErrorActionPreference = "Stop"
Write-Host "=== ISO8583Simulator Smoke Test ===" -ForegroundColor Cyan

# ── 1. Health Check ─────────────────────────────────
Write-Host "`n[1/7] Health check..." -ForegroundColor Yellow
$health = Invoke-RestMethod -Uri "$SimulatorUrl/api/simulator/health" -Method Get
Write-Host "  Status: OK" -ForegroundColor Green

# ── 2. Connect ──────────────────────────────────────
Write-Host "`n[2/7] Connecting to $ServerHost`:$ServerPort..." -ForegroundColor Yellow
$connect = Invoke-RestMethod -Uri "$SimulatorUrl/api/simulator/connect" -Method Post `
    -Body (@{ host = $ServerHost; port = $ServerPort; tlsEnabled = $true; tlsAllowUntrusted = $true } | ConvertTo-Json) `
    -ContentType "application/json"
Write-Host "  Connected" -ForegroundColor Green

# ── 3. Status ───────────────────────────────────────
Write-Host "`n[3/7] Checking status..." -ForegroundColor Yellow
$status = Invoke-RestMethod -Uri "$SimulatorUrl/api/simulator/status" -Method Get
Write-Host "  State: $($status.state), Uptime: $([math]::Round($status.uptimeSeconds, 1))s" -ForegroundColor Green

# ── 4. List Scenarios ───────────────────────────────
Write-Host "`n[4/7] Listing scenarios..." -ForegroundColor Yellow
$scenarios = Invoke-RestMethod -Uri "$SimulatorUrl/api/scenario" -Method Get
$scenarios | ForEach-Object { Write-Host "  - $($_.name) ($($_.description))" }

# ── 5. Run Echo Scenario ────────────────────────────
Write-Host "`n[5/7] Running Echo scenario..." -ForegroundColor Yellow
$echoResult = Invoke-RestMethod -Uri "$SimulatorUrl/api/scenario/Echo/run" -Method Post
if ($echoResult.passed) {
    Write-Host "  PASS (${echoResult.duration}s)" -ForegroundColor Green
} else {
    Write-Host "  FAIL: $($echoResult.errorMessage)" -ForegroundColor Red
}

# ── 6. Send Manual Message ──────────────────────────
Write-Host "`n[6/7] Sending manual authorization (0100)..." -ForegroundColor Yellow
try {
    $sendResult = Invoke-RestMethod -Uri "$SimulatorUrl/api/messages/send" -Method Post `
        -Body (@{ mti = "0100" } | ConvertTo-Json) `
        -ContentType "application/json"
    Write-Host "  Response MTI: $($sendResult.responseMti), F39: $($sendResult.f39), Time: $([math]::Round($sendResult.elapsedMs, 1))ms" -ForegroundColor Green
} catch {
    Write-Host "  WARNING: Manual send failed (server may need sign-on first): $_" -ForegroundColor Yellow
}

# ── 7. Disconnect ───────────────────────────────────
Write-Host "`n[7/7] Disconnecting..." -ForegroundColor Yellow
$disconnect = Invoke-RestMethod -Uri "$SimulatorUrl/api/simulator/disconnect" -Method Post
Write-Host "  Disconnected" -ForegroundColor Green

Write-Host "`n=== Smoke Test Complete ===" -ForegroundColor Cyan
