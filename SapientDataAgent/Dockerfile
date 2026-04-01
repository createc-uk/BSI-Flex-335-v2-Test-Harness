# Multi-stage build — DataAgent (headless middleware hub)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy solution and all project files first (layer-cache friendly)
COPY SapientTestHarness.sln ./
COPY SapientDataAgent/SapientDataAgent/SapientDataAgent.csproj SapientDataAgent/SapientDataAgent/
COPY SapientServices/SapientServices.csproj SapientServices/
COPY SapientDatabase/SapientDatabase.csproj SapientDatabase/
COPY SAPIENTMessageProcessor/SAPIENTMessageProcessor.csproj SAPIENTMessageProcessor/
COPY SAPIENTMessageProcessorInterface/SAPIENTMessageProcessorInterface.csproj SAPIENTMessageProcessorInterface/
COPY ReadSampleSapientMessage/ReadSampleSapientMessage.csproj ReadSampleSapientMessage/
RUN dotnet restore SapientDataAgent/SapientDataAgent/SapientDataAgent.csproj

# Copy source and build
COPY . .
RUN dotnet publish SapientDataAgent/SapientDataAgent/SapientDataAgent.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Override config with Docker-specific settings (DB host = 'postgres', listen on 0.0.0.0)
COPY docker/data-agent.dll.config ./SapientDataAgent.dll.config

EXPOSE 14000 14001 12002 12003

ENTRYPOINT ["dotnet", "SapientDataAgent.dll"]
