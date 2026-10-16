# Geeta Studio / ContentOS

Geeta Studio runs on a reusable .NET 10 ContentOS modular monolith with separate API, server-interactive Blazor web and worker hosts. The controlled private implementation covers source rights and canonical samples, evidence/retrieval, durable AI workflows, casting, editorial evaluation, READ, trend briefs, media/WATCH, publishing exports, analytics, identity/credits, text TALK, CREATE and digital/institutional commerce. M26 reuses these paths for a private Shivaji historical sample; M27 adds guarded private experiments and selective automation. M22 realtime voice TALK remains missing.

Implementation and local fixture verification do not qualify live providers, human quality, source rights for broader use or production release. M12's genuine human-reviewed dataset and the milestone-specific release gates remain pending. The Geeta corpus is a 25/701 edition sample. Current M25.1 remediation, full-check evidence and remaining hosted CI gate are maintained in [project state](docs/PROJECT_STATE.md), the [milestone matrix](docs/MILESTONE_MATRIX.md) and [M25.1 report](docs/milestones/M25.1.md). Runtime-disabled mode needs no database; durable acceptance uses dedicated real SQL Server and deterministic adapters.

Prompt 4 now connects the factory to the existing M8/M7 AI workflow with pinned inputs, original operation recovery, bounded paid/unknown usage and private editorial import with pending fiction review. Deterministic templates remain available. See [M25.2 status/evidence](docs/milestones/M25.2.md) and [local run/debug](docs/M25.2_PROMPT4_LOCAL_RUN.md). M25.2 remains In Progress. Prompt 5 adds bounded configured RSS metadata intake with stable candidate history and current rights; it remains disabled/unqualified until a permitted feed is supplied. See [Prompt 5 report](docs/milestones/evidence/M25.2-prompt5.md) and [run/debug](docs/M25.2_PROMPT5_LOCAL_RUN.md). Genuine live acceptance remains pending; see the current Prompt 7 checkpoint below.

Prompt 6 adds reviewed-layout narration segmentation and exact voice/request receipts through existing M16/M17. The owner has no approved art/voice yet; qualified media acceptance remains pending. See [Prompt 6 report](docs/milestones/evidence/M25.2-prompt6.md) and [run/debug](docs/M25.2_PROMPT6_LOCAL_RUN.md). Prompt 7 now joins two audience variants through the same pipeline and M18 private packages; genuine live acceptance remains pending.

Prompt 7: final160offline/all256SQL in40fresh class processes pass, with two playable captioned fixtures, original operation reuse and withdrawal/expiry fences. The separately enabled live helper requires a bounded owner-approved budget and current reviewed real inputs. Fixture tones/art/events do not qualify live release. See [report](docs/milestones/evidence/M25.2-prompt7.md) and [one-command demo/debug](docs/M25.2_PROMPT7_LOCAL_RUN.md). M25.2 remains In Progress; Prompt8 is unselected.

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
- [M7 AI routing, budgets, local demo and debugging](docs/M7_LOCAL_RUN.md)
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

