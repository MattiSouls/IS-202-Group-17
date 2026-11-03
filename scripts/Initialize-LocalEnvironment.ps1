param(
    [ValidateRange(1024, 65535)][int]$DatabasePort = 3307,
    [ValidateRange(1024, 65535)][int]$WebPort = 8080
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$environmentPath = Join-Path $repositoryRoot '.env'

if (Test-Path -LiteralPath $environmentPath) {
    Write-Output '.env finnes allerede. Den eksisterende konfigurasjonen er beholdt.'
    return
}

function New-LocalPassword {
    $bytes = New-Object byte[] 24
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $generator.Dispose()
    }
}

$databasePassword = New-LocalPassword
$rootPassword = New-LocalPassword
$contents = @"
MARIADB_DATABASE=resourcesdb
MARIADB_USER=resourcesapp
MARIADB_PASSWORD=$databasePassword
MARIADB_ROOT_PASSWORD=$rootPassword
DB_PORT=$DatabasePort
WEB_PORT=$WebPort
"@

[System.IO.File]::WriteAllText($environmentPath, $contents + [Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false))
Write-Output '.env er opprettet med lokale, tilfeldige passord. Filen ignoreres av Git.'
