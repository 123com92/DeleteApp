param(
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net8.0-windows",
    [string]$OutputDir = "publish"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$dotnetRoot = Join-Path $scriptDir ".dotnet"
if (Test-Path $dotnetRoot) {
    $env:DOTNET_ROOT = $dotnetRoot
    $env:PATH = "$dotnetRoot;$env:PATH"
}

Write-Host "=== Windows Rogue Software Cleaner - 打包发布 ===" -ForegroundColor Cyan
Write-Host ""

$csproj = Join-Path $scriptDir "src\DeleteApp\DeleteApp.csproj"
$publishDir = Join-Path $scriptDir $OutputDir

Write-Host "[1/3] 检查 .NET SDK..."
$dotnetVersion = dotnet --version 2>&1
Write-Host "       .NET SDK version: $dotnetVersion" -ForegroundColor Green

Write-Host ""
Write-Host "[2/3] 发布项目 (单文件自包含)..."

dotnet publish $csproj `
    -c $Configuration `
    -f $TargetFramework `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:IncludeAllContentForSelfExtract=true `
    --self-contained true `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "       单文件发布失败，尝试普通发布..." -ForegroundColor Yellow
    dotnet publish $csproj -c $Configuration -f $TargetFramework -o $publishDir
}

Write-Host ""
Write-Host "[3/3] 复制说明文档..."

Copy-Item (Join-Path $scriptDir "README.md") (Join-Path $publishDir "README.md") -Force

Write-Host ""
Write-Host "=== 打包完成 ===" -ForegroundColor Green
Write-Host "输出目录: $publishDir" -ForegroundColor Cyan
Write-Host "可执行文件: $publishDir\DeleteApp.exe" -ForegroundColor Cyan
Write-Host ""
Write-Host "双击 $publishDir\DeleteApp.exe 或运行："
Write-Host "  Start-Process '$publishDir\DeleteApp.exe' -Verb RunAs"
