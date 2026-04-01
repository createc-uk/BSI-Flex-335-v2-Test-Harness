# GitHub Copilot Instructions — SAPIENT BSI Flex 335 v2 Test Harness

## Project overview

This is the **SAPIENT BSI Flex 335 v2 Test Harness** (v5.2.4), a compliance-testing tool for developers building SAPIENT components. SAPIENT is a UK government standard for networks of Autonomous Sensors and Effectors (ASMs) managed by a Decision-Making Module (DMM). This harness lets developers verify that their ASM or DMM implementation correctly exchanges messages as defined by [BSI Flex 335 v2.0](https://knowledge.bsigroup.com/products/bsi-flex-335-v2-0-2023-sapient-network-of-autonomous-sensors-and-effectors-interface-control-document-specification-specification).

Crown-owned copyright 2021–2025. Licensed under Apache 2.0.

---

## Architecture

The solution (`SapientTestHarness.sln`) contains the following C# projects:

| Project | Target | Role |
|---|---|---|
| `SapientServices` | `net6.0` | Core library: TCP server/client, Protobuf message parsing, logging helpers |
| `SapientDatabase` | `net6.0` | PostgreSQL persistence layer via Npgsql |
| `SAPIENTMessageProcessor` | `net6.0` | Low-level TCP framing (4-byte length prefix + Protobuf payload) |
| `SAPIENTMessageProcessorInterface` | `net6.0` | Interfaces shared between services and processor |
| `ReadSampleSapientMessage` | `net6.0` | Utility for deserialising JSON sample SAPIENT messages |
| `SapientDataAgent` | `net6.0-windows` ⚠️ | **Windows Forms GUI** — the middleware hub; connects ASMs to the DMM and writes to PostgreSQL |
| `SapientAsmSimulator` | `net6.0-windows` ⚠️ | **Windows Forms GUI** — simulates an Autonomous Sensor Module (ASM) |
| `SapientDmmSimulator` | `net6.0-windows` ⚠️ | **Windows Forms GUI** — simulates a Decision-Making Module (DMM) |
| `SAPIENTMessageProcessor.UnitTests` | `net6.0` | NUnit tests for message framing |
| `SapientServices.UnitTests` | `net6.0` | NUnit tests for service layer |
| `SapientServicesValidator.UnitTests` | `net6.0` | NUnit tests using embedded JSON fixture files |
| `ReadSampleSapientMessage.UnitTests` | `net6.0` | NUnit tests for sample message reader |

### Message flow

```
ASM Simulator  ←TCP→  SapientDataAgent (middleware)  ←TCP→  DMM Simulator
                              ↓
                         PostgreSQL
```

- All messages are **Google Protobuf** encoded, framed with a **4-byte little-endian length prefix**
- Message schemas live in `SapientServices/sapient_msg/bsi_flex_335_v2_0/` (`.proto` files)
- Key message types: `Registration`, `RegistrationAck`, `StatusReport`, `DetectionReport`, `Task`, `TaskAck`, `Alert`, `AlertAck`, `Error`
- ULIDs are used for node/sensor identifiers

### TCP ports (default config)

| Component | Port |
|---|---|
| DataAgent ← ASM clients | 14000 |
| DataAgent ← SDA clients | 14001 |
| DataAgent ← DMM (HDA tasking) | 12002 |
| DataAgent → GUI | 12003 |

---

## Technology stack

- **Runtime**: .NET 6 (C# 10)
- **Messaging**: Google.Protobuf 3.25, Grpc.Tools 2.60 (compile-time proto generation)
- **Database**: PostgreSQL 12 via Npgsql 8.0; database name `sapientBSIFlex335v2`
- **Validation**: FluentValidation.AspNetCore 11.3
- **Serialisation**: Newtonsoft.Json 13 (for JSON sample messages)
- **Logging**: log4net 2.0.15 — configured via `app.config` / `dll.config`
- **UI**: Windows Forms (three GUI projects only)
- **Tests**: NUnit 3 via `dotnet test`
- **Style**: StyleCop.Analyzers (enforced on GUI projects)

---

## Building

```bash
# Restore & build (requires .NET 6 SDK)
dotnet restore SapientTestHarness.sln
dotnet build SapientTestHarness.sln

# Build only cross-platform projects (skips WinForms projects on non-Windows)
dotnet build SapientServices/SapientServices.csproj
dotnet build SapientDatabase/SapientDatabase.csproj
dotnet build SAPIENTMessageProcessor/SAPIENTMessageProcessor.csproj

# Run tests
dotnet test SapientTestHarness.sln
```

`Grpc.Tools` generates C# from `.proto` files at build time — no manual codegen step needed.

---

## Configuration

Runtime settings live in `app.config` / `<app>.dll.config` XML files (ApplicationSettings pattern). The `BuildExecutableFolder/` directory contains pre-configured sets of these files for a full multi-component deployment:

- `Files_DMM/` — DMM simulator config
- `Files_ASM1/`, `Files_ASM2/`, `Files_ASM3/` — ASM simulator configs
- `Files_ASMDataAgent1/`, `Files_ASMDataAgent2/`, `Files_ASMDataAgent3/` — Data agent configs for ASMs
- `Files_DMMDataAgent/` — Data agent config for the DMM side

Key settings to check when configuring a new deployment:
- `ClientAddress` / `TaskingAddress` — IP addresses
- `DACommunicationPort` / `HDATaskingPort` — TCP ports
- `DatabaseServer`, `DatabasePort`, `DatabaseUser`, `DatabasePassword`, `DatabaseName`
- `ValidationEnabled` — enable/disable Protobuf message validation
- `sendNullTermination` — must match between sender and receiver

---

## Platform constraints and Docker / Linux feasibility

### Current state: Windows-only

The three executable projects (`SapientDataAgent`, `SapientAsmSimulator`, `SapientDmmSimulator`) target `net6.0-windows` and use **Windows Forms**. They will not compile or run on Linux or macOS without changes.

### What IS cross-platform already

The library projects (`SapientServices`, `SapientDatabase`, `SAPIENTMessageProcessor`, `SAPIENTMessageProcessorInterface`, `ReadSampleSapientMessage`) target plain `net6.0` and have no Windows-specific dependencies. All unit tests are also cross-platform.

### Dockerisation options

#### Option A — Windows containers (least code change)

Build a Docker image using `mcr.microsoft.com/dotnet/framework/runtime` or `mcr.microsoft.com/dotnet/runtime:6.0-windowsservercore`. This requires Docker Desktop configured for Windows containers and runs only on a Windows host. The GUI will not display (headless WinForms is technically possible but fragile).

#### Option B — Port GUI projects to headless console apps (recommended for Debian/macOS Docker)

1. Change `<TargetFramework>net6.0-windows</TargetFramework>` → `net6.0`
2. Remove `<UseWindowsForms>true</UseWindowsForms>` and `<ImportWindowsDesktopTargets>true</ImportWindowsDesktopTargets>`
3. Replace `System.Windows.Forms.*` UI code with a console/`IHostedService` equivalent
4. The core logic in `SapientProtocol.cs`, `SapientMessageMonitor.cs`, `SapientMessageParser.cs` etc. does not depend on WinForms and can be reused as-is
5. Use `mcr.microsoft.com/dotnet/runtime:6.0` as the base image

A minimal `docker-compose.yml` for Option B would look like:

```yaml
services:
  postgres:
    image: postgres:12
    environment:
      POSTGRES_DB: sapientBSIFlex335v2
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: password
    ports:
      - "5432:5432"

  data-agent:
    build:
      context: .
      dockerfile: SapientDataAgent/Dockerfile
    depends_on:
      - postgres
    environment:
      - DatabaseServer=postgres
      - DatabasePort=5432

  asm-simulator:
    build:
      context: .
      dockerfile: SapientAsmSimulator/Dockerfile
    depends_on:
      - data-agent

  dmm-simulator:
    build:
      context: .
      dockerfile: SapientDmmSimulator/Dockerfile
    depends_on:
      - data-agent
```

> **Note**: The config files currently use `127.0.0.1` for all addresses. In Docker these must be changed to service names or environment-variable overrides.

---

## Coding conventions

- Crown copyright header on every file: `// Crown-owned copyright, 2021-YYYY`
- XML doc comments on all public types and members
- StyleCop enforced on GUI projects (`stylecop.json` present)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled
- Log4net for all logging — use `LogManager.GetLogger(typeof(MyClass))`
- Avoid raw SQL string concatenation — the codebase has known SQL injection risks (see README known issues); prefer parameterised queries with Npgsql
- Assembly/file version format: `3352.{major}.{minor}.{patch}` (e.g. `3352.5.2.4`)

---

## Domain glossary

| Term | Meaning |
|---|---|
| **SAPIENT** | Sensing for Asset Protection with Integrated Electronic Networked Technology — UK government autonomous sensor standard |
| **BSI Flex 335 v2** | The interface specification this harness validates against |
| **ASM** | Autonomous Sensor Module — a sensor node that registers, reports detections, and responds to tasks |
| **DMM** | Decision-Making Module — the command node that tasks ASMs and receives their data |
| **HDA** | High-level Data Agent — the DMM-side data agent |
| **SDA** | Sensor Data Agent — the ASM-side data agent |
| **DataAgent** | Middleware hub process that brokers messages between ASMs and the DMM |
| **Registration** | First message an ASM sends to announce its capabilities |
| **DetectionReport** | ASM → DMM sensor observation |
| **Task** / **TaskAck** | DMM → ASM command / ASM acknowledgement |
| **ULID** | Universally Unique Lexicographically Sortable Identifier — used for node IDs |
