#!/usr/bin/env bash
# ==============================================================================
# MicroSaaS - AWS EC2 User Data Script (Ubuntu 24.04 LTS - Noble Numbat)
# ==============================================================================
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

echo "[1/5] Actualizando paquetes del sistema operativo..."
apt-get update -y
apt-get upgrade -y
apt-get install -y ca-certificates curl gnupg lsb-release git ufw

echo "[2/5] Configurando archivo Swap de 2GB (recomendado para instancias Free Tier t2.micro / t3.micro)..."
if [ ! -f /swapfile ]; then
    fallocate -l 2G /swapfile
    chmod 600 /swapfile
    mkswap /swapfile
    swapon /swapfile
    echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "[3/5] Configurando repositorio oficial de Docker para Ubuntu 24.04..."
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
chmod a+r /etc/apt/keyrings/docker.asc

echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \
  $(. /etc/os-release && echo "${VERSION_CODENAME}") stable" | \
  tee /etc/apt/sources.list.d/docker.list > /dev/null

echo "[4/5] Instalando Docker Engine y Docker Compose Plugin..."
apt-get update -y
apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

systemctl enable docker
systemctl start docker

echo "[5/5] Configurando permisos para el usuario 'ubuntu'..."
usermod -aG docker ubuntu
mkdir -p /opt/microsaas
chown -R ubuntu:ubuntu /opt/microsaas

echo "Inicialización EC2 completada con éxito."

