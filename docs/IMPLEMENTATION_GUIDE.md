# Implement Geeta Studio M0–M27 with Codex in VS Code

Prepared 10 October 2026. Based on the accepted roadmap version 2.1. This is an execution guide and prompt pack; implementation status starts as **Not Started** until repository evidence establishes otherwise.

Use one repository, one selected milestone, and one reviewable change at a time. A milestone can take several Codex conversations. Working software and acceptance evidence determine completion.

## 1. Prepare your Windows workstation

Install or confirm these tools:

| Tool | What to do | When |
|---|---|---|
| Git | Install Git for Windows from its official site | Before M0 |
| VS Code | Install the desktop editor and open the project folder | Before M0 |
| .NET 10 SDK | Install the current stable SDK matching your CPU; a runtime-only install is insufficient for building | Before M0 |
| Microsoft C# extension | Install the extension published by Microsoft | Before M0 |
| C# Dev Kit | Recommended for Solution Explorer and integrated debugging; follow its applicable sign-in/licensing requirements | Before M0 |
| OpenAI Codex extension | Install through the official OpenAI IDE documentation link; sign in and open the local folder | Before M0 |
| SQL Server | Use an existing development instance or install one, with a new `GeetaStudio_Dev` database | Before M1 |
| SQL editor / SSMS | Optional, for inspecting the development database | M1 onward |

Open a **new** PowerShell terminal after installation and run:

```powershell
git --version
code --version
dotnet --info
dotnet --list-sdks
```

Confirm that a stable `10.0.xxx` SDK is installed. If `code` is not on PATH, open VS Code normally and use File → Open Folder. Ask M0 to pin the actual compatible installed .NET 10 SDK, rather than copying an arbitrary patch version.

Native Windows is the default. Codex currently supports native Windows operation, so WSL is not a prerequisite. Complete the extension's normal Windows sandbox setup if prompted and retain its permission controls. Choose WSL later only if your actual tooling needs a Linux environment. Do not mix Windows and WSL paths/tool installations accidentally.

Official references and download links are in [SOURCE_NOTES.md](SOURCE_NOTES.md).

## 2. Put the starter pack in your project folder

For a new project, extract the ZIP so the root is `C:\dev\GeetaStudio` and `AGENTS.md` is directly inside it. Avoid an accidental second nested `GeetaStudio` folder.

```powershell
Set-Location C:\dev\GeetaStudio
code .
```

If you already have a Geeta Studio repository, open that repository instead. Copy in the roadmap and execution documents, merging existing `AGENTS.md`, README, and editor settings. Preserve existing source code and project history. M0 must audit and extend it; it must not assume an empty project.

For a new repository only:

```powershell
git init -b main
git status --short
git add .
git diff --cached --stat
git commit -m "docs: add Geeta Studio implementation plan"
git switch -c milestone/m00
```

If Git asks for identity, configure your own name and email before committing. For an existing repository, make or request an appropriate checkpoint for your current work, then create a milestone branch from the intended base. Do not reset uncommitted work.

No GitHub account or cloud deployment is required to begin locally.

## 3. Open Codex with the right context

Open the Codex sidebar in VS Code, sign in, and select local work in the current folder. If the icon is missing, use the Command Palette and run **Codex: Open Codex Sidebar**. Use the coding model offered by your installation; this guide does not depend on a particular model name.

Keep `AGENTS.md` at the root. Codex reads repository guidance from that file. Keep the long roadmap in `docs/ROADMAP.md` and point each task at its relevant prompt and current state. This avoids relying on the assistant remembering an earlier chat.

The pack contains:

- 28 prompt files, `docs/prompts/M00.md` through `M27.md`, each including the common requirements, complete milestone specification, suggested slices, and local demonstration.
- A progress/resume file, `docs/PROJECT_STATE.md`.
- A dependency and demo matrix, `docs/MILESTONE_MATRIX.md`.
- An evidence template at `docs/milestones/_TEMPLATE.md`.
- A run/debug contract at `docs/LOCAL_DEVELOPMENT.md`.

## 4. Execute M0 first

Paste this into Codex:

```text
Read AGENTS.md, docs/PROJECT_STATE.md, docs/LOCAL_DEVELOPMENT.md,
and docs/prompts/M00.md. Inspect the repository and current tools first.
Implement M0 only, preserving existing work. Create or complete the .NET 10
solution, API, Blazor web, worker, architectural boundaries, focused tests,
and Windows/VS Code run/debug setup. Follow the M0 acceptance criteria.
Run the checks available here and fix relevant failures. Update
docs/milestones/M00.md, docs/PROJECT_STATE.md, and the coverage matrix.
Tell me the exact commands and URLs to run the result, and clearly identify
anything you could not verify. Do not start M1 yet.
```

M0 must deliver the smallest runnable foundation: three hosts, a shell showing real API health, worker startup, a build/test workflow, project boundaries, repository instructions, and working VS Code configurations. No paid API keys or SQL Server are needed for this first milestone.

Ask for the development commands in [LOCAL_DEVELOPMENT.md](LOCAL_DEVELOPMENT.md) to be verified against the actual generated projects. The starter ZIP itself cannot be run with `dotnet run` until this work is done.

Before accepting M0:

1. Build and run the solution using the documented commands.
2. Open the web app and confirm its API status.
3. Start the worker and observe its development status/heartbeat.
4. Set a breakpoint and debug a host in VS Code.
5. Confirm the architecture gate detects a forbidden dependency through its controlled negative check.
6. Review the evidence report for actual test results and remaining limitations.

## 5. Run and debug after M0

Expected initial commands, subject to M0's verified paths:

```powershell
dotnet restore .\GeetaStudio.sln
dotnet build .\GeetaStudio.sln --no-restore
dotnet test .\GeetaStudio.sln --no-build
```

Then use separate terminals:

```powershell
# API terminal
dotnet run --project .\src\GeetaStudio.Api --launch-profile Development
```

```powershell
# Web terminal
dotnet watch --project .\src\GeetaStudio.Web --launch-profile Development
```

```powershell
# Worker terminal
dotnet run --project .\src\GeetaStudio.Worker --launch-profile Development
```

Target web URL: `http://localhost:5200`. Target API liveness URL: `http://localhost:5100/health/live`. Codex must update these if an existing repository or a port conflict requires different settings.

For F5 debugging, stop those running hosts first. Select the `Geeta Studio: all` compound configuration created by M0. Keep the three-terminal commands as a simple troubleshooting fallback. See the local development contract for SQL setup from M1 onward.

## 6. Repeat the same milestone cycle

For M1, start a new Codex conversation in the **same repository** and send:

```text
Read AGENTS.md, docs/PROJECT_STATE.md, the relevant dependency evidence,
and docs/prompts/M01.md. Inspect the code, then implement M1 only.
Follow the prompt's slices and acceptance criteria. Keep the local app
runnable, run meaningful checks, fix relevant failures, and update the
milestone evidence and current state. Report remaining live prerequisites.
```

For later milestones, replace `M01` and `M1` with the required number. The zero-padded filenames are for sorting; M00 is M0, M01 is M1, and so on.

You can also open the complete prompt in VS Code and attach it to Codex. Or copy its text in PowerShell:

```powershell
Get-Content -Raw -Encoding UTF8 .\docs\prompts\M01.md | Set-Clipboard
```

Use this cycle:

1. **Read state:** identify the selected milestone, prerequisite evidence, and open blockers.
2. **Implement:** let Codex work through its small slices and fix the relevant failures. Do not request the entire M0–M27 program in one task.
3. **Run:** launch the app and perform the milestone's visible demo from the matrix.
4. **Review:** inspect the diff, especially migrations, authorization, provider routes, retry behavior, and rights/cost controls touched by the change.
5. **Record:** update the evidence file and current state with tests actually executed and any limitations.
6. **Checkpoint:** commit the reviewed change. Then proceed to the next milestone whose prerequisites are satisfied.

For the new-repository branch example, after reviewing a completed M0:

```powershell
git status --short
git diff --stat
# Review the actual file diff in VS Code before staging.
git add .
git diff --cached --stat
git commit -m "feat: complete M0 engineering foundation"
git switch main
git merge --ff-only milestone/m00
git switch -c milestone/m01
```

Stage only intended files if you also have unrelated work. If a fast-forward merge fails because the base changed, stop and resolve the branch history; do not force-reset it. Adapt branch names to an existing repository's conventions.

## 7. Use the full M0–M27 sequence

Follow [MILESTONE_MATRIX.md](MILESTONE_MATRIX.md) in order. It gives the implementation slices and a concrete demonstration for **every** milestone. Each linked prompt preserves its original dependencies, deliverables, acceptance criteria, and scope boundary.

| Stage | Milestones | What you should have |
|---|---|---|
| Runnable foundation | M0–M2 | Local hosts, durable jobs, configurable routes, domain/database foundations |
| Trustworthy knowledge | M3–M6 | Rights/provenance, scripture corpus, evidence, concepts and retrieval |
| Generation controls | M7–M10 | AI/cost routing, workflows, audiences/languages, dynamic characters |
| First product | M11–M14 | Drafting, evaluation, editorial review, READ release candidate |
| Trend and video | M15–M17 | Verified trend briefs, reusable assets, reviewed WATCH output |
| Distribution and business | M18–M20 | Channels, analytics, identity/households, credits and payments |
| Interaction and products | M21–M24 | Dadaji text/voice, personalized CREATE, books and B2B |
| Expansion and improvement | M25–M27 | Hardening, a second universe, measured optimization |

M4 includes a small internal reader. M14 is the first public READ release gate. M17 can demonstrate WATCH with approved export; social publication follows M18. TALK and CREATE are later product gates. You do not need to finish M27 before learning from a working READ product, but each release must satisfy the roadmap's applicable rights, identity, safety, reliability, and operational gates. M25 is not permission to defer essential protection until then.

## 8. Introduce real services when needed

| Milestone | Prepare | Offline path |
|---|---|---|
| M0 | Development tools | All foundation checks use fixtures |
| M1–M2 | Dedicated SQL database and staff development access | Real local SQL for persistence; fake external operations |
| M3–M4 | Rights-reviewed Geeta sources and a small approved import set | Synthetic parser fixtures clearly separated from published/canonical scripture |
| M6 | Selected embedding/retrieval integration if required | Controlled corpus/retriever fixtures; semantic quality remains unqualified |
| M7 | One intended text provider, credentials, explicit budget | Deterministic adapters exercise routing and errors |
| M9–M12 | Language/audience reviewers, character bible, review examples | Fixed reference cases; mark human/provider reviews still pending |
| M15 | Permitted feed/API or submitted links, source coverage | Recorded trend fixtures with evidence timestamps |
| M16–M17 | Approved visual/voice assets and media/rendering providers | Owned sample assets and deterministic render path; record gaps |
| M18 | Channel accounts, app permissions, supported operations | Dry-run/export and simulated callbacks; qualify each channel separately |
| M19 | Channel/financial metric permissions and definitions | Labeled sample events, never represented as real income |
| M20 | Identity and payment sandbox credentials | Fake payment tests plus separate sandbox qualification |
| M21–M23 | Audience controls, approved conversation/media providers | Fixtures and budgeted sandbox evaluation |
| M24 | Rights/print-commerce/fulfillment requirements | Export and sandbox order flow |
| M25 | Chosen deployment target and operational access | Local/load/staging checks; production gates remain explicit |
| M26–M27 | Reviewed second-universe sources and evaluation evidence | Isolated fixtures and proposals requiring the defined approval gates |

Configurable provider selection applies across all integrations, including tools and self-hosted engines. Switching among installed, qualified adapters can use configuration. New vendors still require adapter work, and existing external resources retain their original bindings.

For characters, preserve the seed cast and make the registry extensible. At M10, prove that a new situation-specific character can be added through data/admin operations and selected without rebuilding. At M16/M17, prove that approved visual and voice profiles and episode snapshots preserve continuity.

For trends, M15 must convert discovery into a verified, cited story brief with an audience and cast recommendation. M17 turns an approved script/asset set into a video. Avoid an automatic path from an unverified trending item directly to publication.

## 9. Resume, review, and fix with short prompts

**Resume an interrupted milestone:**

```text
Read AGENTS.md, docs/PROJECT_STATE.md, and the current milestone evidence.
Inspect the working tree and identify the last verified point. Continue the
remaining work for that milestone without recreating completed parts.
```

**Review before accepting a milestone:**

```text
Review the current milestone diff against its prompt and acceptance criteria.
Identify concrete defects and missing evidence, especially at the changed
authorization, persistence, retry, provider, rights, and cost boundaries.
Fix relevant defects, rerun targeted checks, and update the evidence.
Do not expand into the next milestone.
```

**Fix a local failure:**

```text
The current milestone fails when I run the documented command below.
Inspect the error and relevant code/configuration, find the cause, make the
smallest durable fix, and rerun the relevant checks. Preserve existing data.
Command: <paste command>
Redacted error: <paste error without credentials or personal content>
```

Do not accept “all tests pass” without the command and result. A missing live credential is a visible qualification gap, not a reason to invent a successful connection. Independent offline work can continue while that gap remains tracked.

## 10. Common local problems

| Problem | Next action |
|---|---|
| No .NET SDK found | Check `dotnet --list-sdks`, install the SDK, restart the terminal, then compare `global.json` with installed SDKs |
| `code` not found | Open VS Code from the Start menu and use Open Folder; update PATH through the installer if desired |
| Codex does not see the project | Open the folder containing `AGENTS.md` and the solution; start a new local conversation |
| Debug profile missing | M0 has not created/verified it yet; use the documented terminal commands and ask Codex to finish the debug setup |
| Port already in use | Stop the prior host you launched, or change that host's port and all corresponding base-address/debug settings |
| SQL connection fails | Verify the selected instance, authentication, database permissions, and local certificate settings; do not disable production validation |
| UI starts but API is unavailable | Confirm API process/URL and web base-address configuration; show a clear UI failure rather than fake success |
| PowerShell script is blocked | Use the documented individual commands and your organization's approved script policy; do not globally disable execution restrictions |
| NuGet restore cannot reach packages | Check the permitted network/proxy/package-source setup; record the blocked restore instead of claiming a build |
| Provider call fails | Inspect redacted normalized errors, route/capabilities, credential status, and budget; never blindly retry an uncertain paid side effect |
| Codex's environment cannot reproduce Windows debugging | Record that limitation and run the documented F5 check on your workstation |

Your first goal is a reproducible M0 run/debug loop. Once that works, the same workflow carries the project through all 28 milestones.
