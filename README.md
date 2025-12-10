# Stock Investment Backend API

Backend API service for Multi-Agent AI Stock Investment System built with .NET 8.0 using Clean Architecture.

## Architecture

This project follows Clean Architecture principles with the following layers:

- **StockInvestment.Api** - Presentation Layer (Controllers, Hubs, Middleware)
- **StockInvestment.Application** - Application Layer (Use Cases, DTOs, Interfaces)
- **StockInvestment.Domain** - Domain Layer (Entities, Value Objects, Enums)
- **StockInvestment.Infrastructure** - Infrastructure Layer (Data Access, External Services)
- **StockInvestment.Shared** - Shared utilities and common types

## Prerequisites

- .NET 8.0 SDK
- PostgreSQL 15+
- Redis 7+
- RabbitMQ (optional, for message queue)

## Setup

1. Clone the repository
2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```
3. Update `appsettings.json` with your database connection strings
4. Run database migrations:
   ```bash
   dotnet ef migrations add InitialCreate --project src/StockInvestment.Infrastructure --startup-project src/StockInvestment.Api
   dotnet ef database update --project src/StockInvestment.Infrastructure --startup-project src/StockInvestment.Api
   ```
5. Run the application:
   ```bash
   dotnet run --project src/StockInvestment.Api
   ```

## API Endpoints

- `/api/trading-board` - Get stock tickers with filters
- `/hubs/stock-price` - SignalR hub for real-time price updates

## Configuration

Update `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=stock_investment;Username=postgres;Password=postgres",
    "Redis": "localhost:6379"
  },
  "AIService": {
    "Url": "http://localhost:8000"
  }
}
```

