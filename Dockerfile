# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["src/StockInvestment.Api/StockInvestment.Api.csproj", "src/StockInvestment.Api/"]
COPY ["src/StockInvestment.Application/StockInvestment.Application.csproj", "src/StockInvestment.Application/"]
COPY ["src/StockInvestment.Domain/StockInvestment.Domain.csproj", "src/StockInvestment.Domain/"]
COPY ["src/StockInvestment.Infrastructure/StockInvestment.Infrastructure.csproj", "src/StockInvestment.Infrastructure/"]
COPY ["src/StockInvestment.Shared/StockInvestment.Shared.csproj", "src/StockInvestment.Shared/"]

RUN dotnet restore "src/StockInvestment.Api/StockInvestment.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/src/StockInvestment.Api"
RUN dotnet build "StockInvestment.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "StockInvestment.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Copy published app
COPY --from=publish /app/publish .

# Create logs directory
RUN mkdir -p /app/logs && chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose port
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD curl -f http://localhost:8080/health/live || exit 1

# Entry point
ENTRYPOINT ["dotnet", "StockInvestment.Api.dll"]
