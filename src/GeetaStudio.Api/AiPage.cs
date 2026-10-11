namespace GeetaStudio.Api;

public static class AiPage
{
    public const string Html = """
<!doctype html><html lang="en"><head><meta charset="utf-8"><title>AI tasks and budgets</title>
<style>body{font:16px system-ui;max-width:1000px;margin:2rem auto;padding:1rem}input,select,textarea,button{font:inherit;margin:.3rem;padding:.5rem}textarea{width:96%;min-height:9rem}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#eee;padding:1rem}label{display:block}</style></head><body>
<h1>AI tasks and budgets</h1><p>Generated drafts require editorial review. Offline fixtures do not qualify a real provider.</p>
<label>Tenant <input id="tenant" size="40"></label><label>Universe <input id="universe" size="40"></label>
<button id="refresh">Refresh profiles, routes, budgets and runs</button><pre id="state"></pre>
<h2>Draft</h2><label>Task <select id="task"><option>draft</option><option>summarize</option><option>translate</option><option>evaluate</option></select></label>
<label>Original public fixture text <textarea id="prompt">Write an original calm conversation about making a thoughtful choice.</textarea></label>
<button id="generate">Generate with the active local route</button><button id="cancel">Cancel current request</button><pre id="result"></pre>
<h2>Administrator setup</h2><p>Use the local demo task for a complete offline example. For reviewed configuration, enter a relative operation and JSON. Credentials belong in private secret storage.</p>
<label>Operation <select id="operation"><option>profiles</option><option>routes</option><option>budgets</option><option>routes/ROUTE_ID/activate</option><option>profiles/PROFILE_ID/switch</option><option>runs/RUN_ID/recover</option><option>runs/RUN_ID/attempts/ATTEMPT_ID/reconcile</option></select></label>
<label>Relative path (replace IDs as needed) <input id="path" size="65" value="profiles"></label><textarea id="body">{}</textarea><button id="submit">Submit reviewed configuration</button>
<script>
const $=id=>document.getElementById(id);let controller;
function base(){return '/api/v1/ai/tenants/'+encodeURIComponent($('tenant').value);}
async function call(path,body,signal){const r=await fetch(base()+'/'+path,{method:body===undefined?'GET':'POST',credentials:'same-origin',headers:{'Content-Type':'application/json','X-ContentOS-Staff':'1'},body:body===undefined?undefined:JSON.stringify(body),signal});const text=await r.text();if(!r.ok)throw Error(r.status+': '+text);return text?JSON.parse(text):'Saved';}
async function action(f){try{$('result').textContent=JSON.stringify(await f(),null,2);}catch(e){$('result').textContent=e.message;}}
$('refresh').onclick=()=>action(async()=>{const data={};for(const key of ['profiles','routes','budgets','runs'])data[key]=await call(key);$('state').textContent=JSON.stringify(data,null,2);return 'Refreshed';});
$('generate').onclick=()=>action(async()=>{controller=new AbortController();return await call('universes/'+encodeURIComponent($('universe').value)+'/generate',{operationId:crypto.randomUUID(),task:$('task').value,prompt:$('prompt').value},controller.signal);});
$('cancel').onclick=()=>controller?.abort();
$('operation').onchange=()=>{$('path').value=$('operation').value;};
$('submit').onclick=()=>action(async()=>{const p=$('path').value;if(!/^[a-zA-Z0-9\/-]+$/.test(p))throw Error('Use a relative operation path');return await call(p,JSON.parse($('body').value));});
</script></body></html>
""";
}
