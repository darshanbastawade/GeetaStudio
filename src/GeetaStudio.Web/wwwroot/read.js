(() => {
const key='geeta.read.device.v1',root=document.querySelector('[data-read-root]');if(!root)return;
const ttl=90*86400000;let state={version:1,expires:Date.now()+ttl,marks:{},progress:[],consent:false};
try{const saved=JSON.parse(localStorage.getItem(key));if(saved&&saved.version===1&&saved.expires>Date.now()&&saved.marks&&!Array.isArray(saved.marks)&&typeof saved.marks==='object'&&Array.isArray(saved.progress))state=saved;else localStorage.removeItem(key);}catch{}
const status=message=>root.querySelectorAll('[data-device-status]').forEach(n=>n.textContent=message);
const persist=()=>{state.expires=Date.now()+ttl;try{localStorage.setItem(key,JSON.stringify(state));}catch{status('This device cannot save reading data. Reading still works.');}};
const validPath=path=>typeof path==='string'&&/^\/(?:preview\/)?read\/[a-z][a-z0-9-]*\/chapter\/\d+\/verse\/\d+\?/.test(path);
function list(){root.querySelectorAll('[data-device-list]').forEach(ul=>{ul.replaceChildren();Object.values(state.marks).filter(validPath).slice(-100).forEach(path=>{const li=document.createElement('li'),a=document.createElement('a');a.href=path;const match=path.match(/chapter\/(\d+)\/verse\/(\d+)/);a.textContent='Chapter '+match[1]+', verse '+match[2];li.append(a);ul.append(li);});});root.querySelectorAll('[data-read-card]').forEach(n=>n.classList.toggle('was-read',state.progress.includes(n.dataset.readCard)));}
const consent=root.querySelector('[data-read-consent]');if(consent)consent.checked=state.consent===true;
async function event(name,id){if(root.dataset.privatePreview==='true'||state.consent!==true)return;const match=location.pathname.match(/^\/read\/([a-z][a-z0-9-]*)/);if(!match)return;try{await fetch('/read-events/'+match[1],{method:'POST',headers:{'Content-Type':'application/json','X-Read-Event-Version':'1'},body:JSON.stringify({version:1,pieceId:id,name,consent:true})});}catch{}}
root.addEventListener('change',e=>{if(e.target.matches('[data-read-consent]')){state.consent=e.target.checked;persist();if(state.consent){const article=root.querySelector('[data-piece]');if(article)event('Read',article.dataset.piece);}}});
root.addEventListener('click',e=>{const button=e.target.closest('button');if(!button)return;
if(button.hasAttribute('data-clear-reading')){state={version:1,expires:Date.now()+ttl,marks:{},progress:[],consent:false};try{localStorage.removeItem(key);}catch{}if(consent)consent.checked=false;list();status('Device bookmarks and progress cleared.');}
if(button.dataset.bookmark&&validPath(button.dataset.readingPath)){state.marks[button.dataset.bookmark]=button.dataset.readingPath;state.marks=Object.fromEntries(Object.entries(state.marks).slice(-100));persist();list();status('Bookmark saved on this device.');event('Bookmark',button.dataset.bookmark);}
if(button.dataset.progress){state.progress=[...new Set([...state.progress,button.dataset.progress])].slice(-200);persist();list();status('Progress saved on this device.');event('Progress',button.dataset.progress);}});
const current=root.querySelector('[data-piece]');if(current&&state.consent===true)event('Read',current.dataset.piece);
list();
})();
