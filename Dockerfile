# =====================================================================
# STAGE 1: RUNTIME BASE (Debian 12 Bookworm Slim + libgomp1 cho ONNX)
# =====================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 8080

# Cài đặt libgomp1 (bắt buộc cho ONNX Runtime OpenMP) và tzdata (múi giờ VN)
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgomp1 \
    tzdata \
    curl \
    && rm -rf /var/lib/apt/lists/*

ENV TZ=Asia/Ho_Chi_Minh
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Đảm bảo thư mục chứa model AI tồn tại
RUN mkdir -p /app/wwwroot/AIModels

# =====================================================================
# STAGE 2: BUILD & PUBLISH (.NET 8 SDK)
# =====================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy trước các file .csproj của 4 tầng để tận dụng Docker Layer Caching khi restore
COPY ["src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj", "src/QuanLyChoThuePhongTroWeb.Web/"]
COPY ["src/QuanLyChoThuePhongTroWeb.Infrastructure/QuanLyChoThuePhongTroWeb.Infrastructure.csproj", "src/QuanLyChoThuePhongTroWeb.Infrastructure/"]
COPY ["src/QuanLyChoThuePhongTroWeb.Application/QuanLyChoThuePhongTroWeb.Application.csproj", "src/QuanLyChoThuePhongTroWeb.Application/"]
COPY ["src/QuanLyChoThuePhongTroWeb.Domain/QuanLyChoThuePhongTroWeb.Domain.csproj", "src/QuanLyChoThuePhongTroWeb.Domain/"]

# Restore với cờ UseCpuOnnx=true để nạp gói Microsoft.ML.OnnxRuntime cho Linux
RUN dotnet restore "src/QuanLyChoThuePhongTroWeb.Web/QuanLyChoThuePhongTroWeb.Web.csproj" /p:UseCpuOnnx=true

# Copy toàn bộ source code còn lại và publish
COPY . .
WORKDIR "/src/src/QuanLyChoThuePhongTroWeb.Web"
RUN dotnet publish "QuanLyChoThuePhongTroWeb.Web.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false \
    /p:UseCpuOnnx=true

# =====================================================================
# STAGE 3: FINAL IMAGE
# =====================================================================
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "QuanLyChoThuePhongTroWeb.Web.dll"]
