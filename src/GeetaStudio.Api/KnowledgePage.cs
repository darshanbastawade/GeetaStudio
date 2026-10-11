namespace GeetaStudio.Api;

public static class KnowledgePage { public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Knowledge retrieval</title>
<style>body{font:17px system-ui;max-width:1000px;margin:2rem auto;padding:0 1rem}label{display:block;margin:1rem 0}input,textarea{width:95%}article{border:1px solid #ccc;padding:1rem;margin:1rem 0}pre{white-space:pre-wrap}button{padding:.5rem}</style>
<h1>Knowledge and evidence search</h1><p>Results are source evidence, not a generated answer. Reviewed concept aliases help matching. An unrelated question can have no supported match.</p>
<label>Tenant <select id="tenant"></select></label><label>Universe <select id="universe"></select></label><button id="refresh">Refresh</button>
<label>Active index <select id="alias"></select></label><label>Question <input id="query"></label><button id="search">Find evidence</button><p id="status" role="status"></p><div id="results"></div>
<h2>Indexes and review</h2><pre id="indices"></pre><h2>Concept graph</h2><pre id="graph"></pre>
<details><summary>Administrator operations</summary><p>Use the documented forms to queue an index, validate labeled questions and activate or roll back a reviewed index. A different embedding model needs a separate index.</p>
<label>Operation <select id="operation"><option>indices</option><option>aliases</option><option>concepts</option><option>relations</option></select></label><label>Form <textarea id="body" rows="8"></textarea></label><button id="submit">Submit</button>
<label>Review or validation path, such as indices/ID/validate <input id="path"></label><button id="review">Submit review / validation</button></details>
<script>
const $=id=>document.getElementById(id);let indices=[],aliases=[];const base=()=>'/api/v1/knowledge/tenants/'+$('tenant').value+'/universes/'+$('universe').value;
async function request(path,body){const r=await fetch(path,{credentials:'same-origin',method:body?'POST':'GET',headers:body?{'Content-Type':'application/json','X-ContentOS-Staff':'1'}:{},body:body?JSON.stringify(body):undefined});const t=await r.text();if(!r.ok)throw Error(t);return t?JSON.parse(t):null;}
async function run(action){try{$('status').textContent='';await action();}catch(e){$('status').textContent=e.message;}}
function options(id,rows,label){$(id).replaceChildren();for(const r of rows){const o=document.createElement('option');o.value=r.id;o.textContent=label(r);$(id).append(o);}}
async function load(){indices=await request(base()+'/indices');aliases=await request(base()+'/aliases');$('indices').textContent=JSON.stringify(indices,null,2);$('graph').textContent=JSON.stringify(await request(base()+'/graph'),null,2);options('alias',aliases,a=>a.key+' · '+a.locale);}
async function universes(){const rows=await request('/api/v1/core/tenants/'+$('tenant').value+'/universes');options('universe',rows.filter(r=>r.tenantId===$('tenant').value),r=>r.name);await load();}
$('tenant').onchange=()=>run(universes);$('universe').onchange=()=>run(load);$('refresh').onclick=()=>run(load);
$('search').onclick=()=>run(async()=>{const alias=aliases.find(a=>a.id===$('alias').value);const index=indices.find(i=>i.id===alias?.activeIndexId);if(!index)throw Error('Select a validated active index first.');
const result=await request(base()+'/search',{alias:alias.key,query:$('query').value,locale:alias.locale,territory:'IN',embeddingSpaceId:index.embeddingSpaceId,limit:5});$('status').textContent=result.outcome;$('results').replaceChildren();
for(const h of result.evidence){const a=document.createElement('article');const title=document.createElement('h3');title.textContent=h.label+' · '+h.edition+' · '+h.chapter+'.'+h.verse;a.append(title);const p=document.createElement('p');p.textContent=h.exactText;a.append(p);const cite=document.createElement('p');cite.textContent=h.citation.citation+' · '+h.citation.requiredAttribution;a.append(cite);$('results').append(a);}});
$('submit').onclick=()=>run(async()=>{const body=JSON.parse($('body').value);body.operationId=crypto.randomUUID();await request(base()+'/'+$('operation').value,body);await load();});
$('review').onclick=()=>run(async()=>{const path=$('path').value;if(!/^(indices|graph)\/[a-f0-9-]+\/(validate|review)$/.test(path))throw Error('Use an index validation or graph review path.');await request(base()+'/'+path,JSON.parse($('body').value));await load();});
run(async()=>{options('tenant',await request('/api/v1/core/tenants'),r=>r.name);await universes();});
</script></html>
"""; }
