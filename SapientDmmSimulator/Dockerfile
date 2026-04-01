# Multi-stage build — DMM Simulator (headless decision-making module)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY SapientTestHarness.sln ./
COPY SapientDmmSimulator/SapientDmmSimulator/SapientDmmSimulator.csproj SapientDmmSimulator/SapientDmmSimulator/
COPY SapientServices/SapientServices.csproj SapientServices/
COPY SAPIENTMessageProcessor/SAPIENTMessageProcessor.csproj SAPIENTMessageProcessor/
COPY SAPIENTMessageProcessorInterface/SAPIENTMessageProcessorInterface.csproj SAPIENTMessageProcessorInterface/
COPY ReadSampleSapientMessage/ReadSampleSapientMessage.csproj ReadSampleSapientMessage/
RUN dotnet restore SapientDmmSimulator/SapientDmmSimulator/SapientDmmSimulator.csproj

COPY . .
RUN dotnet publish SapientDmmSimulator/SapientDmmSimulator/SapientDmmSimulator.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Override config — connects to 'data-agent' service instead of 127.0.0.1
COPY docker/dmm-simulator.dll.config ./SapientDmmSimulator.dll.config

ENTRYPOINT ["dotnet", "SapientDmmSimulator.dll"]
