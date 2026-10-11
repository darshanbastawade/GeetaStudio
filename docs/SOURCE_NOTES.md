# Source notes and version information

Prepared: 10 October 2026.

## Project specification

`docs/ROADMAP.md` is a byte-for-byte copy of the accepted **Geeta Studio — revised M0–M27 execution roadmap**, version 2.1. It remains the feature/acceptance specification. The implementation guide adds the local development workflow; it does not declare milestones implemented or replace the accepted roadmap.

Source SHA-256: `c6a0575f38bb11757af58a2e2e1d7c0eb8b69a3b742712aac368fe55c1b8cf3a`.

Each milestone prompt embeds the complete original milestone section and common Codex instructions, followed by proposed execution slices and a concrete demonstration. Character and trend requirements remain in the accepted specification. The roadmap itself records the limitations of reconstructing the older linked discussion; this pack does not assert that the inaccessible original thread was fetched.

## Official setup references checked

- [OpenAI: Codex IDE extension](https://developers.openai.com/codex/ide/) — install the official extension, open its sidebar, work on the local folder, and review changes with Git checkpoints. Some OpenAI documentation URLs currently redirect to ChatGPT Learn.
- [OpenAI: Windows sandbox](https://developers.openai.com/codex/windows/) — native Windows operation is supported; WSL is optional depending on the toolchain.
- [OpenAI: repository instructions with AGENTS.md](https://developers.openai.com/codex/guides/agents-md/) — repository guidance discovery and precedence. Keep this repository's instruction file concise and its long specifications in docs.
- [Microsoft: C# in VS Code](https://code.visualstudio.com/docs/csharp/get-started) — editor, C# tooling, and SDK setup.
- [Microsoft: C# debugging in VS Code](https://code.visualstudio.com/docs/csharp/debugging) — project debugging and launch configuration options.
- [Microsoft: install .NET on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows) — SDK installation and SDK/runtime distinction.
- [Microsoft: .NET 10 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) — obtain the current stable compatible SDK.
- [Microsoft: development user secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0) — project initialization, local configuration, and the limits of local secret storage.

Additional official entry points: [VS Code](https://code.visualstudio.com/), [Git for Windows](https://git-scm.com/download/win), and [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads). Follow current platform requirements and license terms shown by each installer/service.

API versions, provider capabilities, extension UI, and package releases can change. At each implementation milestone, Codex must check the official documentation for the exact selected service/version and record qualification evidence. This pack deliberately avoids pinning an unverified vendor model name or exact SDK patch.

## Validation limits

The pack was checked for all 28 prompts, exact preservation of the accepted milestone/common specifications, matching roadmap hash, valid JSON, resolvable internal document links, and ZIP integrity. It contains no implementation or executable PowerShell scripts yet. No application build, SQL integration, Windows F5 session, or external provider operation is claimed to have run as part of preparing this pack. M0 and later milestone reports must capture those results on the actual repository/workstation.
