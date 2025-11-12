# BASMS-BE (Biometric Attendance And Shift Management System - Backend)

.NET 8.0 Microservice backend for Biometric Attendance And Shift Management System for Security Services.

## Prerequisites

Before you begin, ensure you have the following installed:
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://www.docker.com/get-started)
- [Docker Compose](https://docs.docker.com/compose/install/)

## Project Structure

```
BASMS-BE/
├── Service1/
│   ├── appsettings.json
│   └── ...
├── Service2/
│   ├── appsettings.json
│   └── ...
├── Service3/
│   ├── appsettings.json
│   └── ...
├── docker-compose.yml
└── README.md
```

## Getting Started

### Step 1: Configure AppSettings for Each Service

You need to add `appsettings.json` files to each of the three services. Create or update the configuration files with your specific settings.

#### Service 1 - appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string-here"
  },
  "JwtSettings": {
    "SecretKey": "your-secret-key",
    "Issuer": "your-issuer",
    "Audience": "your-audience"
  }
}
```

#### Service 2 - appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string-here"
  }
}
```

#### Service 3 - appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "your-connection-string-here"
  }
}
```

**Note:** Make sure to replace placeholder values with your actual configuration values. Never commit sensitive information like connection strings or secret keys to version control.

### Step 2: Build the Project

#### Build with .NET CLI
```bash
# Build all projects in the solution
dotnet build

# Build specific service
dotnet build Service1/Service1.csproj
```

#### Build with Docker
```bash
# Build all services using Docker Compose
docker-compose build

# Build specific service
docker-compose build service1
```

### Step 3: Run with Docker Compose

#### Start all services
```bash
# Start services in detached mode
docker-compose up -d

# Start services with logs visible
docker-compose up
```

#### Stop all services
```bash
# Stop services
docker-compose down

# Stop and remove volumes
docker-compose down -v
```

#### View logs
```bash
# View all service logs
docker-compose logs

# View specific service logs
docker-compose logs service1

# Follow logs in real-time
docker-compose logs -f
```

#### Restart services
```bash
# Restart all services
docker-compose restart

# Restart specific service
docker-compose restart service1
```

## Development

### Run Locally (Without Docker)

```bash
# Run Service 1
cd Service1
dotnet run

# Run Service 2
cd Service2
dotnet run

# Run Service 3
cd Service3
dotnet run
```

### Database Migrations (if applicable)

```bash
# Add migration
dotnet ef migrations add MigrationName --project Service1

# Update database
dotnet ef database update --project Service1
```

## Environment Variables

You can override `appsettings.json` values using environment variables in your `docker-compose.yml`:

```yaml
environment:
  - ASPNETCORE_ENVIRONMENT=Development
  - ConnectionStrings__DefaultConnection=your-connection-string
```

## Testing

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

## Troubleshooting

### Common Issues

1. **Port conflicts**: Make sure the ports defined in `docker-compose.yml` are not already in use
2. **Database connection**: Verify your connection strings in `appsettings.json`
3. **Docker issues**: Try `docker-compose down -v` and rebuild with `docker-compose build --no-cache`

### Check Service Health

```bash
# Check running containers
docker-compose ps

# Check specific container logs
docker logs <container-name>
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## Support

For support, please open an issue in the GitHub repository.
```
