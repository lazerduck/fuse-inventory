# Docker Deployment Guide

This guide explains how to build and run the Fuse Inventory application using Docker.

## Building the Docker Image

### Using Docker

Build the image:
```bash
docker build -t fuse-inventory:latest .
```

Run the container:
```bash
docker run -d -p 8080:8080 --name fuse-inventory fuse-inventory:latest
```

### Using Docker Compose

Build and start:
```bash
docker-compose up -d
```

Stop:
```bash
docker-compose down
```

Rebuild after changes:
```bash
docker-compose up -d --build
```

## Accessing the Application

Once running, access the application at:
- **Application**: http://localhost:8080

## Container Details

### Multi-Stage Build Process

1. **Frontend Build Stage**: Builds the Vue.js application
   - Uses Node.js 20 Alpine
   - Runs `npm ci` and `npm run build`
   - Outputs to `dist/` directory

2. **Backend Build Stage**: Builds the .NET API
   - Uses .NET 8 SDK
   - Restores NuGet packages
   - Publishes in Release mode

3. **Runtime Stage**: Final lightweight image
   - Uses .NET 8 ASP.NET runtime (smaller than SDK)
   - Copies published API and built frontend
   - Exposes port 8080

### Environment Variables

You can customize the application using environment variables:

```bash
docker run -d \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_URLS=http://+:8080 \
  --name fuse-inventory \
  fuse-inventory:latest
```

### Data Persistence

To persist data, mount a volume for your data storage:

```bash
docker run -d \
  -p 8080:8080 \
  -v $(pwd)/data:/app/data \
  --name fuse-inventory \
  fuse-inventory:latest
```

Or update the `docker-compose.yml` file volumes section.

## Troubleshooting

### View Logs
```bash
docker logs fuse-inventory
```

### View Logs (Follow)
```bash
docker logs -f fuse-inventory
```

### Execute Shell in Container
```bash
docker exec -it fuse-inventory /bin/sh
```

### Check Container Status
```bash
docker ps -a | grep fuse-inventory
```

## Production Considerations

1. **Reverse Proxy**: Consider using nginx or Traefik in front of the container
2. **HTTPS**: Configure SSL certificates (Let's Encrypt recommended)
3. **Health Checks**: Add health check endpoints and configure Docker health checks
4. **Resource Limits**: Set memory and CPU limits in production
5. **Monitoring**: Implement logging and monitoring solutions

### Example with Resource Limits

```yaml
services:
  fuse-app:
    # ... other config
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 512M
        reservations:
          cpus: '0.5'
          memory: 256M
```
