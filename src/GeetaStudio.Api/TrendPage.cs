namespace GeetaStudio.Api;

public static class TrendPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Trend evidence and story review</title>
<style>body{font:1rem system-ui;color:#243e35;max-width:70rem;margin:2rem auto;padding:1rem}input,textarea,button,select{font:inherit;box-sizing:border-box;padding:.7rem;max-width:100%;min-height:44px}label{display:block;margin:1rem 0}textarea{width:100%;min-height:12rem}pre{white-space:pre-wrap;overflow-wrap:anywhere}section{border:1px solid #8da99a;padding:1rem;margin:1rem 0}a{color:#245441}:focus-visible{outline:3px solid #8b4c0e}</style>
<h1>Trend evidence and original story review</h1><p>Discovery is a signal, not a verified fact. Check original reporting, permitted substance, dates and sources before choosing a teaching or spending on production.</p>
<p><a href="/staff/editorial">Editorial workbench</a> · <a href="/staff/characters">Characters and guests</a> · <a href="/staff/factory">Shared planning</a> · <a href="/staff/sources">Source permissions</a></p>
<label>Tenant ID <input id="tenant"></label><label>Universe ID <input id="universe"></label><button id="load">Load scoped review queue</button><button id="recheck">Recheck current evidence</button><p role="status" id="status"></p>
<section><h2>Coverage and provider versions</h2><pre id="connectors"></pre></section><section><h2>Facts, copied reports and freshness</h2><pre id="clusters"></pre></section>
<section><h2>Teaching matches and explicit no-match results</h2><pre id="teachings"></pre></section><section><h2>Audience packages, casts, scripts and costs</h2><pre id="stories"></pre></section><section><h2>Remediation and outcomes</h2><pre id="remediations"></pre><pre id="stats"></pre></section>
<section><h2>Submit a scoped review operation</h2><p>Use a stable operation ID and the current rowversion for review/control. Human reviews use manual: references. Fixtures cannot qualify public release. Register new guests in the character workbench.</p>
<label>Operation path <input id="path" value="manual"></label><label>Owned request JSON <textarea id="request" spellcheck="false">{}</textarea></label><button id="submit">Submit for review</button><pre id="result"></pre></section>
<script>
const get=id=>document.getElementById(id),base=()=>'/api/v1/trends/tenants/'+encodeURIComponent(get('tenant').value)+'/universes/'+encodeURIComponent(get('universe').value);
async function load(){try{for(const name of ['connectors','clusters','teachings','stories','remediations','stats']){const r=await fetch(base()+'/'+name,{credentials:'same-origin',cache:'no-store'});const text=await r.text();get(name).textContent=text;if(!r.ok)throw Error('Request denied or unavailable: '+r.status);}get('status').textContent='Current scoped evidence loaded.';}catch(e){get('status').textContent=e.message;}}
get('load').onclick=load;get('submit').onclick=async()=>{try{const route=get('path').value;if(!/^(manual|connectors|poll|teachings|stories|bind|rank|(?:connectors|clusters|teachings|stories)\/[0-9a-f-]+\/(?:review|change|disable))$/.test(route))throw Error('Choose a supported scoped operation.');const data=JSON.parse(get('request').value);const r=await fetch(base()+'/'+route,{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/json','X-ContentOS-Staff':'1'},body:JSON.stringify(data)});get('result').textContent=await r.text();get('status').textContent='Operation returned '+r.status;if(r.ok)await load();}catch(e){get('status').textContent=e.message;}};
get('recheck').onclick=async()=>{const r=await fetch(base()+'/recheck',{method:'POST',credentials:'same-origin',headers:{'X-ContentOS-Staff':'1'}});get('status').textContent='Recheck returned '+r.status;if(r.ok)await load();};
</script></html>
""";
}
