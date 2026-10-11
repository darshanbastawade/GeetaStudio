# M12 local run and debug

Run in PowerShell from C:\dev\GeetaStudio. Use the existing .NET10/SQL/Windows staff setup; no new account, provider key or package.

~~~powershell
Set-Location C:\dev\GeetaStudio
.\scripts\db-update.ps1 -ProvisionRuntime -NoBuild
.\scripts\verify.ps1 -Locked -Sql -LowMemory
~~~

Migration adds evaluation history without resetting databases. API gets SELECT/INSERT on evaluation tables only; the standalone Worker keeps its existing privileges.

Start API, Web and Worker in three terminals:

~~~powershell
dotnet run --project .\src\GeetaStudio.Api --no-build --launch-profile Development
~~~
~~~powershell
dotnet run --project .\src\GeetaStudio.Web --no-build --launch-profile Development
~~~
~~~powershell
dotnet run --project .\src\GeetaStudio.Worker --no-build --launch-profile Development
~~~

Open http://localhost:5100/staff/evaluation with your Windows staff account, or its link from http://localhost:5200. Choose your owned tenant/universe IDs from the existing core staff API. Evaluations run on the explicit staff request; no evaluation worker, remote call or automatic route activation is added.

The page lists versioned datasets/candidates, runs a selected development or held-out split, compares against a development baseline, and records dataset/candidate/review/advisory JSON requests. Deterministic findings, human rubric scores, fixture reviews, advisory scores, versions, sample sizes, failures, costs/currency and latency provenance appear in report details. Text is rendered as text, not executable HTML. Held-out cases are excluded from dataset listing and revealed in their once-scored report.

For a Windows fixture demonstration, stop your API/debug host first; port5100 must be free:

~~~powershell
.\scripts\evaluation-demo.ps1 -ManageApi
~~~

The script only uses GeetaStudio_M1Tests. It creates isolated source/character fixtures,120 cases (100 Development/20 HeldOut),120 explicit fixture labels and controlled A/B/bad candidates. It starts hidden owned API hosts with Windows authentication, checks different wording against the same properties, injects fabricated citations, unsafe audience instructions and fluent cast/event/media failures, consumes the held-out set and restarts its own API. Reports survive restart. Release stays Blocked because human reviews and real provider qualification are pending. Script stops only its own hosts. Evidence: docs/milestones/evidence/M12-demo.json. Local full dataset and IDs: .local/M12-dataset.json and .local/M12-fixture.json. Repeating the demo creates a fresh isolated scope; it does not reuse a scored held-out set or upgrade real M04 permissions.

Fixture coverage is20 scenario families with six correlated contexts, not120 independently reviewed theological questions. English staff fixture wording does not qualify Hindi/Marathi translation, character appeal, comprehension or native-language quality. Human release gates require100 genuine reviewed Development labels,20 genuine reviewed HeldOut labels and human acceptance of every release-sample result. All four1–5 rubric scores must be>=4. Cast release reviews additionally require appeal and understanding scores at least4 with age-appropriate aggregate feedback evidence; do not enter children's personal data. Evidence prefixed fixture: cannot count as Human; genuine Human reviews require an explicit manual: evidence attestation.

Dataset and candidate versions append; stable operation replay returns the immutable result. A new operation cannot rescore consumed held-out questions, even under a copied dataset version. Refresh genuinely new cases. No fixture or advisory score bypasses deterministic evidence, current rights, cast or human gates. Keep tuning examples in a separate dataset.

Recorded outputs use adapter recorded.v1 and an exact case-to-output mapping. A qualified provider claim additionally requires a completed non-fixture M07 run bound to the exact body/cost/currency, owned scope, route, prompt and evaluand adapter. Imported output alone remains unqualified. Optional factory IDs bind the exact immutable M11 body/locale and current withholding state. Judge results are imported versioned advisory evidence only; M12 does not invoke a live judge or spend money. Live evaluands/judges must be admitted through M07.

Optional candidate Retrieval/ RetrievalIndexId bindings run the actual owned M06 engine. Pin its ready local concept-projection index, locale and embedding space; RetrievalVersion must equal that space. Each case can store a reviewed RetrievalQuery. Reports keep actual index/space/adapter/model/outcome/text IDs. Alias cutover fails new evaluation instead of silently changing the candidate and blocks current qualification of the old alias binding. M06 retrieval now respects an existing owning transaction; standalone calls retain their original transaction behavior.

Retrieval accuracy uses the citation/missing-source/no-answer/forced-connection subset. Without an actual retrieval binding it is a fixture/output evidence-and-no-answer score, not measured search performance. Candidate threshold is the pinned measured baseline accuracy; any fabricated/unresolvable required citation still fails the hard gate. Latency threshold is max(50ms,2×measured baseline p95). Local latency measures local processing; bound M07 samples use persisted completed-attempt elapsed times. Import-validation time is labeled and cannot qualify a provider. Costs in different currencies cannot be added or compared.

Debug:
1. Open trusted C:\dev\GeetaStudio in VS Code; stop foreground API/Web hosts.
2. Select existing **Geeta Studio: all**, F5.
3. Breakpoints: QualityEvaluation.RunAsync, EvaluationRules.Check and QualityEvaluation.Report; submit a run from /staff/evaluation.
4. Inspect pinned case/configuration, deterministic findings and qualification.Blocks. Continue F5; stop the compound with Shift+F5.
5. The task **Geeta Studio: M12 evaluation fixture demo** uses the same demo and needs5100 free.

Fresh interactive M12 breakpoint/visual/mobile/accessibility checks are unverified unless recorded in final evidence. Human theological/native-language/age-feedback review, live-provider evaluation, hosted CI/cold cache and production qualification remain open. M13 not started.



Verified here: locked build0 warnings/errors,81 offline/125 real-SQL tests,focused9,actual Windows fixture restart and restricted Development reads. Genuine human-reviewed cases0; release remains Blocked. No M13 work or public/live operation.
