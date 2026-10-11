namespace GeetaStudio.Api;

public static class LessonPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>Audience lessons and translation review</title>
<style>body{font:17px system-ui;max-width:1150px;margin:2rem auto;padding:1rem;color:#223}section{border:1px solid #ccd;padding:1rem;margin:1rem 0}label{display:block;margin:.6rem 0}input,select,textarea,button{font:inherit;padding:.4rem;max-width:100%}textarea{width:95%;min-height:5rem}.comparison{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:1rem}article{border:1px solid #ccc;padding:1rem}pre{white-space:pre-wrap}button{margin:.3rem}h1,h2,h3{color:#345}</style>
<h1>Audience lessons and translation review</h1>
<p>Compare teaching treatments with a shared source and attributed interpretation. Examples are fictional. Languages, profiles and variants require human review before release. Sanskrit stays unchanged. No child conversations or advertising profiles are collected.</p>
<label>Tenant <select id="tenant"></select></label><label>Universe <select id="universe"></select></label>
<button id="load">Load lessons and rules</button><p id="status" role="status" aria-live="polite"></p>
<section><h2>Audience, writing and language rules</h2><button id="defaults">Propose initial rules</button><p>8–14, 15–23 and adult audiences; 15–18 and 19–23 writing subprofiles; English, Marathi and Hindi locale proposals. These are drafts, not fixed launch decisions.</p><div id="profiles"></div>
<details><summary>Edit a rule as a new version</summary><p>Choose an existing proposal to edit. The saved version will need its own review. Writing ages are editorial bands, not legal or consent ages.</p>
<label>Rules to edit <select id="proposalBase"></select></label><label>Rule key <input id="proposalKey"></label><label>New version <input id="proposalVersion" type="number" min="1"></label>
<div id="ruleFields"></div><button id="register">Save new proposal for review</button></details></section>
<section><h2>Shared evidence bundle</h2><label>Approved exact source claim <select id="anchor"></select></label>
<label>Attributed interpretation claim <select id="meaning"></select></label><label>Sanskrit text ID (optional; same verse) <input id="sanskrit"></label>
<label>Learning-goal identifiers (comma separated) <input id="goals" value="understand-context"></label>
<label>Names to preserve (comma separated) <input id="names"></label><label>Concept identifiers (comma separated) <input id="concepts" value="context"></label>
<button id="bundleCreate">Create evidence bundle</button></section>
<section><h2>Create an audience and language variant</h2><label>Evidence bundle <select id="bundle"></select></label>
<label>Audience <select id="audience"></select></label><label>Writing treatment <select id="writing"></select></label>
<label>Delivery language <select id="locale"></select></label><label>Translation route <select id="route"></select></label>
<label>Previous variant ID (optional, for a linked revision) <input id="parent"></label><button id="adapt">Create review draft</button><button id="cast">Show eligible characters</button><pre id="candidates"></pre></section>
<section><h2>Compare lessons</h2><div id="variants" class="comparison"></div></section>
<section><h2>Reviewed translation memory</h2>
<p>Choose an English source variant, propose the five adapted sections, and review meaning and terminology. Source anchor, names, concept identifiers and learning-goal identifiers remain pinned.</p>
<label>English source variant <select id="memorySource"></select></label><label>Target locale profile <select id="memoryLocale"></select></label>
<label>Situation <textarea id="mSituation"></textarea></label><label>Attributed interpretation <textarea id="mInterpretation"></textarea></label>
<label>Example <textarea id="mExample"></textarea></label><label>Practical action <textarea id="mAction"></textarea></label><label>Reflection <textarea id="mReflection"></textarea></label>
<label>Meaning and terminology review notes <textarea id="mNotes"></textarea></label><button id="memoryCreate">Save translation for review</button><div id="memories"></div></section>
<section><h2>Add an eligible fictional guest</h2><p>This keeps the guest's canonical life stage. It does not create media assets or authorize a likeness.</p>
<label>Key <input id="cKey"></label><label>Name <input id="cName"></label><label>Canonical life stage <input id="cStage"></label><label>Biography <textarea id="cBio"></textarea></label>
<label>Identity/suitability review reference <input id="cEvidence" placeholder="manual:review-reference"></label><button id="characterCreate">Register reviewed guest</button></section>
<script>
const $=id=>document.getElementById(id),op=()=>crypto.randomUUID(),parts=id=>$(id).value.split(',').map(x=>x.trim()).filter(Boolean);
let profiles=[],variants=[],memories=[],characters=[],editing=null;const base=()=>'/api/v1/lessons/tenants/'+$('tenant').value+'/universes/'+$('universe').value;
async function request(path,data){const r=await fetch(path,{credentials:'same-origin',headers:data?{'Content-Type':'application/json','X-ContentOS-Staff':'1'}:{},method:data?'POST':'GET',body:data?JSON.stringify(data):undefined});const text=await r.text();const value=text?JSON.parse(text):null;if(!r.ok)throw Error(value?.error||'Request failed');return value;}
function select(id,items,label){const old=$(id).value;$(id).replaceChildren();for(const x of items){const o=document.createElement('option');o.value=x.id;o.textContent=label(x);$(id).append(o);}if(items.some(x=>x.id===old))$(id).value=old;}
async function act(fn){try{$('status').textContent='Working…';await fn();$('status').textContent='Done.';}catch(e){$('status').textContent=e.message;}}
function reviewButton(kind,item){const b=document.createElement('button');b.textContent='Record review';b.onclick=()=>act(async()=>{const evidence=prompt('Human review reference (manual:) or explicit test-fixture reference (fixture:)');if(!evidence)return;const approve=confirm('Approve this reviewed item? Cancel records rejection.');await request(base()+'/'+kind+'/'+item.id+'/review',{operationId:op(),expectedRowVersion:item.rowVersion,approve,evidenceReference:evidence});await load();});return b;}
function para(parent,label,value){const p=document.createElement('p');const strong=document.createElement('strong');strong.textContent=label+': ';p.append(strong,document.createTextNode(value||'—'));parent.append(p);}
function label(key){return key.replace(/([A-Z])/g,' $1').replace(/^\w/,c=>c.toUpperCase());}
function readable(value){const p=profiles.find(p=>p.id===value||p.rules.audience?.audienceId===value);const c=characters.find(c=>c.characterId===value);return p?p.rules.key+' v'+p.rules.version:c?c.name:/^[0-9a-f]{8}-/.test(String(value))?'Registered identity':String(value);}
function describe(obj){return Object.entries(obj).map(([k,v])=>label(k)+': '+(Array.isArray(v)?v.map(x=>typeof x==='object'?describe(x):readable(x)).join('; '):v&&typeof v==='object'?'\n'+describe(v):readable(v))).join('\n');}
function editRules(){const p=profiles.find(p=>p.id===$('proposalBase').value);$('ruleFields').replaceChildren();if(!p){editing=null;return;}editing=structuredClone(p.rules);$('proposalKey').value=p.rules.key;$('proposalVersion').value=p.rules.version+1;const section=editing[editing.kind==='TranslationRoute'?'route':editing.kind.toLowerCase()];fields(section,$('ruleFields'));}
function fields(obj,parent){for(const [key,value] of Object.entries(obj)){if(value&&typeof value==='object'&&!Array.isArray(value)){const box=document.createElement('fieldset');const legend=document.createElement('legend');legend.textContent=label(key);box.append(legend);fields(value,box);parent.append(box);continue;}
if(Array.isArray(value)&&value.some(x=>typeof x==='object')){for(const item of value){const box=document.createElement('fieldset');fields(item,box);parent.append(box);}if(key==='glossary'){const add=document.createElement('button');add.textContent='Add glossary term';add.onclick=()=>{value.push({key:'',value:'',transliteration:'',pronunciation:''});$('ruleFields').replaceChildren();fields(editing.locale,$('ruleFields'));};parent.append(add);}continue;}
const row=document.createElement('label');row.append(document.createTextNode(label(key)+' '));let input;
if(key==='audienceId'||key==='audienceProfileId'||key==='eligibleCharacters'||key==='adapter'){input=document.createElement('select');let choices=[];
if(key==='eligibleCharacters'){input.multiple=true;choices=characters.map(c=>({id:c.characterId,name:c.name+' / '+c.lifeStage}));}
else if(key==='adapter')choices=[{id:'reviewed-memory.a.v1',name:'Reviewed memory A'},{id:'reviewed-memory.b.v1',name:'Reviewed memory B'}];
else choices=profiles.filter(p=>p.rules.kind==='Audience').map(p=>({id:key==='audienceId'?p.rules.audience.audienceId:p.id,name:p.rules.key+' v'+p.rules.version}));
for(const x of choices){const opt=document.createElement('option');opt.value=x.id;opt.textContent=x.name;opt.selected=Array.isArray(value)?value.includes(x.id):value===x.id;input.append(opt);}
input.onchange=()=>{obj[key]=input.multiple?[...input.selectedOptions].map(x=>x.value):input.value;};}
else {input=document.createElement(typeof value==='string'&&value.length>150?'textarea':'input');if(typeof value==='boolean'){input.type='checkbox';input.checked=value;input.onchange=()=>obj[key]=input.checked;}else{if(typeof value==='number')input.type='number';input.value=Array.isArray(value)?value.join(', '):value;input.onchange=()=>obj[key]=typeof value==='number'?Number(input.value):Array.isArray(value)?input.value.split(',').map(x=>x.trim()).filter(Boolean):input.value;}}
row.append(input);parent.append(row);}}
$('proposalBase').onchange=editRules;
$('defaults').onclick=()=>act(async()=>{await request(base()+'/proposals',{operationId:op()});await load();});
async function load(){[profiles,variants,memories,characters]=await Promise.all([request(base()+'/profiles'),request(base()+'/variants'),request(base()+'/memory'),request(base()+'/characters')]);select('proposalBase',profiles,p=>p.rules.key+' v'+p.rules.version);editRules();
$('profiles').replaceChildren();for(const p of profiles){const section=document.createElement('section');para(section,p.rules.key+' v'+p.rules.version,p.rules.kind+' / '+p.reviewStatus);const pre=document.createElement('pre');pre.textContent=describe(p.rules[p.rules.kind==='TranslationRoute'?'route':p.rules.kind.toLowerCase()]);section.append(pre);if(p.reviewStatus==='Draft')section.append(reviewButton('profiles',p));$('profiles').append(section);}
for(const [id,kind] of [['audience','Audience'],['writing','Writing'],['locale','Locale'],['route','TranslationRoute'],['memoryLocale','Locale']])select(id,profiles.filter(p=>p.rules.kind===kind&&p.reviewStatus==='Approved'),p=>p.rules.key+' v'+p.rules.version);
select('bundle',await request(base()+'/bundles'),b=>b.snapshot.locator+' / '+b.id);
const claims=await request('/api/v1/evidence/tenants/'+$('tenant').value+'/universes/'+$('universe').value+'/claims');const revisions=claims.flatMap(c=>c.revisions).filter(r=>r.reviewStatus==='Approved');
select('anchor',revisions.filter(r=>r.type==='Translation'||r.type==='Quotation'),r=>r.statement);select('meaning',revisions.filter(r=>r.type==='Interpretation'),r=>r.statement);
$('variants').replaceChildren();for(const v of variants){const a=document.createElement('article');para(a,v.snapshot.audience.rules.key+' / '+v.snapshot.locale.rules.locale.locale,v.reviewStatus+' · revision '+v.version);para(a,'ID',v.id);if(v.outputWithheld)para(a,'Availability','Withheld: evidence or rights changed');else{for(const [key,label] of [['situation','Situation — fiction'],['sourceAnchor','Exact source and attribution'],['interpretation','Attributed interpretation'],['example','Relatable example — fiction'],['practicalAction','Practical action'],['reflection','Reflection']])para(a,label,v.sections[key]);para(a,'Interpretation attribution',v.snapshot.bundle.interpretationAuthor+' / '+v.snapshot.bundle.tradition+' / '+v.snapshot.bundle.uncertainty);para(a,'Goals',v.sections.learningGoals.join(', '));para(a,'Cast',v.snapshot.cast.map(c=>c.name+' ('+c.lifeStage+', '+c.role+')').join(', '));}
if(v.reviewStatus==='Draft')a.append(reviewButton('variants',v));const b=document.createElement('button');b.textContent='Check reviewed export';b.onclick=()=>act(async()=>{await request(base()+'/variants/'+v.id+'/export');alert('Export gate passed. No publication performed.');});a.append(b);$('variants').append(a);}
select('memorySource',variants.filter(v=>!v.outputWithheld&&v.snapshot.locale.rules.locale.locale==='en-IN'),v=>v.snapshot.audience.rules.key+' / '+v.id);
$('memories').replaceChildren();for(const m of memories){const div=document.createElement('section');para(div,'Translation',m.reviewStatus+' / '+m.id);para(div,'Meaning review',m.entry.meaningReviewNotes);if(m.reviewStatus==='Draft')div.append(reviewButton('memory',m));$('memories').append(div);}}
$('tenant').onchange=()=>act(universes);async function universes(){select('universe',await request('/api/v1/core/tenants/'+$('tenant').value+'/universes'),u=>u.name);}
$('load').onclick=()=>act(load);$('register').onclick=()=>act(async()=>{if(!editing)throw Error('Choose a rule proposal to edit.');const r=structuredClone(editing);r.operationId=op();r.key=$('proposalKey').value;r.version=Number($('proposalVersion').value);await request(base()+'/profiles',r);await load();});
$('bundleCreate').onclick=()=>act(async()=>{await request(base()+'/bundles',{operationId:op(),anchorClaimRevisionId:$('anchor').value,interpretationClaimRevisionId:$('meaning').value,sanskritTextId:$('sanskrit').value||null,learningGoals:parts('goals'),names:parts('names'),concepts:parts('concepts')});await load();});
$('adapt').onclick=()=>act(async()=>{await request(base()+'/variants',{operationId:op(),bundleId:$('bundle').value,audienceProfileId:$('audience').value,writingProfileId:$('writing').value,localeProfileId:$('locale').value,translationRouteId:$('route').value,parentVariantId:$('parent').value||null});await load();});
$('cast').onclick=()=>act(async()=>{$('candidates').textContent=(await request(base()+'/writing/'+$('writing').value+'/candidates')).map(c=>c.name+' / '+c.lifeStage+' / '+c.role).join('\n');});
$('memoryCreate').onclick=()=>act(async()=>{const v=variants.find(v=>v.id===$('memorySource').value);if(!v)throw Error('Choose an eligible English variant.');const source=v.sections;const target={...source,situation:$('mSituation').value,interpretation:$('mInterpretation').value,example:$('mExample').value,practicalAction:$('mAction').value,reflection:$('mReflection').value};await request(base()+'/memory',{operationId:op(),bundleId:v.bundleId,localeProfileId:$('memoryLocale').value,source,target,meaningReviewNotes:$('mNotes').value});await load();});
$('characterCreate').onclick=()=>act(async()=>{await request(base()+'/characters',{operationId:op(),key:$('cKey').value,name:$('cName').value,lifeStage:$('cStage').value,biography:$('cBio').value,evidenceReference:$('cEvidence').value});await load();});
act(async()=>{select('tenant',await request('/api/v1/core/tenants'),t=>t.name);await universes();});
</script></html>
""";
}
