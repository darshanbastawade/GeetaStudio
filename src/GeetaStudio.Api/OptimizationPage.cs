namespace GeetaStudio.Api;

public static class OptimizationPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Learning and improvement</title><style>body{font:18px system-ui;max-width:980px;margin:2rem auto;padding:1rem}input,textarea,button{font:inherit;padding:.5rem;margin:.4rem}textarea{width:95%;height:14rem}pre{white-space:pre-wrap;overflow-wrap:anywhere}label{display:block}</style>
<h1>Learning and improvement</h1><p>Private fixture experiments. Measured results and hypotheses remain separate. Popularity, quiz scores and automated assessments cannot approve sensitive content.</p>
<label>Tenant <input id="tenant"></label><label>Universe <input id="universe"></label><button id="load">Load experiments and report</button>
<label>Choose a trial <select id="trial"><option value="">Load trials first</option></select></label><button id="activate">Activate private trial</button><button id="rollback">Roll back</button>
<label>Measurement <select id="metric"><option>CorrectionRate</option><option>EditorialRework</option><option>LearningFeedback</option><option>Retention</option><option>AudienceSatisfaction</option><option>Conversion</option><option>Cost</option><option>OperationalFailure</option></select></label>
<label>Value (rates from 0 to 1; cost in the trial currency)<input id="value" type="number" min="0" step="0.01" value="0"></label><label>Number of samples <input id="samples" type="number" min="1" value="2"></label><button id="measure">Record fixture measurement</button>
<label>Operator report <select id="reportKind"><option value="CostReport">Costs</option><option value="RightsAlert">Source and rights checks</option><option value="FailedJobTriage">Failed work</option><option value="ProviderReview">Provider health</option></select></label><button id="runReport">Record report</button>
<details><summary>Advanced experiment setup and evidence</summary>
<label>Experiment <input id="experiment"></label><label>Action <select id="action"><option value="experiments">Propose</option><option value="evaluation">Bind evaluation</option><option value="measurements">Record measurement</option><option value="control">Activate or roll back</option><option value="automation">Run low-risk report or schedule</option></select></label>
<label>Owned request JSON (examples in M27 local guide)<textarea id="request" spellcheck="false"></textarea></label><button id="submit">Submit reviewed request</button><pre id="raw"></pre></details><p role="status" id="status"></p><pre id="output"></pre>
<script>
const el=id=>document.getElementById(id);const base=()=>`/api/v1/optimization/tenants/${encodeURIComponent(el('tenant').value)}/universes/${encodeURIComponent(el('universe').value)}`;
async function call(path,body){const r=await fetch(base()+path,{credentials:'same-origin',headers:{'Content-Type':'application/json','X-ContentOS-Staff':'1'},...(body?{method:'POST',body:JSON.stringify(body)}:{})});const t=await r.text();if(!r.ok)throw Error(t);return JSON.parse(t)}
let trials=[];
function show(r){el('raw').textContent=JSON.stringify(r,null,2);el('output').textContent=r.measured?['Measured results',...r.measured,'','Hypotheses',...r.hypotheses,'','Review and recommendations',...r.recommendations,'','Human review gates',...r.humanGates,'','Missing evidence',...r.unavailable].join('\n'):r.state?`Trial ${r.proposal.key}, version ${r.proposal.version}: ${r.state}. Human publication gates remain in place.`:'Recorded. Inspect the advanced evidence for details.'}
async function load(){trials=await call('/experiments');const selected=el('trial').value;el('trial').replaceChildren();for(const t of trials){const o=document.createElement('option');o.value=t.id;o.textContent=`${t.proposal.key} — version ${t.proposal.version} — ${t.state}`;el('trial').appendChild(o)}if(trials.some(t=>t.id===selected))el('trial').value=selected;show(await call('/report'))}
async function act(fn){try{await fn();el('status').textContent='Recorded. Human publication gates remain in place.'}catch(e){el('status').textContent=e.message}}
el('load').onclick=()=>act(load);
for(const action of ['Activate','Rollback'])el(action.toLowerCase()).onclick=()=>act(async()=>{const t=trials.find(t=>t.id===el('trial').value);if(!t)throw Error('Load and select a trial first.');show(await call(`/experiments/${t.id}/control`,{operationId:crypto.randomUUID(),action,expectedRowVersion:t.rowVersion,evidenceReference:'fixture:staff-reviewed-control'}));await load()});
el('measure').onclick=()=>act(async()=>{const t=trials.find(t=>t.id===el('trial').value);if(!t)throw Error('Select a trial first.');show(await call(`/experiments/${t.id}/measurements`,{operationId:crypto.randomUUID(),metric:el('metric').value,value:Number(el('value').value),samples:Number(el('samples').value),origin:'Fixture',evidenceReference:'fixture:staff-recorded-measurement',observedUtc:new Date().toISOString()}));await load()});
el('runReport').onclick=()=>act(async()=>show(await call('/automation',{operationId:crypto.randomUUID(),kind:el('reportKind').value})));
el('submit').onclick=()=>act(async()=>{const a=el('action').value;const path=['experiments','automation'].includes(a)?'/'+a:'/experiments/'+encodeURIComponent(el('experiment').value)+'/'+a;show(await call(path,JSON.parse(el('request').value)))});
</script></html>
""";
}
