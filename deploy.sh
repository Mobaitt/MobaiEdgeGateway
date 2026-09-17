#!/usr/bin/env bash

set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"

IMAGE_NAME="mobai-edge-gateway:latest"
CONTAINER_NAME="mobai-edge-gateway"
DATA_VOLUME="mobai-edge-gateway-data"
OUTPUT_VOLUME="mobai-edge-gateway-output"
PORT="${EDGE_GATEWAY_PORT:-5000}"
DEMO_MODE_ENABLED="${DEMO_MODE_ENABLED:-true}"

if ! command -v git >/dev/null 2>&1; then
  echo "错误：未找到 Git，请先安装 Git。" >&2
  exit 1
fi

echo "拉取最新代码：origin/master"
if ! git pull --ff-only origin master; then
  echo "错误：拉取代码失败，已停止部署。请检查 Git 凭据、本地改动和网络连接。" >&2
  exit 1
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "错误：未找到 Docker，请先安装并启动 Docker。" >&2
  exit 1
fi

echo "检查 Docker 环境..."
docker version --format '{{.Server.Version}}'

if [[ -n "$(docker ps -q --filter "name=^/${CONTAINER_NAME}$")" ]]; then
  echo "停止服务：${CONTAINER_NAME}"
  docker stop "${CONTAINER_NAME}"
fi

if [[ -n "$(docker ps -aq --filter "name=^/${CONTAINER_NAME}$")" ]]; then
  echo "移除旧容器：${CONTAINER_NAME}"
  docker rm "${CONTAINER_NAME}"
fi

if docker image inspect "${IMAGE_NAME}" >/dev/null 2>&1; then
  echo "删除旧镜像：${IMAGE_NAME}"
  docker image rm -f "${IMAGE_NAME}"
fi

echo "创建或复用数据卷：${DATA_VOLUME}、${OUTPUT_VOLUME}"
docker volume create "${DATA_VOLUME}" >/dev/null
docker volume create "${OUTPUT_VOLUME}" >/dev/null

BUILD_ARGS=(build --tag "${IMAGE_NAME}")
if [[ "${NO_CACHE:-false}" == "true" || "${1:-}" == "--no-cache" ]]; then
  BUILD_ARGS+=(--no-cache)
fi
BUILD_ARGS+=(.)

echo "构建镜像：${IMAGE_NAME}"
docker "${BUILD_ARGS[@]}"

echo "启动服务：${CONTAINER_NAME}，端口映射 ${PORT}:5000"
docker run -d \
  --name "${CONTAINER_NAME}" \
  --restart unless-stopped \
  --publish "${PORT}:5000" \
  --env "ASPNETCORE_ENVIRONMENT=Production" \
  --env "ASPNETCORE_URLS=http://+:5000" \
  --env "Database__ConnectionString=Data Source=/data/gateway.db" \
  --env "DemoMode__Enabled=${DEMO_MODE_ENABLED}" \
  --volume "${DATA_VOLUME}:/data" \
  --volume "${OUTPUT_VOLUME}:/app/output" \
  "${IMAGE_NAME}"

echo "部署完成：http://localhost:${PORT}"
