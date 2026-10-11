namespace GeetaStudio.Api;

public static class ReadAdminPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>READ release review</title>
<style>body{font:1rem system-ui;color:#243e35;max-width:60rem;margin:2rem auto;padding:1rem}label{display:block;margin:1rem 0}input,textarea,select,button{font:inherit;padding:.5rem;max-width:100%;box-sizing:border-box}pre{white-space:pre-wrap;overflow-wrap:anywhere}button{background:#245441;color:white;border:0;padding:.8rem}a{color:#245441}:focus-visible{outline:3px solid #8b4c0e}</style>
<h1>READ collection release review</h1><p>Review exact publication bindings and gate evidence. Fixture collections stay private; genuine human qualification and current release checks are required for rollout.</p>
<p><a href="/staff/read">Canonical corpus</a> · <a href="/staff/editorial">Editorial approval</a> · <a href="/staff/lessons">Audience and language variants</a> · <a href="/staff/evaluation">Evaluation</a></p>
<label>Tenant ID <input id="tenant"></label><label>Universe ID <input id="universe"></label><label>Collection ID <input id="collection"></label><button id="inspect">Inspect collection</button>
<pre id="status" role="status" aria-live="polite"></pre>
<label>Operation <select id="kind"><option value="collections">Create collection</option><option value="pieces">Bind reviewed piece</option><option value="evidence">Record release evidence</option><option value="control">Activate, disable or rollback</option></select></label>
<label>Request <textarea id="request" rows="12" cols="80">{"operationId":"","kind":"PrivacyChild","mode":"Fixture","reference":"fixture:review-pending","notes":"Human privacy and child policy review pending.","expiresUtc":""}</textarea></label><button id="submit">Submit staff action</button>
<script>
const el=id=>document.getElementById(id);let version='';
const base=()=>'/api/v1/read-admin/tenants/'+encodeURIComponent(el('tenant').value)+'/universes/'+encodeURIComponent(el('universe').value);
el('inspect').onclick=async()=>{try{const r=await fetch(base()+'/collections/'+encodeURIComponent(el('collection').value));if(!r.ok)throw Error('Request failed: '+r.status);const d=await r.json();version=d.rowVersion;el('status').textContent=JSON.stringify(d,null,2);}catch(e){el('status').textContent=e.message;}};
el('submit').onclick=async()=>{try{const data=JSON.parse(el('request').value);data.operationId=data.operationId||crypto.randomUUID();if(el('kind').value==='control')data.expectedRowVersion=data.expectedRowVersion||version;const path=['evidence','control'].includes(el('kind').value)?'/collections/'+encodeURIComponent(el('collection').value)+'/'+el('kind').value:'/'+el('kind').value;const r=await fetch(base()+path,{method:'POST',headers:{'Content-Type':'application/json','X-ContentOS-Staff':'1'},body:JSON.stringify(data)});el('status').textContent=r.ok?'Staff action saved. Inspect again for current status.':await r.text();}catch(e){el('status').textContent=e.message;}};
</script></html>
""";
}
