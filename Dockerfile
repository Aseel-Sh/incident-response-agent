# syntax=docker/dockerfile:1
FROM node:26-bookworm-slim AS frontend
WORKDIR /src
COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts
COPY frontend/ frontend/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY IncidentResponseAgent.slnx Directory.Build.props ./
COPY IncidentResponseAgent.Domain/IncidentResponseAgent.Domain.csproj IncidentResponseAgent.Domain/
COPY IncidentResponseAgent.Application/IncidentResponseAgent.Application.csproj IncidentResponseAgent.Application/
COPY IncidentResponseAgent.Infrastructure/IncidentResponseAgent.Infrastructure.csproj IncidentResponseAgent.Infrastructure/
COPY IncidentResponseAgent.Agent/IncidentResponseAgent.Agent.csproj IncidentResponseAgent.Agent/
COPY IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj IncidentResponseAgent.Api/
RUN dotnet restore IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj
COPY IncidentResponseAgent.Domain/ IncidentResponseAgent.Domain/
COPY IncidentResponseAgent.Application/ IncidentResponseAgent.Application/
COPY IncidentResponseAgent.Infrastructure/ IncidentResponseAgent.Infrastructure/
COPY IncidentResponseAgent.Agent/ IncidentResponseAgent.Agent/
COPY IncidentResponseAgent.Api/ IncidentResponseAgent.Api/
COPY --from=frontend /src/IncidentResponseAgent.Api/wwwroot/ IncidentResponseAgent.Api/wwwroot/
ARG VERSION=1.0.0
RUN dotnet publish IncidentResponseAgent.Api/IncidentResponseAgent.Api.csproj -c Release --no-restore -o /out /p:UseAppHost=false /p:Version=${VERSION}

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /out/ ./
# Docker initializes new named volumes from these owned directories.
RUN mkdir -p /state/knowledge /state/telemetry /keys && cp -r /app/Runbooks/KnowledgeBase/. /state/knowledge/ && chown -R $APP_UID:$APP_UID /state /keys
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production \
    Storage__Incidents__IncidentRecordsPath=/state/incidents.json \
    Storage__Incidents__SessionDatabasePath=/state/sessions.sqlite \
    Storage__DataProtectionKeysPath=/keys \
    Runbooks__SemanticRetrieval__DatabasePath=/state/runbooks.sqlite \
    Runbooks__SemanticRetrieval__SourceRegistryPath=/state/sources.json \
    Runbooks__SemanticRetrieval__KnowledgeBasePath=/state/knowledge \
    Tools__OperationalData__ProjectRegistryPath=/state/projects.json \
    Tools__OperationalData__LogEntriesPath=/state/telemetry/logs.json \
    Tools__OperationalData__MetricSamplesPath=/state/telemetry/metrics.json \
    Monitoring__StatePath=/state/monitoring.json
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "IncidentResponseAgent.Api.dll"]
