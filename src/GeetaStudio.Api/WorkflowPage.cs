namespace GeetaStudio.Api;

public static class WorkflowPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><title>Workflows</title>
<style>body{font:16px system-ui;max-width:1050px;margin:2rem auto;padding:1rem}input,textarea,button,select{font:inherit;margin:.3rem}textarea{width:95%;height:180px}pre{white-space:pre-wrap;overflow-wrap:anywhere}label{display:block}</style>
<h1>Draft workflows and human review</h1><p>Local fixtures produce generated drafts. Approval completes this workflow; it does not publish or approve canonical scripture.</p>
<label>Tenant ID <input id="tenant"></label><label>Universe ID <input id="universe"></label>
<button id="load">Load prompts, definitions and runs</button>
<h2>Start a draft</h2><p>Choose a registered definition and provide its declared inputs. Evidence rights are checked again before generation and review.</p>
<label>Definition ID <input id="definition"></label><label>Inputs <textarea id="inputs">{"situation":"An original calm conversation."}</textarea></label>
<button id="start">Start workflow</button>
<h2>Review and controls</h2><label>Run ID <input id="run"></label><button id="inspect">Inspect checkpoints</button>
<label>Evidence reference <input id="evidence" value="manual:local-review"></label>
<button data-action="review">Approve draft</button><button id="reject">Reject draft</button><button data-action="pause">Pause at boundary</button><button data-action="resume">Resume boundary / reconciled draft</button><button data-action="cancel">Cancel</button>
<p>Read the draft, evidence and check result before recording approval. Unknown provider usage requires authoritative reconciliation in AI tasks. All requests use Windows staff identity; mutations require administrator membership.</p>
<h2>Registration</h2><label>Artifact <select id="kind"><option value="prompts">Prompt revision</option><option value="definitions">Workflow definition</option></select></label>
<label>Registration JSON <textarea id="registration">{}</textarea></label><button id="register">Register immutable revision</button>
<pre id="out" aria-live="polite"></pre>
<script>
let current=null;const val=id=>document.getElementById(id).value.trim(),show=x=>document.getElementById('out').textContent=JSON.stringify(x,null,2);
const base=()=>'/api/v1/workflows/tenants/'+encodeURIComponent(val('tenant'));
async function api(path,body){const r=await fetch(base()+path,{method:body?'POST':'GET',headers:body?{'Content-Type':'application/json','X-ContentOS-Staff':'1'}:{},body:body?JSON.stringify(body):undefined});const text=await r.text();let x;try{x=JSON.parse(text)}catch{x={message:text}}if(!r.ok)throw new Error(JSON.stringify(x));return x}
async function safe(work){try{await work()}catch(e){show({error:e.message})}}
document.getElementById('load').onclick=()=>safe(async()=>show({prompts:await api('/universes/'+val('universe')+'/prompts'),definitions:await api('/universes/'+val('universe')+'/definitions'),runs:await api('/runs')}));
document.getElementById('start').onclick=()=>safe(async()=>{current=await api('/universes/'+val('universe')+'/runs',{operationId:crypto.randomUUID(),definitionId:val('definition'),inputs:JSON.parse(val('inputs'))});document.getElementById('run').value=current.id;show(current)});
document.getElementById('inspect').onclick=()=>safe(async()=>{current=await api('/runs/'+val('run'));show(current)});
async function control(action,approve=true){current=await api('/runs/'+val('run'));current=await api('/runs/'+val('run')+'/'+action,{operationId:crypto.randomUUID(),expectedRowVersion:current.rowVersion,evidenceReference:val('evidence'),approve});show(current)}
document.querySelectorAll('[data-action]').forEach(b=>b.onclick=()=>safe(()=>control(b.dataset.action)));
document.getElementById('reject').onclick=()=>safe(()=>control('review',false));
document.getElementById('register').onclick=()=>safe(async()=>show(await api('/universes/'+val('universe')+'/'+val('kind'),JSON.parse(val('registration')))));
</script></html>
""";
}
