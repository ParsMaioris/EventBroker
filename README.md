## Overview

A .NET microservices demo for event-driven architecture with RabbitMQ.

## Features

- Gateway API for payment requests
- Payment processing service
- Notification service with event sourcing
- Shipping service
- End-to-end message flow

## Requirements

- .NET 9.0
- RabbitMQ server

## Setup

1. Clone the repository
2. Configure RabbitMQ connection in user secrets:
```json
{
  "RabbitMq": {
    "Host": "your-rabbitmq-host",
    "Username": "your-username",
    "Password": "your-password"
  }
}
```

## Usage

```bash
# Build the solution
dotnet build

# Run services
dotnet run --project CookieStore.Gateway
dotnet run --project CookieStore.Payments
dotnet run --project CookieStore.Notifications
dotnet run --project CookieStore.Shipping

# Run tests
dotnet test
```

## Architecture

The system uses RabbitMQ for message passing:
- Direct queue for payment requests
- Fanout exchange for processed payments
- Event sourcing for notification retries