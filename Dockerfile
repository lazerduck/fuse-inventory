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
COPY fuse-invetory.sln ./
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

# Copy published API
COPY --from=backend-build /app/publish ./

# Copy built frontend to wwwroot
COPY --from=frontend-build /app/frontend/dist ./wwwroot

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Run the application
ENTRYPOINT ["dotnet", "Fuse.API.dll"]
