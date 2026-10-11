namespace GeetaStudio.Api;

internal static class CorpusReaderPage
{
    public const string Html = """
<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Geeta reader · Geeta Studio</title><style>body{font:18px system-ui;max-width:1000px;margin:40px auto;padding:0 20px;background:#faf9f5;color:#20312a}label{display:inline-block;margin:12px}select,button{font:inherit;padding:8px}article{border:1px solid #b5c1b9;border-radius:8px;padding:24px;margin:24px 0}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}small{display:block;font-size:14px}section{padding:12px;border-bottom:1px solid #ccc}.quotation{font-size:24px}button{background:#254f3c;color:white;border:0;cursor:pointer}#status{white-space:pre-wrap}</style>
<h1>Geeta reader</h1><p>Staff-only source reader. Sanskrit quotation, published translation, and interpretation are separate. Source review confirms the imported transcription and locators; it does not certify an interpretation. Missing material is shown in the corpus report. No AI service is needed.</p>
<label>Tenant <select id="tenant"></select></label><label>Universe <select id="universe"></select></label><label>Edition <select id="manifest"></select></label><label>Chapter <select id="chapter"></select></label><button id="read">Read chapter</button>
<p id="status" role="status"></p><p id="summary"></p><details><summary>Corpus report: every missing, variant and restricted locator</summary><pre id="report"></pre></details><div id="verses"></div>
<script>
const $=id=>document.getElementById(id);const base=()=>'/api/v1/corpus/tenants/'+$('tenant').value+'/universes/'+$('universe').value;
async function get(path){const r=await fetch(path,{credentials:'same-origin'});const text=await r.text();if(!r.ok)throw new Error(text);return JSON.parse(text);}
function options(id,rows,label){$(id).replaceChildren();for(const row of rows){const o=document.createElement('option');o.value=row.id;o.textContent=label(row);$(id).append(o);}}
async function run(fn){try{$('status').textContent='Loading…';await fn();$('status').textContent='Source material loaded.';}catch(e){$('status').textContent=e.message;}}
async function read(){if(!$('manifest').value){$('summary').textContent='No imported corpus. Run the M04 sample setup or import an approved edition.';$('verses').replaceChildren();return;}
const prefix=base()+'/manifests/'+$('manifest').value;
const report=await get(prefix+'/report?territory=IN');$('summary').textContent=report.manifest.edition+' · baseline '+report.manifest.baselineTarget+' · edition inventory '+report.manifest.expectedCount+' · imported '+report.imported+' · approved readable '+report.approvedReadable+' · missing '+report.missing+' · restricted '+report.restricted;
$('report').textContent=JSON.stringify(report,null,2);$('verses').replaceChildren();
for(const verse of await get(prefix+'/chapters/'+$('chapter').value+'?territory=IN')){
const article=document.createElement('article');const title=document.createElement('h2');title.textContent='Chapter '+verse.chapter+', verse '+verse.verse+(verse.variantKey?' · '+verse.variantKey:'')+' · '+verse.reviewStatus;article.append(title);
for(const text of verse.texts){const section=document.createElement('section');const heading=document.createElement('h3');heading.textContent=text.kind==='DirectQuotation'?'Sanskrit — direct quotation':text.kind+' — '+text.locale;section.append(heading);const value=document.createElement('pre');value.textContent=text.exactText||'Restricted: '+text.availability;if(text.kind==='DirectQuotation'){value.className='quotation';value.lang='sa';}else value.lang=text.locale.split('-')[0];section.append(value);
const source=document.createElement('small');source.textContent='Availability: '+text.availability+' · '+text.sourceLocator+' · SHA-256 '+text.sha256;section.append(source);
if(text.citation){const credit=document.createElement('small');credit.textContent=text.citation.citation+' · '+text.citation.requiredAttribution+' · evidence '+text.citation.evidenceReference;section.append(credit);}article.append(section);}
for(const kind of ['Transliteration','WordMeaning'])if(!verse.texts.some(t=>t.kind===kind)){const missing=document.createElement('p');missing.textContent=kind+': not supplied by this sample.';article.append(missing);}
for(const annotation of verse.annotations){const section=document.createElement('section');section.textContent=annotation.kind+' · '+annotation.reviewStatus+': '+(annotation.text||'Not approved/readable');article.append(section);}
if(!verse.annotations.length){const note=document.createElement('p');note.textContent='Concepts/explanations: no approved records. No generated paraphrase supplied.';article.append(note);}
$('verses').append(article);}}
async function manifests(){const rows=await get(base()+'/');options('manifest',rows,r=>r.edition+' · v'+r.version);await read();}
async function universes(){const rows=await get('/api/v1/core/tenants/'+$('tenant').value+'/universes');options('universe',rows.filter(r=>r.tenantId===$('tenant').value),r=>r.name);const geeta=rows.find(r=>r.key==='geeta'&&r.tenantId===$('tenant').value);if(geeta)$('universe').value=geeta.id;await manifests();}
options('chapter',Array.from({length:18},(_,i)=>({id:i+1})),r=>'Chapter '+r.id);
$('tenant').onchange=()=>run(universes);$('universe').onchange=()=>run(manifests);$('manifest').onchange=()=>run(read);$('chapter').onchange=()=>run(read);$('read').onclick=()=>run(read);
run(async()=>{options('tenant',await get('/api/v1/core/tenants'),r=>r.name);await universes();});
</script></html>
""";
}
