# =========================
# 前端构建阶段
# =========================
FROM node:22-bookworm-slim AS frontend-build

WORKDIR /src
COPY edge-gateway-ui/package*.json ./edge-gateway-ui/
RUN npm ci --prefix ./edge-gateway-ui

COPY edge-gateway-ui/ ./edge-gateway-ui/
RUN npm run build --prefix ./edge-gateway-ui

# =========================
# .NET 发布阶段
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build

WORKDIR /src
COPY EdgeGateway.sln ./
COPY EdgeGateway.Application/EdgeGateway.Application.csproj ./EdgeGateway.Application/
COPY EdgeGateway.Domain/EdgeGateway.Domain.csproj ./EdgeGateway.Domain/
COPY EdgeGateway.Host/EdgeGateway.Host.csproj ./EdgeGateway.Host/
COPY EdgeGateway.Infrastructure/EdgeGateway.Infrastructure.csproj ./EdgeGateway.Infrastructure/
COPY EdgeGateway.WebApi/EdgeGateway.WebApi.csproj ./EdgeGateway.WebApi/
COPY EdgeGateway.Tests/EdgeGateway.Tests.csproj ./EdgeGateway.Tests/
RUN dotnet restore EdgeGateway.WebApi/EdgeGateway.WebApi.csproj

COPY . ./
COPY --from=frontend-build /src/EdgeGateway.WebApi/wwwroot ./EdgeGateway.WebApi/wwwroot
RUN dotnet publish EdgeGateway.WebApi/EdgeGateway.WebApi.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# =========================
# 运行阶段
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

WORKDIR /app
ENV ASPNETCORE_URLS=http://+:5000
ENV Database__ConnectionString="Data Source=/data/gateway.db"

# curl 用于容器健康检查。
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data /app/output

COPY --from=backend-build /app/publish ./

EXPOSE 5000

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl --fail --silent http://localhost:5000/api > /dev/null || exit 1

ENTRYPOINT ["dotnet", "EdgeGateway.WebApi.dll"]
