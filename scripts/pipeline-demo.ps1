[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][ValidateSet('Fixture','Live')][string]$Mode,
    [ValidateSet('Generate','Render','Export')][string]$Stage='Generate',
    [switch]$EnableLive,
    [decimal]$MaxGenerationCost=0,
    [string]$Currency='USD',
    [string]$Configuration,
    [string]$Api='http://localhost:5100',
    [switch]$SkipBuild
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$repo=Split-Path $PSScriptRoot -Parent
# Validate spend opt-in before reading configuration, contacting an API, or starting a process.
if($Mode -eq 'Live'){
    if(!$EnableLive -or $MaxGenerationCost -le 0 -or $MaxGenerationCost -gt 1000){throw 'Live mode requires -EnableLive and an explicit positive -MaxGenerationCost (at most 1000). No work admitted.'}
    if($Currency -notin @('USD','INR')){throw 'Explicit supported budget currency required.'}
    if(!$Configuration){throw 'Provide a local reviewed-ID configuration; this script never creates rights, profiles or approvals.'}
    $uri=[uri]$Api
    if($uri.Scheme -ne 'http' -or $uri.Host -ne 'localhost' -or $uri.Port -ne 5100 -or $uri.AbsolutePath -ne '/' -or $uri.Query -or $uri.Fragment -or $uri.UserInfo){throw 'This helper supports only the existing local Windows API at http://localhost:5100.'}
}
Push-Location $repo
$savedSql=$env:GEETA_SQL_TESTS
try{
    if($Mode -eq 'Fixture'){
        if(!$SkipBuild){
            & dotnet build GeetaStudio.sln --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false
            if($LASTEXITCODE -ne 0){throw 'Build failed; restore the pinned dependencies first.'}
        }
        & powershell -NoProfile -ExecutionPolicy Bypass -File scripts/db-update.ps1 -Database GeetaStudio_M1Tests -NoBuild
        if($LASTEXITCODE -ne 0){throw 'Dedicated SQL test migration failed.'}
        $env:GEETA_SQL_TESTS='1'
        & dotnet test GeetaStudio.sln --no-build --no-restore -m:1 --filter 'FullyQualifiedName~SqlIntegratedPipelineTests.' --results-directory .local/M25-2-prompt7/demo --logger 'trx;LogFileName=pipeline.trx'
        if($LASTEXITCODE -ne 0){throw 'Integrated fixture failed. Inspect the TRX; do not treat older outputs as this run.'}
        [xml]$trx=Get-Content .local/M25-2-prompt7/demo/pipeline.trx -Raw
        if([int]$trx.TestRun.ResultSummary.Counters.passed -ne 1 -or [int]$trx.TestRun.ResultSummary.Counters.notExecuted -ne 0){throw 'The integrated fixture must execute, not skip.'}
        Write-Host 'Two private technical variants: .local/M25-2-prompt7/sample. Placeholder art and tones; no live event, narration permission, genuine human review or external charge.'
        Write-Host 'Withdrawal checks run last: generated local files are historical test evidence, not currently eligible distribution packages.'
        return
    }
    $config=Get-Content -LiteralPath $Configuration -Raw | ConvertFrom-Json
    $tenant=[guid]$config.tenant; $universe=[guid]$config.universe
    if($tenant -eq [guid]::Empty -or $universe -eq [guid]::Empty -or @($config.variants).Count -ne 2){throw 'Provide one owned scope and exactly two audience variants.'}
    function Get-Local([string]$Path){Invoke-RestMethod ($Api.TrimEnd('/')+$Path) -UseDefaultCredentials -TimeoutSec 30}
    function Post-Local([string]$Path,$Body){Invoke-RestMethod ($Api.TrimEnd('/')+$Path) -Method Post -UseDefaultCredentials -TimeoutSec 30 -Headers @{'X-ContentOS-Staff'='1'} -ContentType application/json -Body ($Body|ConvertTo-Json -Depth 80)}
    $suffix="/tenants/$tenant/universes/$universe"
    $factory='/api/v1/factory'+$suffix; $watch='/api/v1/watch'+$suffix; $social='/api/v1/social'+$suffix
    $plans=Get-Local "$factory/plans"; $briefs=Get-Local ('/api/v1/trends'+$suffix+'/stories')
    $trend='/api/v1/trends'+$suffix
    $eventClusters=Get-Local "$trend/clusters"; $connectors=Get-Local "$trend/connectors"
    $teachings=Get-Local "$trend/teachings"; $selectedTeachings=@()
    $audiences=@(); $clusters=@(); [decimal]$exposure=0
    # Validate BOTH variants before admitting either; M7 then reserves each bound operation.
    foreach($variant in $config.variants){
        $request=$variant.batch
        $plan=$plans | Where-Object Id -eq $request.planId
        $brief=$briefs | Where-Object FactoryPlanId -eq $request.planId
        if(!$plan -or $plan.reviewStatus -ne 'Approved' -or !$plan.snapshot -or !$brief -or $brief.state -ne 'Approved' -or !$brief.snapshot){throw 'Current reviewed eligible trend/factory plans required.'}
        $cluster=$eventClusters|Where-Object Id -eq $brief.snapshot.clusterId
        if(!$cluster -or $cluster.state -ne 'Verified' -or @($cluster.blocks).Count){throw 'Current verified event evidence required.'}
        foreach($candidate in $cluster.candidates){
            $connector=$connectors|Where-Object Id -eq $candidate.connectorId
            if(!$connector -or !$connector.enabled -or $connector.configuration.mode -ne 'Manual' -or $connector.configuration.evidenceReference -notlike 'manual:*'){throw 'Live mode rejects fixture or unavailable event connectors.'}
        }
        $teaching=$teachings|Where-Object Id -eq $brief.snapshot.teachingId
        if(!$teaching -or $teaching.outcome -ne 'Matched' -or $teaching.reviewStatus -ne 'Approved' -or @($teaching.blocks).Count -or !$teaching.retrieval){throw 'Exact current reviewed teaching connection required.'}
        $selectedTeachings += $teaching
        $generation=$plan.snapshot.generationWorkflow
        if(!$generation -or @($plan.snapshot.briefs).Count -ne 1 -or @($generation.profiles).Count -lt 1){throw 'One pinned M7/M8 audience workflow per variant required.'}
        foreach($profile in $generation.profiles){if($profile.settings.qualification -ne 'LiveSandboxVerified' -or $profile.settings.adapter -like 'fixture.*' -or $profile.settings.evidenceReference -like 'fixture:*'){throw 'Live mode rejects fixture/unqualified AI profiles, including fallback profiles.'}}
        if($request.budget.currency -ne $Currency -or $generation.route.settings.currency -ne $Currency -or [decimal]$request.budget.maxGenerationCost -lt [decimal]$generation.route.settings.runLimit -or [decimal]$request.budget.maxGenerationCost -le 0 -or [int]$request.budget.maxItems -ne 1 -or [guid]$request.operationId -eq [guid]::Empty){throw 'Stable one-item batch and sufficient bounded same-currency budget required.'}
        $exposure += [decimal]$request.budget.maxGenerationCost
        $audiences += $plan.snapshot.briefs[0].brief.cast.audienceId; $clusters += $brief.snapshot.clusterId
    }
    if($exposure -gt $MaxGenerationCost -or @($audiences|Select-Object -Unique).Count -ne 2 -or @($clusters|Select-Object -Unique).Count -ne 1){throw 'Two distinct audiences must share one event within the aggregate owner-approved limit.'}
    $directory=Join-Path $repo '.local/M25-2-prompt7/live'; New-Item -ItemType Directory -Force $directory|Out-Null
    $results=@()
    foreach($variant in $config.variants){
        if($Stage -eq 'Generate'){
            $batch=Post-Local "$factory/batches" $variant.batch
            $results += $batch
        }elseif($Stage -eq 'Render'){
            $request=$variant.watch
            # The bounded live pilot reuses approved imported recordings/art; generation has separate M16 admission.
            if([decimal]$request.maxMediaCost -ne 0 -or @($request.scenes|Where-Object {$_.imageGeneration -or $_.voiceGeneration -or !$_.imageVersionId -or !$_.voiceVersionId}).Count -gt 0){throw 'Render stage requires explicit reviewed imported per-scene art/recordings and zero new media spend.'}
            $items=Get-Local ('/api/v1/editorial'+$suffix+'/items/'+$request.editorialItemId)
            if(!@($items.factoryHistory|Where-Object {$_.planId -eq $variant.batch.planId}).Count){throw 'The reviewed script must come from this exact audience plan.'}
            if(!@($items.approvals|Where-Object {$_.current -and $_.mode -eq 'Manual'}).Count){throw 'Genuine current manual editorial approval required before live rendering.'}
            $results += Post-Local "$watch/productions" $request
        }else{
            $packageId=[guid]$variant.packageId
            if($packageId -eq [guid]::Empty){throw 'Prepare/review the exact M18 package in the existing workbench before export.'}
            $package=Get-Local "$social/packages/$packageId/export"
            if(!$package.package.snapshot -or $package.package.snapshot.account.mode -eq 'Fixture'){throw 'Live exports require a real ExportOnly account binding, not a fixture account.'}
            $production=[guid]$package.package.snapshot.request.watchProductionId
            $master=Get-Local "$watch/productions/$production/export"
            if(!@($master.production.snapshot.script.factoryHistory|Where-Object {$_.planId -eq $variant.batch.planId}).Count){throw 'The exported script must come from this exact audience plan.'}
            foreach($artifact in $master.production.artifacts){
                $file=Join-Path $directory (([guid]$artifact.id).ToString()+'.mp4')
                Invoke-WebRequest ($Api.TrimEnd('/')+"$watch/artifacts/$($artifact.id)/preview") -UseDefaultCredentials -TimeoutSec 60 -MaximumRedirection 0 -OutFile $file
                if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $artifact.sha256 -or (Get-Item -LiteralPath $file).Length -ne $artifact.length){throw 'Exported master bytes differ from the accepted immutable manifest.'}
            }
            [IO.File]::WriteAllText((Join-Path $directory ($production.ToString()+'.vtt')),$master.vtt)
            [IO.File]::WriteAllText((Join-Path $directory ($production.ToString()+'.srt')),$master.srt)
            $results += [ordered]@{package=$package;watch=$master;aiRuns=$master.production.snapshot.script.productionRuns}
        }
    }
    [ordered]@{stage=$Stage;scope=@{tenant=$tenant;universe=$universe};ownerLimit=$MaxGenerationCost;currency=$Currency;teachingConnections=$selectedTeachings;results=$results;publicPosts=0;qualification='Private staged outputs; no automatic human or release acceptance'}|ConvertTo-Json -Depth 100|Set-Content -Encoding UTF8 (Join-Path $directory ($Stage+'.json'))
    Write-Host "Private $Stage results saved in $directory. Existing workers process admitted work. Review scripts, voices, masters and M18 packages in the existing staff workbenches before the next stage. Rerun with the ORIGINAL operation IDs."
}finally{$env:GEETA_SQL_TESTS=$savedSql;Pop-Location}
