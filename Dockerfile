# =====================================================================
# Stage 1: Build & Publish (.NET 10 SDK)
# =====================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar solución y archivos de proyecto para aprovechar la caché de capas en restore
COPY ["MicroSaaS.sln", "./"]
COPY ["src/MicroSaaS.Core/MicroSaaS.Core.csproj", "src/MicroSaaS.Core/"]
COPY ["src/MicroSaaS.Infrastructure/MicroSaaS.Infrastructure.csproj", "src/MicroSaaS.Infrastructure/"]
COPY ["src/MicroSaaS.Application/MicroSaaS.Application.csproj", "src/MicroSaaS.Application/"]
COPY ["src/MicroSaaS.API/MicroSaaS.API.csproj", "src/MicroSaaS.API/"]

RUN dotnet restore "src/MicroSaaS.API/MicroSaaS.API.csproj"

# Copiar el resto del código fuente del backend y publicar en modo Release
COPY ["src/MicroSaaS.Core/", "src/MicroSaaS.Core/"]
COPY ["src/MicroSaaS.Infrastructure/", "src/MicroSaaS.Infrastructure/"]
COPY ["src/MicroSaaS.Application/", "src/MicroSaaS.Application/"]
COPY ["src/MicroSaaS.API/", "src/MicroSaaS.API/"]

WORKDIR "/src/src/MicroSaaS.API"
RUN dotnet publish "MicroSaaS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =====================================================================
# Stage 2: Runtime (.NET 10 ASP.NET Core)
# =====================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "MicroSaaS.API.dll"]

