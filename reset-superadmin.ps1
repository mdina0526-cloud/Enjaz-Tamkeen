param([string]$Username = 'admin')

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'backend\src\SalesSystem.Api'
$database = Join-Path $project 'App_Data\sales.db'
if (-not (Test-Path -LiteralPath $database)) {
    throw "Local SQLite database was not found: $database"
}

$first = Read-Host 'New password' -AsSecureString
$second = Read-Host 'Confirm new password' -AsSecureString
$firstPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($first)
$secondPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($second)
try {
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($firstPointer)
    $confirmation = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secondPointer)
}
finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($firstPointer)
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secondPointer)
}
if ($password -cne $confirmation) { throw 'Passwords do not match.' }

$names = @('ASPNETCORE_ENVIRONMENT', 'Database__Provider', 'ConnectionStrings__Default', 'RESET_SUPERADMIN_USERNAME', 'RESET_SUPERADMIN_PASSWORD')
$previous = @{}
foreach ($name in $names) { $previous[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }

Push-Location $project
try {
    dotnet build --no-restore -p:UseAppHost=false -o App_Data/reset-build
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }

    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:Database__Provider = 'Sqlite'
    $env:ConnectionStrings__Default = 'Data Source=App_Data/sales.db'
    $env:RESET_SUPERADMIN_USERNAME = $Username
    $env:RESET_SUPERADMIN_PASSWORD = $password
    dotnet .\App_Data\reset-build\SalesSystem.Api.dll --reset-superadmin
    if ($LASTEXITCODE -ne 0) { throw 'Password reset failed.' }
}
finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $previous[$name], 'Process') }
    Pop-Location
    $password = $null
    $confirmation = $null
}
