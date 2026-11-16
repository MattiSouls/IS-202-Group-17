$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
try {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Installer Docker Desktop med Linux-containere forst, og start PC-en pa nytt hvis installasjonen ber om det.'
    }
    if (-not (Test-Path -LiteralPath '.env')) {
        $dbPassword = [Guid]::NewGuid().ToString('N')
        $rootPassword = [Guid]::NewGuid().ToString('N')
        @"
MARIADB_DATABASE=resourcesdb
MARIADB_USER=resourcesapp
MARIADB_PASSWORD=$dbPassword
MARIADB_ROOT_PASSWORD=$rootPassword
WEB_PORT=8080
DB_PORT=3307
EXPO_DEMO_ENABLED=true
EXPO_DEMO_DATE=2026-11-16
SEED_PUBLIC_EMAIL=crisis-demo@example.test
SEED_PUBLIC_PASSWORD=ReviewPublic123
SEED_PROVIDER_EMAIL=provider-ux@example.test
SEED_PROVIDER_PASSWORD=ReviewProvider123
"@ | Set-Content -LiteralPath '.env' -Encoding ASCII
        Write-Host 'Laget lokal .env med egne databasepassord og syntetiske demokontoer.'
    }
    if (-not (Test-Path '\\.\pipe\dockerDesktopLinuxEngine')) {
        $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (-not (Test-Path -LiteralPath $desktop)) { throw 'Start Docker Desktop manuelt med Linux-containere og prov igjen.' }
        Start-Process -FilePath $desktop -WindowStyle Hidden
        Write-Host 'Venter pa Docker Desktop ...'
        $deadline = (Get-Date).AddMinutes(3)
        while (-not (Test-Path '\\.\pipe\dockerDesktopLinuxEngine')) {
            if ((Get-Date) -gt $deadline) { throw 'Docker startet ikke innen tre minutter. Apne Docker Desktop og sjekk feilmeldingen.' }
            Start-Sleep -Seconds 2
        }
    }
    Write-Host 'Starter app og database. Forste bygg trenger internett og kan ta flere minutter.'
    docker compose -p beredt-expo up -d --build --wait --wait-timeout 180
    if ($LASTEXITCODE -ne 0) { throw 'Oppstart feilet. Les meldingen over; sjekk Docker og ledige porter. Ingen data er slettet.' }
    $portLine = docker compose -p beredt-expo port web 8080
    if ($LASTEXITCODE -ne 0 -or $portLine -notmatch ':(\d+)$') { throw 'Fant ikke webporten. Kjor docker compose -p beredt-expo ps.' }
    $url = 'http://localhost:' + $Matches[1]
    Write-Host "Demo klar: $url"
    Write-Host 'Kontoer og passord finnes i din lokale .env. EXPO-oppsett er beskrevet i README.'
    Start-Process $url
}
catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
