# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Fakturio.Api.csproj ./
RUN dotnet restore Fakturio.Api.csproj

COPY . ./
RUN dotnet publish Fakturio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DisableHttpsRedirection=true \
    DOTNET_EnableDiagnostics=0

COPY --from=build /app/publish ./

RUN mkdir -p /data /app/uploads/logos \
    && chown -R app:app /data /app/uploads

USER app
EXPOSE 8080

VOLUME ["/data", "/app/uploads"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Fakturio.Api.dll"]
