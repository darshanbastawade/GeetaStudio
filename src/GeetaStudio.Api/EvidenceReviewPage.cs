namespace GeetaStudio.Api;

public static class EvidenceReviewPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Evidence review</title><style>body{font:17px system-ui;max-width:1000px;margin:2rem auto;padding:0 1rem}label{display:block;margin:1rem 0}textarea{width:100%;min-height:8rem}article{border:1px solid #ccc;padding:1rem;margin:1rem 0}pre{white-space:pre-wrap}button{padding:.5rem;margin:.3rem}</style>
<h1>Evidence and interpretation review</h1><p>Source readings, attributed viewpoints and fictional scenes stay separate. Model agreement does not establish truth. A practical suggestion is not a prediction by scripture.</p>
<label>Tenant <select id="tenant"></select></label><label>Universe <select id="universe"></select></label><button id="refresh">Refresh</button><p id="status" role="status"></p>
<details><summary>Submit a claim or new version</summary><p>Use the documented claim form with actual snapshot spans and a registered rubric. Source positions count UTF-16 characters; a citation alone is not evidence.</p><textarea id="submission" aria-label="Claim submission"></textarea><button id="submit">Submit pending claim</button></details>
<details><summary>Reviewer assessment</summary><label>Revision ID <input id="revision"></label><label>Row version <input id="version"></label>
<label>Evidence quality <select id="quality"><option>Insufficient</option><option>DirectSource</option><option>Attributed</option><option>Corroborated</option></select></label>
<label>Finding <select id="outcome"><option>InsufficientEvidence</option><option>Supported</option><option>Contested</option></select></label>
<label>Review evidence reference <input id="evidence"></label><button id="approve">Approve</button><button id="reject">Reject</button>
<label>Correction reason <input id="reason"></label><button id="correct">Request correction</button><button id="impact">Show affected drafts</button></details>
<h2>Review criteria</h2><div id="rubrics"></div><h2>Claims and history</h2><div id="claims"></div><h2>Retained disagreements</h2><div id="disagreements"></div><pre id="detail"></pre>
<script>
const $=id=>document.getElementById(id);const base=()=>'/api/v1/evidence/tenants/'+$('tenant').value+'/universes/'+$('universe').value;
async function request(path,body){const r=await fetch(path,{credentials:'same-origin',method:body?'POST':'GET',headers:body?{'Content-Type':'application/json','X-ContentOS-Staff':'1'}:{},body:body?JSON.stringify(body):undefined});const text=await r.text();if(!r.ok)throw Error(text);return text?JSON.parse(text):null;}
async function run(action){try{$('status').textContent='';await action();}catch(e){$('status').textContent=e.message;}}
function options(id,rows,label){$(id).replaceChildren();for(const row of rows){const o=document.createElement('option');o.value=row.id;o.textContent=label(row);$(id).append(o);}}
async function load(){$('rubrics').replaceChildren();for(const r of await request(base()+'/rubrics')){const p=document.createElement('p');p.textContent=r.key+' · v'+r.version+' · '+r.id+' · '+r.description+' · minimum independent sources '+r.minimumIndependentSources;$('rubrics').append(p);}$('claims').replaceChildren();for(const c of await request(base()+'/')){const a=document.createElement('article');const h=document.createElement('h3');h.textContent=c.key+' · current version '+c.currentVersion;a.append(h);
for(const r of c.revisions){const p=document.createElement('p');p.textContent=r.label+' · v'+r.version+' · '+r.reviewStatus+' · '+r.uncertainty+(r.version!==c.currentVersion?' · superseded':'');a.append(p);const text=document.createElement('pre');text.textContent=r.statement;a.append(text);
const credit=document.createElement('p');credit.textContent='Attribution: '+(r.author||'source reading')+' · '+r.tradition+' · '+r.quality+'/'+r.outcome+' · reviewer '+(r.reviewer||'pending');a.append(credit);
const button=document.createElement('button');button.textContent='Inspect / review';button.onclick=()=>{$('revision').value=r.id;$('version').value=r.rowVersion;$('detail').textContent=JSON.stringify(r,null,2);};a.append(button);}$('claims').append(a);}
$('disagreements').replaceChildren();for(const d of await request(base()+'/disagreements')){const p=document.createElement('p');p.textContent=d.leftRevisionId+' versus '+d.rightRevisionId+' · '+d.reason+' · '+d.evidenceReference;$('disagreements').append(p);}}
async function universes(){const rows=await request('/api/v1/core/tenants/'+$('tenant').value+'/universes');options('universe',rows.filter(r=>r.tenantId===$('tenant').value),r=>r.name);await load();}
$('tenant').onchange=()=>run(universes);$('universe').onchange=()=>run(load);$('refresh').onclick=()=>run(load);
$('submit').onclick=()=>run(async()=>{const body=JSON.parse($('submission').value);body.operationId=crypto.randomUUID();await request(base()+'/claims',body);await load();});
for(const [id,status] of [['approve','Approved'],['reject','Rejected']])$(id).onclick=()=>run(async()=>{await request(base()+'/revisions/'+$('revision').value+'/review',{expectedRowVersion:$('version').value,status,quality:$('quality').value,outcome:$('outcome').value,evidenceReference:$('evidence').value});await load();});
$('correct').onclick=()=>run(async()=>{await request(base()+'/corrections',{operationId:crypto.randomUUID(),claimRevisionId:$('revision').value,reason:$('reason').value,evidenceReference:$('evidence').value});$('detail').textContent=JSON.stringify(await request(base()+'/revisions/'+$('revision').value+'/impact'),null,2);});
$('impact').onclick=()=>run(async()=>{$('detail').textContent=JSON.stringify(await request(base()+'/revisions/'+$('revision').value+'/impact'),null,2);});
run(async()=>{options('tenant',await request('/api/v1/core/tenants'),r=>r.name);await universes();});
</script></html>
""";
}
