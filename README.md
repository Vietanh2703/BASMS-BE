# BASMS-BE

.NET 8.0 Microservice backend for Biometric Attendance And Shift Management System for Security Services.

## Overview

BASMS-BE is a scalable, microservices-based backend system designed for managing security guard attendance, shifts, contracts, and incidents using biometric authentication.

### Key Features

- **Microservices Architecture**: 6 independent services (Users, Contracts, Shifts, Attendances, Incidents, Chats)
- **Biometric Authentication**: Face recognition for attendance tracking
- **Real-time Communication**: WebSocket-based chat and notifications
- **Event-Driven**: RabbitMQ message broker for inter-service communication
- **Cloud Integration**: AWS S3 for file storage, Firebase for push notifications
- **Comprehensive API**: RESTful APIs built with Carter and MediatR

### Tech Stack

- **Framework**: .NET 8.0 with Carter (Minimal APIs)
- **Database**: MySQL 8.0
- **Message Broker**: RabbitMQ 3.13
- **Authentication**: JWT Bearer Tokens
- **Architecture Patterns**: CQRS (MediatR), Repository Pattern (Dapper)
- **Containerization**: Docker & Docker Compose

## Quick Start

Get up and running in minutes with Docker:

```bash
git clone <repository-url>
cd BASMS-BE/basms-be
docker-compose -f compose.yaml up -d
```

Access the APIs at:
- Users: http://localhost:5001
- Contracts: http://localhost:5002
- Shifts: http://localhost:5003
- Attendances: http://localhost:5004
- Incidents: http://localhost:5005
- Chats: http://localhost:5006

For detailed installation instructions, prerequisites, manual setup, and troubleshooting, see **[INSTALL.md](./INSTALL.md)**.

## Documentation

- **[Installation Guide](./INSTALL.md)** - Complete setup instructions for local development
- **API Documentation** - Swagger/OpenAPI docs available at each service's `/swagger` endpoint (if configured)

## Project Structure

```
basms-be/
├── Users.API/              # User management and authentication
├── Contracts.API/          # Contract and customer management
├── Shifts.API/             # Shift scheduling and team management
├── Attendances.API/        # Biometric attendance tracking
├── Incidents.API/          # Incident reporting and management
├── Chats.API/              # Real-time chat functionality
├── mysql-init/             # Database initialization scripts
├── compose.yaml            # Development Docker Compose config
└── docker-compose.yml      # Production Docker Compose config
```

## Development

### Prerequisites

- Docker Desktop (recommended) or .NET 8.0 SDK + MySQL + RabbitMQ
- Git

### Running Services Individually

```bash
cd basms-be
dotnet build
cd Users.API
dotnet run
```

Repeat for other services as needed.

## Contributing

Contributions are welcome! Please ensure:
- Code follows existing patterns and conventions
- All tests pass before submitting PR
- Commit messages are clear and descriptive
