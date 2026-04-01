# Multi-stage build — ASM Simulator (headless sensor simulator)
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

COPY SapientTestHarness.sln ./
COPY SapientAsmSimulator/SapientAsmSimulator/SapientAsmSimulator.csproj SapientAsmSimulator/SapientAsmSimulator/
COPY SapientServices/SapientServices.csproj SapientServices/
COPY SAPIENTMessageProcessor/SAPIENTMessageProcessor.csproj SAPIENTMessageProcessor/
COPY SAPIENTMessageProcessorInterface/SAPIENTMessageProcessorInterface.csproj SAPIENTMessageProcessorInterface/
COPY ReadSampleSapientMessage/ReadSampleSapientMessage.csproj ReadSampleSapientMessage/
RUN dotnet restore SapientAsmSimulator/SapientAsmSimulator/SapientAsmSimulator.csproj

COPY . .
RUN dotnet publish SapientAsmSimulator/SapientAsmSimulator/SapientAsmSimulator.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Override config — connects to 'data-agent' service instead of 127.0.0.1
COPY docker/asm-simulator.dll.config ./SapientASMsimulator.dll.config

ENTRYPOINT ["dotnet", "SapientASMsimulator.dll"]
