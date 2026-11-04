# ──────────────────────────────────────────────────────────
# Multi-stage Dockerfile — Ticketing API
# Stage 1: restore & build (SDK)
# Stage 2: runtime (ASP.NET Runtime)
# ──────────────────────────────────────────────────────────

# ─── Stage 1: Build ───
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution và props trước để cache layer restore
COPY backend/Ticketing.slnx ./
COPY backend/Directory.Build.props ./
COPY backend/Directory.Packages.props ./

# Copy tất cả .csproj files để restore (giữ cấu trúc thư mục)
COPY backend/src/Api/Ticketing.Api/Ticketing.Api.csproj src/Api/Ticketing.Api/
COPY backend/src/BuildingBlocks/Ticketing.SharedKernel/Ticketing.SharedKernel.csproj src/BuildingBlocks/Ticketing.SharedKernel/
COPY backend/src/BuildingBlocks/Ticketing.Infrastructure.Core/Ticketing.Infrastructure.Core.csproj src/BuildingBlocks/Ticketing.Infrastructure.Core/
COPY backend/src/Modules/Identity/Identity.Contracts/Identity.Contracts.csproj src/Modules/Identity/Identity.Contracts/
COPY backend/src/Modules/Identity/Identity.Internal/Identity.Internal.csproj src/Modules/Identity/Identity.Internal/
COPY backend/src/Modules/Events/Events.Contracts/Events.Contracts.csproj src/Modules/Events/Events.Contracts/
COPY backend/src/Modules/Events/Events.Internal/Events.Internal.csproj src/Modules/Events/Events.Internal/
COPY backend/src/Modules/Ticketing/Ticketing.Contracts/Ticketing.Contracts.csproj src/Modules/Ticketing/Ticketing.Contracts/
COPY backend/src/Modules/Ticketing/Ticketing.Internal/Ticketing.Internal.csproj src/Modules/Ticketing/Ticketing.Internal/
COPY backend/src/Modules/Orders/Orders.Contracts/Orders.Contracts.csproj src/Modules/Orders/Orders.Contracts/
COPY backend/src/Modules/Orders/Orders.Internal/Orders.Internal.csproj src/Modules/Orders/Orders.Internal/
COPY backend/src/Modules/CheckIn/CheckIn.Contracts/CheckIn.Contracts.csproj src/Modules/CheckIn/CheckIn.Contracts/
COPY backend/src/Modules/CheckIn/CheckIn.Internal/CheckIn.Internal.csproj src/Modules/CheckIn/CheckIn.Internal/
COPY backend/src/Modules/Notifications/Notifications.Contracts/Notifications.Contracts.csproj src/Modules/Notifications/Notifications.Contracts/
COPY backend/src/Modules/Notifications/Notifications.Internal/Notifications.Internal.csproj src/Modules/Notifications/Notifications.Internal/
COPY backend/tests/Architecture.Tests/Architecture.Tests.csproj tests/Architecture.Tests/
COPY backend/tests/Integration.Tests/Integration.Tests.csproj tests/Integration.Tests/

RUN dotnet restore Ticketing.slnx

# Copy toàn bộ source code
COPY backend/src/ src/
COPY backend/tests/ tests/

# Build & publish
RUN dotnet publish src/Api/Ticketing.Api/Ticketing.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ─── Stage 2: Runtime ───
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Non-root user (built-in in .NET 8/9/10 images)
USER app

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Ticketing.Api.dll"]
