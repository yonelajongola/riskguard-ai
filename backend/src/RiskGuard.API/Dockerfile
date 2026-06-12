FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/RiskGuard.API/RiskGuard.API.csproj
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
