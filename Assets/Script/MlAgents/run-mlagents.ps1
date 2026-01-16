# ================================
# Script ML-Agents - PowerShell
# ================================

$ErrorActionPreference = "Stop"

Write-Host "Iniciando entrenamiento ML-Agents..."

# --- CONFIGURACION ---
$PROJECT_PATH = "C:\Users\leand\OneDrive\Documents\GitHub\Simulacion-Trafico"
$CONDA_ENV = "mlagents"
$CONFIG_FILE = "config/CarConfig.yaml"
$RUN_ID = "Car"
$LOG_FILE = "training.log"

# --- IR AL PROYECTO ---
if (!(Test-Path $PROJECT_PATH)) {
    Write-Host "ERROR: No existe la ruta $PROJECT_PATH"
    exit 1
}

Set-Location $PROJECT_PATH
Write-Host "Directorio actual: $(Get-Location)"

# --- INICIALIZAR CONDA CORRECTAMENTE ---
Write-Host "Inicializando Conda..."

$condaHook = (conda shell.powershell hook) -join "`n"
Invoke-Expression $condaHook

# --- ACTIVAR ENTORNO ---
Write-Host "Activando entorno Conda: $CONDA_ENV"
conda activate $CONDA_ENV

# --- VALIDAR ML-AGENTS ---
Write-Host "Verificando ML-Agents..."
if (-not (Get-Command mlagents-learn -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: mlagents-learn no esta disponible en este entorno"
    exit 1
}

# --- EJECUTAR ENTRENAMIENTO ---
Write-Host "Iniciando entrenamiento..."
Write-Host "Log: $LOG_FILE"

mlagents-learn $CONFIG_FILE `
    --run-id=$RUN_ID `
    --force `
    --no-graphics `
    | Tee-Object -FilePath $LOG_FILE

Write-Host "Entrenamiento finalizado correctamente"
Write-Host "Resultados en: results\$RUN_ID"
Write-Host "Log guardado en: $LOG_FILE"
