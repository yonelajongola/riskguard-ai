FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY RiskGuardAI.sln ./
COPY src/RiskGuard.Domain/RiskGuard.Domain.csproj src/RiskGuard.Domain/
COPY src/RiskGuard.Application/RiskGuard.Application.csproj src/RiskGuard.Application/
COPY src/RiskGuard.Persistence/RiskGuard.Persistence.csproj src/RiskGuard.Persistence/
COPY src/RiskGuard.Infrastructure/RiskGuard.Infrastructure.csproj src/RiskGuard.Infrastructure/
COPY src/RiskGuard.API/RiskGuard.API.csproj src/RiskGuard.API/
COPY src/RiskGuard.Persistence.SqlServerMigrations/RiskGuard.Persistence.SqlServerMigrations.csproj src/RiskGuard.Persistence.SqlServerMigrations/
RUN dotnet restore src/RiskGuard.API/RiskGuard.API.csproj
COPY src/ src/
RUN dotnet publish src/RiskGuard.API/RiskGuard.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
RUN addgroup --system riskguard && adduser --system --ingroup riskguard riskguard
COPY --from=build /app/publish .
USER riskguard
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "RiskGuard.API.dll"]
