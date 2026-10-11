# Geeta Studio / ContentOS

M0 establishes a .NET 10 modular monolith with separate API, server-interactive Blazor web, and worker hosts. API health is real; fixture responses are Development-only and labeled. M1 adds the SQL-backed runtime, secured staff API, provider routing, durable recovery and local assets. Runtime-disabled mode still needs no database; deterministic adapters require no paid credentials. M2 adds reusable content/universe contracts, tenant isolation, versioned revisions, scoped import/export and character foundations. M3 adds reviewed source editions, machine-readable rights, attributed exports, source/asset lineage and revocation remediation. M4 adds a source-preserving canonical importer, complete edition inventory, separate annotations and a secured chapter reader with 25 reviewed Sanskrit/English pairs; 676 edition entries remain missing. M5 adds reviewed claim versions, exact evidence spans, attributed disagreements and correction impact, with evidence gates on approval/shared reads/export. M6 adds reviewed concepts, hybrid evidence retrieval, versioned embedding spaces and validated index activation/rollback. M7 adds versioned AI task routes, structured/streaming generation, shared provider controls, currency-aware reservations and usage settlement/reconciliation, and pinned embedding routes. Local fixtures are the default; the configurable real text adapter remains live-unqualified. M8 adds immutable prompts/workflow definitions, durable leased checkpoints, bounded typed tools and explicit review pause/resume with retained AI bindings.

```powershell
Set-Location C:\dev\GeetaStudio
.\scripts\dev-doctor.ps1
.\scripts\db-update.ps1 -ProvisionRuntime
.\scripts\verify.ps1 -Locked -Sql
```

Open three terminals and run:

```powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
dotnet watch --project .\src\GeetaStudio.Web --launch-profile Development
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
```

Open `http://localhost:5200`; API liveness is `http://localhost:5100/health/live`. Stop each terminal with Ctrl+C. In VS Code select **Geeta Studio: all** for F5; stop existing CLI hosts first.

- [Local run/debug details and troubleshooting](docs/LOCAL_DEVELOPMENT.md)
- [M8 workflows, worker restart demo and debugging](docs/M8_LOCAL_RUN.md)
- [M8 evidence and limits](docs/milestones/M08.md)
- [M7 AI routing, budgets, local demo and debugging](docs/M7_LOCAL_RUN.md)(docs/M6_LOCAL_RUN.md)
- [M6 evidence and limits](docs/milestones/M06.md)
- [M5 claim review, local demo and debugging](docs/M5_LOCAL_RUN.md)
- [M5 evidence and limits](docs/milestones/M05.md)
- [M4 corpus sample, local run and debugging](docs/M4_LOCAL_RUN.md)
- [M4 evidence and limits](docs/milestones/M04.md)
- [M3 source registry, local demo and debugging](docs/M3_LOCAL_RUN.md)
- [M3 evidence and limits](docs/milestones/M03.md)
- [M2 core setup, two-universe demo and debugging](docs/M2_LOCAL_RUN.md)
- [M2 evidence and limits](docs/milestones/M02.md)
- [M1 setup, run, staff demo, debugging and recovery](docs/M1_LOCAL_RUN.md)
- [M1 evidence and verification limits](docs/milestones/M01.md)
- [M0 foundation evidence](docs/milestones/M00.md)
- [Project state](docs/PROJECT_STATE.md) and [milestone matrix](docs/MILESTONE_MATRIX.md)
- [Architecture and integration boundaries](docs/ARCHITECTURE.md)
- [Decisions](docs/DECISIONS.md) and [external dependency register](docs/EXTERNAL_DEPENDENCIES.md)
- [Accepted roadmap 2.1](docs/ROADMAP.md), [implementation guide](docs/IMPLEMENTATION_GUIDE.md), [source notes](docs/SOURCE_NOTES.md)
- [Repository instructions](AGENTS.md) and [M0 prompt](docs/prompts/M00.md)

The roadmap and prompts are planned scope, not completion evidence. Existing planning material has been preserved. Review milestone evidence before advancing; no commits or remote changes were made by this implementation.

