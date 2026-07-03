#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Installs or uninstalls the SendEmailService Windows Service.

.DESCRIPTION
    This script automates the installation and configuration of the
    SendEmailService as a Windows Service using sc.exe.

.PARAMETER Action
    The action to perform: Install, Uninstall, or Status.

.PARAMETER ServiceName
    Name of the Windows Service. Default: SendEmailService.

.PARAMETER DisplayName
    Display name shown in Services console. Default: Send Email Service.

.PARAMETER BinPath
    Full path to the service executable (.exe).
    Default: resolved relative to this script's directory.

.PARAMETER StartupType
    Service startup type: Automatic, Manual, Disabled. Default: Automatic.

.PARAMETER ServiceAccount
    Account to run the service under. Default: LocalService.
    Use 'NT AUTHORITY\NetworkService' or 'DOMAIN\ServiceAccount' as needed.

.EXAMPLE
    .\install-service.ps1 -Action Install -BinPath "C:\Services\SendEmailService\SendEmailService.Worker.exe"

.EXAMPLE
    .\install-service.ps1 -Action Uninstall

.EXAMPLE
    .\install-service.ps1 -Action Status
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Install', 'Uninstall', 'Status')]
    [string]$Action,

    [string]$ServiceName   = 'SendEmailService',
    [string]$DisplayName   = 'Send Email Service',
    [string]$BinPath       = '',
    [ValidateSet('Automatic', 'Manual', 'Disabled')]
    [string]$StartupType   = 'Automatic',
    [string]$ServiceAccount = 'LocalService'
)

$ErrorActionPreference = 'Stop'

function Get-DefaultBinPath {
    $scriptDir = Split-Path -Parent $MyInvocation.ScriptName
    $candidate = Join-Path $scriptDir '..\src\SendEmailService.Worker\bin\Release\net8.0\win-x64\publish\SendEmailService.Worker.exe'
    return [System.IO.Path]::GetFullPath($candidate)
}

function Test-ServiceExists {
    param([string]$Name)
    return [bool](Get-Service -Name $Name -ErrorAction SilentlyContinue)
}

switch ($Action) {

    'Install' {
        if ([string]::IsNullOrWhiteSpace($BinPath)) {
            $BinPath = Get-DefaultBinPath
        }

        if (-not (Test-Path $BinPath)) {
            Write-Error "Executable not found: $BinPath`nPlease publish the project first:`n  dotnet publish -c Release -r win-x64 --self-contained"
            exit 1
        }

        if (Test-ServiceExists -Name $ServiceName) {
            Write-Host "Service '$ServiceName' already exists. Stopping before reinstalling..." -ForegroundColor Yellow
            Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
            sc.exe delete $ServiceName | Out-Null
            Start-Sleep -Seconds 2
        }

        Write-Host "Installing service '$ServiceName'..." -ForegroundColor Cyan

        $scArgs = @(
            'create', $ServiceName,
            'binPath=', "`"$BinPath`"",
            'DisplayName=', "`"$DisplayName`"",
            'start=', ($StartupType.ToLower() -replace 'automatic', 'auto' -replace 'manual', 'demand' -replace 'disabled', 'disabled'),
            'obj=', "NT AUTHORITY\$ServiceAccount"
        )

        $result = sc.exe @scArgs
        if ($LASTEXITCODE -ne 0) {
            Write-Error "sc.exe create failed: $result"
            exit 1
        }

        # Set description
        sc.exe description $ServiceName "Processes emails from tbl_sendMailQueue using SparkPost." | Out-Null

        # Configure recovery actions: restart service after 1st, 2nd, 3rd failure (60s delay)
        sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null

        Write-Host "Starting service '$ServiceName'..." -ForegroundColor Cyan
        Start-Service -Name $ServiceName

        $svc = Get-Service -Name $ServiceName
        Write-Host "Service installed and started successfully." -ForegroundColor Green
        Write-Host "  Status:  $($svc.Status)"
        Write-Host "  StartType: $($svc.StartType)"
    }

    'Uninstall' {
        if (-not (Test-ServiceExists -Name $ServiceName)) {
            Write-Warning "Service '$ServiceName' does not exist."
            exit 0
        }

        Write-Host "Stopping service '$ServiceName'..." -ForegroundColor Cyan
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue

        Write-Host "Deleting service '$ServiceName'..." -ForegroundColor Cyan
        sc.exe delete $ServiceName | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Service '$ServiceName' uninstalled successfully." -ForegroundColor Green
        } else {
            Write-Error "sc.exe delete returned exit code $LASTEXITCODE"
        }
    }

    'Status' {
        if (Test-ServiceExists -Name $ServiceName) {
            $svc = Get-Service -Name $ServiceName
            Write-Host "Service: $($svc.DisplayName)"
            Write-Host "  Status:    $($svc.Status)"
            Write-Host "  StartType: $($svc.StartType)"
        } else {
            Write-Warning "Service '$ServiceName' is not installed."
        }
    }
}
