# Static frontend assets are shared by every target architecture. Build once natively.
FROM --platform=$BUILDPLATFORM node:26-alpine AS frontend-build

WORKDIR /app/frontend

# Copy frontend package files
COPY UI/Fuse.Web/package*.json ./
RUN npm ci

# Copy frontend source
COPY UI/Fuse.Web/ ./

# Build the Vue app
RUN npm run build

# Run the SDK natively and cross-publish the API for the requested runtime.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
ARG TARGETARCH

WORKDIR /app

# Restore only the application and its referenced projects.
COPY API/Fuse.API/*.csproj ./API/Fuse.API/
COPY API/Fuse.Core/*.csproj ./API/Fuse.Core/
COPY API/Fuse.MCP/*.csproj ./API/Fuse.MCP/
COPY API/Fuse.Data/*.csproj ./API/Fuse.Data/

# Restore dependencies
RUN dotnet restore API/Fuse.API/Fuse.API.csproj -a "$TARGETARCH" -p:UseAppHost=false

# Copy the rest of the source code
COPY API/Fuse.API/ ./API/Fuse.API/
COPY API/Fuse.Core/ ./API/Fuse.Core/
COPY API/Fuse.MCP/ ./API/Fuse.MCP/
COPY API/Fuse.Data/ ./API/Fuse.Data/

# Copy the already-built frontend to satisfy the SpaRoot reference
COPY --from=frontend-build /app/frontend/dist /app/UI/Fuse.Web/dist

# Build and publish the API (skip the npm build since we already have dist)
WORKDIR /app/API/Fuse.API
RUN dotnet publish -c Release -a "$TARGETARCH" --self-contained false -p:UseAppHost=false -o /app/publish --no-restore /p:BuildSpa=false

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ARG APP_VERSION
ARG APP_CHANNEL
ARG GIT_COMMIT_ID
ARG GIT_COMMIT_ID_SHORT
ARG BUILD_DATE

# Copy published API
COPY --from=backend-build /app/publish ./

# Copy built frontend to wwwroot
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV APP_VERSION=$APP_VERSION
ENV APP_CHANNEL=$APP_CHANNEL
ENV GIT_COMMIT_ID=$GIT_COMMIT_ID
ENV GIT_COMMIT_ID_SHORT=$GIT_COMMIT_ID_SHORT
ENV BUILD_DATE=$BUILD_DATE

# Run the application
ENTRYPOINT ["dotnet", "Fuse.API.dll"]
