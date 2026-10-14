namespace GeetaStudio.Api;

internal static class AnalyticsPage
{
    internal const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Performance and revenue</title>
<style>body{font:17px system-ui;max-width:1100px;margin:auto;padding:24px;background:#f8f6ef;color:#272623}label{display:inline-block;margin:8px}input,select,button,textarea{font:inherit;padding:8px;max-width:100%;box-sizing:border-box}button{cursor:pointer}textarea{width:100%;min-height:150px}section{background:white;padding:18px;margin:18px 0;border:1px solid #aaa;border-radius:8px}table{width:100%;border-collapse:collapse;overflow-wrap:anywhere}th,td{text-align:left;padding:8px;border-bottom:1px solid #ccc}pre{white-space:pre-wrap;overflow-wrap:anywhere}summary{cursor:pointer;padding:8px}#status{min-height:24px}</style>
<h1>Performance and revenue</h1><p>Private operational reports. Revenue type and acquisition channel are separate. Missing data is visible. Forecasts are scenarios, never guaranteed earnings.</p>
<label>Tenant <input id="tenant"></label><label>Universe <input id="universe"></label><label>Currency <input id="currency" value="INR" size="4"></label>
<label>From <input id="from" type="date"></label><label>Through <input id="through" type="date"></label><button id="load">View report</button><p id="status" role="status" aria-live="polite"></p>
<section><h2>Reconciliation</h2><div id="totals"></div><h3>Revenue types, acquisition and costs</h3><div id="groups"></div><h3>Coverage and missing data</h3><div id="gaps"></div></section>
<section><h2>Content variants</h2><p>Character/story metadata describes production, not individual viewers. Approval and corrections are shown separately.</p><div id="variants"></div><h3>Cost per approved episode</h3><div id="episodes"></div></section>
<section><h2>Engagement definitions and coverage</h2><div id="metrics"></div></section>
<section><h2>Consented cohorts</h2><p>Groups below five people are suppressed. Revoked or expired consent is excluded. Learning feedback is observed feedback, not proof of learning.</p><div id="cohorts"></div><h3>Cost per consented session</h3><div id="sessions"></div></section>
<section><h2>Scenario forecasts</h2><p>Every saved edit creates a new version. Audience, conversion, price, retention, payout eligibility, costs and uncertainty are inspectable.</p><div id="forecasts"></div></section>
<section><h2>Evidence and imports</h2><p>Administrator access is required to change financial data. Use a bounded validated import with original economic identifiers and provenance. Never paste credentials or personal details.</p>
<label>Action <select id="action"><option value="imports">Import events</option><option value="sources">Register source version</option><option value="definitions">Register metric definition</option><option value="rates">Record FX evidence</option><option value="forecasts">Save forecast version</option><option value="consents">Record consent</option></select></label>
<label>Load evidence file <input id="file" type="file" accept=".json,application/json"></label><label for="evidence">Structured evidence</label><textarea id="evidence" spellcheck="false"></textarea><button id="submit">Validate and save</button><button id="customerCapture">Capture test customer payments</button><button id="capture">Capture owned production costs</button><button id="inventory">View source, definition and FX versions</button><div id="inventoryResult"></div></section>
<script>
const $=id=>document.getElementById(id);
const now=new Date();$('from').value=String(now.getUTCFullYear())+'-01-01';$('through').value=now.toISOString().slice(0,10);
function base(){return '/api/v1/analytics/tenants/'+encodeURIComponent($('tenant').value)+'/universes/'+encodeURIComponent($('universe').value)}
async function request(path,body){const r=await fetch(base()+path,body===undefined?{}:{method:'POST',headers:{'Content-Type':'application/json','X-ContentOS-Staff':'1'},body:JSON.stringify(body)});if(!r.ok)throw Error((await r.text()).slice(0,600));return r.status===204?null:r.json()}
function table(id,headers,rows){const root=$(id);root.replaceChildren();const t=document.createElement('table');const tr=document.createElement('tr');for(const h of headers){const th=document.createElement('th');th.textContent=h;tr.append(th)}t.append(tr);for(const row of rows){const r=document.createElement('tr');for(const v of row){const td=document.createElement('td');td.textContent=v===null||v===undefined?'Unavailable':String(v);r.append(td)}t.append(r)}root.append(t);if(!rows.length){const p=document.createElement('p');p.textContent='No recorded data.';root.append(p)}}
function details(id,data){const root=$(id);root.replaceChildren();for(const x of data){const d=document.createElement('details'),s=document.createElement('summary'),p=document.createElement('pre');s.textContent=(x.id||x.key||'Evidence')+' · '+(x.content?.locale||x.actualCoverage||'Inspect recorded version');p.textContent=JSON.stringify(x,null,2);d.append(s,p);root.append(d)}}
async function run(fn){$('status').textContent='Working…';try{await fn();$('status').textContent='Ready. Recorded evidence only; release gates remain separate.'}catch(e){$('status').textContent=e.message}}
async function load(){const end=new Date($('through').value+'T00:00:00Z');end.setUTCDate(end.getUTCDate()+1);const p=new URLSearchParams({currency:$('currency').value,startsUtc:$('from').value+'T00:00:00Z',endsUtc:end.toISOString()});const r=await request('/report?'+p);
$('totals').textContent=r.currency+' · Recognized net '+r.recognizedNet+' · Estimated net '+r.estimatedNet+' · Actual costs '+r.costs+' · Settled '+r.settled+' · Difference '+r.unsettledDifference;
table('groups',['Dimension','Value','Recognized net','Estimated net','Cost','Settled'],r.groups.map(x=>[x.dimension,x.key,x.recognizedNet,x.estimatedNet,x.costs,x.settled]));
$('gaps').textContent=r.gaps.join(' ')+' Missing FX: '+r.excludedMissingFx+' · Excluded activities: '+r.suppressedOrRevokedActivities+' · Correction rate: '+(r.correctionRate??'Unavailable')+' · Unresolved costs: '+r.unresolvedOwnedCosts.length;
details('variants',r.variants);details('episodes',r.episodeCosts);table('metrics',['Metric/version','Unit / aggregation','Source/account','Value','Coverage'],r.metrics.map(x=>[x.key+'/'+x.version,x.unit+' / '+x.aggregation,x.sourceSystem+' / '+x.accountReference,x.value,x.coverage]));
table('cohorts',['Cohort','Subjects','Conversion','30-day retention','Learning feedback','Coverage'],r.cohorts.map(x=>[x.cohort,x.consentedSubjects,x.conversion,x.retention,x.learningMean,x.coverage]));table('sessions',['Group','Sessions','Cost per session','Coverage'],r.sessionCosts.map(x=>[x.cohort,x.sessions,x.costPerSession,x.coverage]));details('forecasts',await request('/forecasts'))}
$('load').onclick=()=>run(load);$('submit').onclick=()=>run(async()=>{const b=JSON.parse($('evidence').value);if(!b.operationId)b.operationId=crypto.randomUUID();await request('/'+$('action').value,b);await load()});
$('capture').onclick=()=>run(async()=>{await request('/capture',{operationId:crypto.randomUUID()});await load()});
$('inventory').onclick=()=>run(async()=>{details('inventoryResult',[{sources:await request('/sources'),definitions:await request('/definitions'),fx:await request('/rates'),providers:await request('/providers')}])});
$('file').onchange=()=>run(async()=>{const f=$('file').files[0];if(!f)return;if(f.size>256000)throw Error('Evidence file is too large.');$('evidence').value=await f.text()});
</script></html>
""";
}
