param(
    [switch]$NoCache
)

$ErrorActionPreference = 'Stop'

$ImageName = 'mobai-edge-gateway:latest'
$ContainerName = 'mobai-edge-gateway'
$DataVolume = 'mobai-edge-gateway-data'
$OutputVolume = 'mobai-edge-gateway-output'
$Port = if ([string]::IsNullOrWhiteSpace($env:EDGE_GATEWAY_PORT)) { '5000' } else { $env:EDGE_GATEWAY_PORT }
$DemoModeEnabled = if ([string]::IsNullOrWhiteSpace($env:DEMO_MODE_ENABLED)) { 'true' } else { $env:DEMO_MODE_ENABLED }

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker 命令执行失败：docker $($Arguments -join ' ')"
    }
}

Write-Host '检查 Docker 环境...' -ForegroundColor Cyan
Invoke-Docker -Arguments @('version', '--format', '{{.Server.Version}}')

$RunningContainerId = ((& docker ps -q --filter "name=^/$ContainerName$") -join '').Trim()
if ($LASTEXITCODE -ne 0) {
    throw '无法查询 Docker 容器。'
}

if (-not [string]::IsNullOrWhiteSpace($RunningContainerId)) {
    Write-Host "停止服务：$ContainerName" -ForegroundColor Yellow
    Invoke-Docker -Arguments @('stop', $ContainerName)
}

$ExistingContainerId = ((& docker ps -aq --filter "name=^/$ContainerName$") -join '').Trim()
if ($LASTEXITCODE -ne 0) {
    throw '无法查询 Docker 容器。'
}

if (-not [string]::IsNullOrWhiteSpace($ExistingContainerId)) {
    Write-Host "移除旧容器：$ContainerName" -ForegroundColor Yellow
    Invoke-Docker -Arguments @('rm', $ContainerName)
}

& docker image inspect $ImageName *> $null
$ImageInspectExitCode = $LASTEXITCODE
if ($ImageInspectExitCode -eq 0) {
    Write-Host "删除旧镜像：$ImageName" -ForegroundColor Yellow
    Invoke-Docker -Arguments @('image', 'rm', '-f', $ImageName)
} elseif ($ImageInspectExitCode -ne 1) {
    throw "无法检查镜像：$ImageName"
}

Write-Host "创建或复用数据卷：$DataVolume、$OutputVolume" -ForegroundColor Cyan
Invoke-Docker -Arguments @('volume', 'create', $DataVolume)
Invoke-Docker -Arguments @('volume', 'create', $OutputVolume)

$BuildArguments = @('build', '--tag', $ImageName)
if ($NoCache) {
    $BuildArguments += '--no-cache'
}
$BuildArguments += '.'

Write-Host "构建镜像：$ImageName" -ForegroundColor Cyan
Invoke-Docker -Arguments $BuildArguments

$PortMapping = "${Port}:5000"
Write-Host "启动服务：$ContainerName，端口映射 $PortMapping" -ForegroundColor Cyan
Invoke-Docker -Arguments @(
    'run', '-d',
    '--name', $ContainerName,
    '--restart', 'unless-stopped',
    '--publish', $PortMapping,
    '--env', 'ASPNETCORE_ENVIRONMENT=Production',
    '--env', 'ASPNETCORE_URLS=http://+:5000',
    '--env', 'Database__ConnectionString=Data Source=/data/gateway.db',
    '--env', "DemoMode__Enabled=$DemoModeEnabled",
    '--volume', "${DataVolume}:/data",
    '--volume', "${OutputVolume}:/app/output",
    $ImageName
)

Write-Host '部署完成。' -ForegroundColor Green
Write-Host "访问地址：http://localhost:$Port" -ForegroundColor Green
