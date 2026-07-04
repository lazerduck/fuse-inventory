# Stage 1: Build Vue Frontend
FROM node:20-alpine AS frontend-build

WORKDIR /app/frontend

# Copy frontend package files
COPY UI/Fuse.Web/package*.json ./
RUN npm ci

# Copy frontend source
COPY UI/Fuse.Web/ ./

# Build the Vue app
RUN npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build

WORKDIR /app

# Copy solution and project files
COPY fuse-inventory.sln ./
COPY API/Fuse.API/*.csproj ./API/Fuse.API/
COPY API/Fuse.Core/*.csproj ./API/Fuse.Core/
COPY API/Fuse.Data/*.csproj ./API/Fuse.Data/
COPY API/Fuse.Tests/*.csproj ./API/Fuse.Tests/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY API/ ./API/

# Copy the already-built frontend to satisfy the SpaRoot reference
COPY --from=frontend-build /app/frontend/dist /app/UI/Fuse.Web/dist

# Build and publish the API (skip the npm build since we already have dist)
WORKDIR /app/API/Fuse.API
RUN dotnet publish -c Release -o /app/publish --no-restore /p:BuildSpa=false

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
