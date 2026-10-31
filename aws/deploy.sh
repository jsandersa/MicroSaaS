#!/usr/bin/env bash
# ==============================================================================
# MicroSaaS - Script de Despliegue Automatizado en AWS EC2
# ==============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
ENV_FILE="${PROJECT_ROOT}/.env"

cd "${PROJECT_ROOT}"

echo "========================================================"
echo " Iniciando despliegue de MicroSaaS en AWS EC2"
echo " Directorio raíz: ${PROJECT_ROOT}"
echo "========================================================"

# 1. Generar o validar el archivo .env de producción
if [ ! -f "${ENV_FILE}" ]; then
    echo "[1/4] Generando archivo .env de producción con secretos criptográficos..."
    GENERATED_DB_PASS="$(openssl rand -base64 24 | tr -dc 'A-Za-z0-9' | head -c 24)"
    GENERATED_JWT_SECRET="$(openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c 48)"

    cat <<EOF > "${ENV_FILE}"
# Configuración de Base de Datos PostgreSQL (Producción)
POSTGRES_USER=postgres
POSTGRES_PASSWORD=${GENERATED_DB_PASS}
POSTGRES_DB=MicroSaaS_DB

# Configuración de Seguridad JWT (Producción)
JWT_SECRET=${GENERATED_JWT_SECRET}
JWT_ISSUER=MicroSaaS_Auth
JWT_AUDIENCE=MicroSaaS_Tenants
JWT_EXPIRY_MINUTES=1440
EOF
    chmod 600 "${ENV_FILE}"
    echo "Archivo .env creado y protegido con permisos 600."
else
    echo "[1/4] Archivo .env existente detectado. Conservando credenciales actuales."
fi

# 2. Detener contenedores previos
echo "[2/4] Deteniendo contenedores previos..."
docker compose down --remove-orphans || true

# 3. Limpiar imágenes huérfanas y caché de compilación obsoleta
echo "[3/4] Limpiando imágenes huérfanas..."
docker image prune -f

# 4. Construir y levantar servicios en segundo plano
echo "[4/4] Construyendo imágenes y levantando el clúster Docker Compose..."
docker compose --env-file "${ENV_FILE}" up --build -d

echo "========================================================"
echo " Estado actual de los contenedores:"
echo "========================================================"
docker compose ps

echo "Despliegue completado exitosamente."

