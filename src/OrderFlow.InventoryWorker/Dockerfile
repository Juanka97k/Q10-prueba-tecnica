# ==========================================
# Etapa 1: Base Runtime Alpine (Producción Ultraligera)
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS base
WORKDIR /app

# ==========================================
# Etapa 2: Build (SDK Alpine de Compilación)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copiar archivos .csproj para restaurar dependencias NuGet (Capa de Caché)
COPY ["src/OrderFlow.InventoryWorker/OrderFlow.InventoryWorker.csproj", "src/OrderFlow.InventoryWorker/"]
COPY ["src/OrderFlow.Infrastructure/OrderFlow.Infrastructure.csproj", "src/OrderFlow.Infrastructure/"]
COPY ["src/OrderFlow.Shared/OrderFlow.Shared.csproj", "src/OrderFlow.Shared/"]

RUN dotnet restore "src/OrderFlow.InventoryWorker/OrderFlow.InventoryWorker.csproj"

# Copiar todo el código fuente y compilar el Worker
COPY . .
WORKDIR "/src/src/OrderFlow.InventoryWorker"
RUN dotnet build "OrderFlow.InventoryWorker.csproj" -c Release -o /app/build

# ==========================================
# Etapa 3: Publish (Generar Binarios de Producción)
# ==========================================
FROM build AS publish
RUN dotnet publish "OrderFlow.InventoryWorker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# Etapa 4: Runner (Imagen Ejecutable Final Alpine)
# ==========================================
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrderFlow.InventoryWorker.dll"]
