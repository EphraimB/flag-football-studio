param(
    [ValidateSet("Cuda", "Cpu")]
    [string]$Runtime = "Cuda"
)

$ErrorActionPreference = "Stop"
$ToolDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$VirtualEnvironment = Join-Path $ToolDirectory ".venv"
py -3 -m venv $VirtualEnvironment
$Python = Join-Path $VirtualEnvironment "Scripts\python.exe"
& $Python -m pip install --upgrade pip
if ($Runtime -eq "Cuda") {
    & $Python -m pip install -r (Join-Path $ToolDirectory "requirements-cuda.txt")
    & $Python -m pip uninstall -y onnxruntime
    & $Python -m pip install "onnxruntime-gpu>=1.23,<2"
} else {
    & $Python -m pip install -r (Join-Path $ToolDirectory "requirements-cpu.txt")
}
Write-Host "Piper runtime installed. Download a voice explicitly with:"
Write-Host "  & '$Python' -m piper.download_voices VOICE_NAME --data-dir '$ToolDirectory\models'"
