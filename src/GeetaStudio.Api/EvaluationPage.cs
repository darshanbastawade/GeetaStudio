namespace GeetaStudio.Api;

internal static class EvaluationPage
{
    internal const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Quality evaluations</title>
<style>body{font:16px system-ui;max-width:1100px;margin:2rem auto;padding:1rem}label{display:block;margin:.5rem 0}input,select,textarea,button{font:inherit;padding:.5rem}textarea{width:95%;height:12rem}table{border-collapse:collapse;width:100%}td,th{padding:.6rem;border:1px solid #bbb;text-align:left}pre{white-space:pre-wrap;overflow-wrap:anywhere}button{margin:.3rem}#status{padding:1rem;background:#eef}</style></head><body>
<h1>Quality evaluations</h1><p>Fixtures prove checks, not human quality or provider approval. Human labels, release reviews and advisory scores remain separate. Held-out scoring is one use; refresh new cases for another attempt.</p>
<label>Tenant <input id="tenant"></label><label>Universe <input id="universe"></label><button id="load">Load evaluations</button>
<p id="status" role="status">Choose your owned scope.</p>
<label>Dataset <select id="dataset"></select></label><label>Candidate <select id="candidate"></select></label>
<label>Split <select id="split"><option>Development</option><option>HeldOut</option></select></label><label>Development baseline <select id="baseline"><option value="">None</option></select></label>
<button id="run">Run local candidate</button><h2>Reports</h2><table><thead><tr><th>Version / split</th><th>Checks</th><th>Human / fixture / advisory</th><th>Release gates</th><th>Latency / cost</th></tr></thead><tbody id="reports"></tbody></table>
<h2>Case findings</h2><pre id="detail">Select a report.</pre>
<h2>Review a result</h2><label>Report <select id="reviewReport"></select></label><label>Case <select id="reviewCase"></select></label>
<label>Review type <select id="reviewMode"><option value="Fixture">Fixture check</option><option value="Human">Human review</option></select></label>
<label>Faithful meaning (1–5) <input id="meaning" type="number" min="1" max="5" value="4"></label>
<label>Usefulness (1–5) <input id="usefulness" type="number" min="1" max="5" value="4"></label>
<label>Readability (1–5) <input id="readability" type="number" min="1" max="5" value="4"></label>
<label>Age suitability (1–5) <input id="age" type="number" min="1" max="5" value="4"></label>
<label>Character appeal (optional,1–5) <input id="appeal" type="number" min="1" max="5"></label>
<label>Understanding (optional,1–5) <input id="understanding" type="number" min="1" max="5"></label>
<label>Decision <select id="accept"><option value="false">Needs work</option><option value="true">Accept</option></select></label>
<label>Evidence reference <input id="reviewEvidence" placeholder="fixture:check or manual:review-reference"></label>
<label>Rubric version <input id="rubric" value="rubric.v1"></label><label>Review notes <textarea id="reviewNotes"></textarea></label>
<button id="review">Record result review</button><p>A human reviewer must supply their own scores and evidence. Accepted cast feedback requires appeal and understanding at least4. Fixture scores never count as human acceptance.</p>
<h2>Dataset and configuration administration</h2><p>Paste an owned request. Human evidence must come from a real reviewer; fixture: references cannot count as human acceptance. Do not enter personal information from children. Review notes record age-appropriate aggregate feedback only.</p>
<label>Action <select id="action"><option value="datasets">Register dataset</option><option value="candidates">Register candidate</option><option value="reviews">Record human or fixture review</option><option value="advisories">Record advisory result</option></select></label>
<label>Request JSON <textarea id="request" spellcheck="false"></textarea></label><button id="save">Save request</button>
<script>
const el=id=>document.getElementById(id); let data={};
function base(){if(!el('tenant').value||!el('universe').value)throw Error('Choose a tenant and universe.');return '/api/v1/evaluation/tenants/'+encodeURIComponent(el('tenant').value)+'/universes/'+encodeURIComponent(el('universe').value);}
async function call(path,body){const r=await fetch(base()+path,{credentials:'same-origin',headers:body?{'Content-Type':'application/json','X-ContentOS-Staff':'1'}:{},method:body?'POST':'GET',body:body?JSON.stringify(body):undefined});if(!r.ok)throw Error((await r.json()).error||'Request failed');return r.status===204?null:r.json();}
function options(id,items,label,none=false){const s=el(id);s.replaceChildren();if(none){const o=document.createElement('option');o.value='';o.textContent='None';s.append(o);}for(const x of items){const o=document.createElement('option');o.value=x.id;o.textContent=label(x);s.append(o);}}
async function load(){const [datasets,candidates,reports]=await Promise.all([call('/datasets'),call('/candidates'),call('/reports')]);data={datasets,candidates,reports};options('dataset',datasets,x=>x.dataset.key+' v'+x.dataset.version+' — human cases '+x.humanReviewedCases);options('candidate',candidates,x=>x.candidate.key+' v'+x.candidate.version);options('baseline',reports.filter(x=>x.split==='Development'),x=>x.id,true);const tbody=el('reports');tbody.replaceChildren();for(const r of reports){const tr=document.createElement('tr');const q=r.qualification;for(const value of [r.configuration.configurationVersion+' / '+r.split,r.results.filter(x=>x.passed).length+'/'+r.results.length,q.humanAccepted+' / '+q.fixtureAccepted+' / '+q.advisoryCount,q.status+': '+q.blocks.join(', '),q.p95LatencyMs.toFixed(1)+' ms / '+q.cost+' INR']){const td=document.createElement('td');td.textContent=value;tr.append(td);}tr.onclick=()=>el('detail').textContent=JSON.stringify(r,null,2);tbody.append(tr);}options('reviewReport',reports,x=>x.candidateKey+' / '+x.split);reviewCases();el('status').textContent='Loaded. Held-out input is excluded from the dataset list.';}
async function action(f){try{el('status').textContent='Working…';await f();}catch(e){el('status').textContent=e.message;}}
function reviewCases(){const r=data.reports?.find(x=>x.id===el('reviewReport').value);options('reviewCase',(r?.results||[]).map(x=>({id:x.caseKey,case:x})),x=>x.id);if(r)el('detail').textContent=JSON.stringify(r,null,2);}
el('reviewReport').onchange=reviewCases;
el('review').onclick=()=>action(async()=>{const r=data.reports.find(x=>x.id===el('reviewReport').value);if(!r)throw Error('Choose a report.');const number=id=>Number(el(id).value);const optional=id=>el(id).value?number(id):null;await call('/reviews',{operationId:crypto.randomUUID(),datasetId:r.datasetId,caseKey:el('reviewCase').value,runId:r.id,mode:el('reviewMode').value,faithfulMeaning:number('meaning'),usefulness:number('usefulness'),readability:number('readability'),ageSuitability:number('age'),characterAppeal:optional('appeal'),understanding:optional('understanding'),accept:el('accept').value==='true',evidenceReference:el('reviewEvidence').value,rubricVersion:el('rubric').value,notes:el('reviewNotes').value});await load();});
el('load').onclick=()=>action(load);
el('run').onclick=()=>action(async()=>{const r=await call('/runs',{operationId:crypto.randomUUID(),datasetId:el('dataset').value,candidateId:el('candidate').value,split:el('split').value,baselineRunId:el('baseline').value||null});await load();el('detail').textContent=JSON.stringify(r,null,2);});
el('save').onclick=()=>action(async()=>{await call('/'+el('action').value,JSON.parse(el('request').value));await load();});
</script></body></html>
""";
}
