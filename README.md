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
4. **First-time admin user (no hard-coded password):** if the database has no user with role `Admin`, the API seeds one only when `BootstrapAdmin:Password` is set (same strength rules as registration: 8+ chars, upper, lower, digit, special). In Development, from `src/StockInvestment.Api`:

   ```bash
   dotnet user-secrets set "BootstrapAdmin:Email" "admin@gmail.com"
   dotnet user-secrets set "BootstrapAdmin:Password" "YourStrongP@ssw0rd"
   ```

   Production: set environment variables `BootstrapAdmin__Email` and `BootstrapAdmin__Password`. If password is omitted and no admin exists, startup logs a warning and skips seeding.

5. Run database migrations:
   ```bash
   # Apply all migrations (including AnalysisReports table)
   dotnet ef database update --project src/StockInvestment.Infrastructure --startup-project src/StockInvestment.Api
   
   # Or from Infrastructure directory:
   cd src/StockInvestment.Infrastructure
   dotnet ef database update --startup-project ../StockInvestment.Api
   ```
   
   **Note:** Ensure PostgreSQL is running and connection string in `appsettings.json` is correct before running migrations.
6. Run the application:
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
    "BaseUrl": "http://localhost:8000"
  },
  "StockAnalyst": {
    "Enabled": false,
    "BaseUrl": ""
  }
}
```

Run **one** Python service from the repo root folder `ai` (`uvicorn main:app --port 8000`). `AIService:BaseUrl` should target that URL. For LangGraph-backed single-symbol forecast on the dashboard, set `StockAnalyst:Enabled` to `true`; leave `StockAnalyst:BaseUrl` empty to reuse `AIService:BaseUrl`.

**Environment Variables:**

For RAG ingestion, set the internal API key as an environment variable:

```bash
# Windows PowerShell
$env:AI_SERVICE_INTERNAL_API_KEY="your-secret-key-for-rag-ingest"

# Linux/Mac
export AI_SERVICE_INTERNAL_API_KEY="your-secret-key-for-rag-ingest"
```

This key must match the `INTERNAL_API_KEY` configured in the AI service.

