[CmdletBinding(PositionalBinding=$false)]
param(
  [string] $PythonLauncher = "py"
)

$ErrorActionPreference = "Stop"

# Resolve repo root and venv paths
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$VenvDir = Join-Path $RepoRoot ".venv"
$KernelsDir = Join-Path $VenvDir "share\jupyter\kernels"
$Requirements = Join-Path $RepoRoot "requirements-jupyter.txt"

Write-Host "Repo: $RepoRoot"
Write-Host "Venv: $VenvDir"

# Select Python launcher
try {
  Get-Command $PythonLauncher -ErrorAction Stop | Out-Null
} catch {
  $PythonLauncher = "python"
}

# Create venv if missing
if (!(Test-Path $VenvDir)) {
  Write-Host "Creating Python venv at $VenvDir ..."
  & $PythonLauncher -3 -m venv $VenvDir
}

$PythonExe = Join-Path $VenvDir "Scripts\python.exe"

# Upgrade pip and install Jupyter packages
Write-Host "Upgrading pip..."
& $PythonExe -m pip install -U pip

if (Test-Path $Requirements) {
  Write-Host "Installing Python packages from $Requirements ..."
  & $PythonExe -m pip install -r $Requirements
} else {
  Write-Host "requirements-jupyter.txt not found, installing jupyterlab and notebook directly ..."
  & $PythonExe -m pip install jupyterlab notebook
}

# Install or update .NET Interactive global tool
Write-Host "Installing/Updating Microsoft.dotnet-interactive tool ..."
$toolList = & dotnet tool list -g | Out-String
if ($toolList -match "Microsoft\.dotnet-interactive") {
  & dotnet tool update -g Microsoft.dotnet-interactive
} else {
  & dotnet tool install -g Microsoft.dotnet-interactive
}

# Ensure kernels dir exists under venv and install kernelspecs there
Write-Host "Registering .NET Interactive kernels into venv Jupyter path ..."
New-Item -ItemType Directory -Force -Path $KernelsDir | Out-Null
& dotnet interactive jupyter install --path "$KernelsDir"

Write-Host "\nSetup complete. Next steps:"
Write-Host "  1) Open VS Code and run task: 'Setup: Jupyter + .NET Interactive' (done if you ran this)"
Write-Host "  2) Run task: 'Open: Jupyter Lab' to launch Jupyter in the notebooks/ folder"
Write-Host "  3) In Jupyter, select kernel '.NET (C#)' for C# notebooks"
