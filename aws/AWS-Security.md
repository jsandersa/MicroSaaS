# Configuración de Seguridad en AWS EC2 (Security Group)

Este documento define las reglas exactas del **Security Group** en la consola de AWS para proteger la instancia **EC2 Standalone (Ubuntu 24.04 LTS)** que ejecuta la arquitectura Docker Compose de `MicroSaaS`.

---

## 1. Reglas de Entrada (Inbound Rules)

Configura únicamente los siguientes puertos en la pestaña **Inbound rules** de tu Security Group:

| Tipo | Protocolo | Rango de Puertos | Origen (Source) | Descripción / Propósito |
| :--- | :--- | :--- | :--- | :--- |
| **SSH** | `TCP` | `22` | `Mi IP` (`x.x.x.x/32`) | Administración remota por consola y despliegue vía SSH/SCP. **Evitar `0.0.0.0/0` en producción.** |
| **HTTP** | `TCP` | `80` | `0.0.0.0/0`, `::/0` | Acceso público al portal Angular servido por NGINX y proxy inverso `/api/*` hacia `.NET 10`. |
| **HTTPS** | `TCP` | `443` | `0.0.0.0/0`, `::/0` | Tráfico web cifrado TLS/SSL (para certificados Let's Encrypt / Certbot o Cloudflare). |

> [!IMPORTANT]
> **Aislamiento de Base de Datos y Backend**:
> - **NO abras el puerto `5432` (PostgreSQL)** ni el **puerto `8080` (API .NET)** en el Security Group de AWS.
> - El contenedor de **NGINX** escucha en el puerto `80`/`443` y se comunica internamente con `api:8080` y éste con `postgres:5432` a través de la red privada `microsaas-net` de Docker.

---

## 2. Reglas de Salida (Outbound Rules)

Mantén la regla predeterminada de salida para permitir que la instancia descargue actualizaciones de Ubuntu (`apt`), imágenes de Docker Hub / Microsoft Container Registry (`mcr.microsoft.com`) y paquetes npm/NuGet durante la construcción:

| Tipo | Protocolo | Rango de Puertos | Destino (Destination) | Descripción / Propósito |
| :--- | :--- | :--- | :--- | :--- |
| **All traffic** | `All` | `All` (`0 - 65535`) | `0.0.0.0/0`, `::/0` | Permite descarga de paquetes del SO, imágenes de contenedores y llamadas salientes. |

---

## 3. Pasos Rápidos de Despliegue en la Instancia EC2

1. Al crear la instancia EC2 (**Ubuntu Server 24.04 LTS**, `t2.micro` o `t3.micro`), pega el contenido de [`ec2-userdata.sh`](./ec2-userdata.sh) en la sección **Advanced details -> User data**.
2. Conéctate por SSH una vez iniciada la instancia:
   ```bash
   ssh -i tu-llave.pem ubuntu@<IP_PUBLICA_EC2>
   ```
3. Clona o copia el repositorio en `/opt/microsaas` y ejecuta el script de despliegue:
   ```bash
   cd /opt/microsaas
   chmod +x aws/deploy.sh
   ./aws/deploy.sh
   ```

