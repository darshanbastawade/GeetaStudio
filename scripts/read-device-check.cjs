const assert=require('node:assert/strict'),vm=require('node:vm'),fs=require('node:fs'),path=require('node:path');
const code=fs.readFileSync(path.join(__dirname,'../src/GeetaStudio.Web/wwwroot/read.js'),'utf8');
function boot(saved,preview=false){
const handlers={},storage=new Map(saved?[['geeta.read.device.v1',saved]]:[]),events=[],status={textContent:''},list={children:[],replaceChildren(){this.children=[]},append(n){this.children.push(n)}},consent={checked:false};
const root={dataset:{privatePreview:String(preview)},querySelectorAll(s){return s==='[data-device-list]'?[list]:s==='[data-device-status]'?[status]:[]},querySelector(s){return s==='[data-read-consent]'?consent:s==='[data-piece]'?{dataset:{piece:'00000000-0000-0000-0000-000000000001'}}:null},addEventListener(n,f){handlers[n]=f}};
vm.runInNewContext(code,{Date,JSON,Object,Array,Set,document:{querySelector:()=>root,createElement:()=>({children:[],append(n){this.children.push(n)}})},localStorage:{getItem:k=>storage.get(k)||null,setItem:(k,v)=>storage.set(k,v),removeItem:k=>storage.delete(k)},location:{pathname:'/read/pilot/chapter/1/verse/1'},fetch:async(url,r)=>{events.push(JSON.parse(r.body));return {ok:true}}});
return {storage,events,list,click(dataset,clear=false){handlers.click({target:{closest:()=>({dataset,hasAttribute:()=>clear})}})},consent(value){handlers.change({target:{matches:()=>true,checked:value}})}};
}
const route='/read/pilot/chapter/1/verse/1?locale=en-IN&audience=00000000-0000-0000-0000-000000000002',id='00000000-0000-0000-0000-000000000001';
const one=boot();one.click({bookmark:id,readingPath:route});one.click({progress:id});assert.equal(one.events.length,0);
const saved=one.storage.get('geeta.read.device.v1'),state=JSON.parse(saved);assert.equal(state.marks[id],route);assert.equal(state.progress[0],id);assert.equal(state.consent,false);assert.ok(state.expires>Date.now()+89*86400000);
const restart=boot(saved);assert.equal(restart.list.children.length,1);assert.equal(restart.list.children[0].children[0].href,route);restart.click({},true);assert.equal(restart.storage.size,0);
assert.equal(boot(JSON.stringify({...state,expires:1})).storage.size,0);
assert.equal(boot(JSON.stringify({...state,marks:null})).storage.size,0);
one.click({bookmark:'unsafe',readingPath:'https://evil.invalid'});assert.equal(JSON.parse(one.storage.get('geeta.read.device.v1')).marks.unsafe,undefined);
one.consent(true);assert.equal(one.events.length,1);assert.equal(one.events[0].name,'Read');assert.equal(one.events[0].version,1);assert.equal(Object.hasOwn(one.events[0],'query'),false);
const optedIn=boot(JSON.stringify({...state,consent:true}));assert.equal(optedIn.events.length,1);
const privatePreview=boot(JSON.stringify({...state,consent:true}),true);privatePreview.click({bookmark:id,readingPath:route});privatePreview.consent(true);assert.equal(privatePreview.events.length,0);
console.log('Device reload, bookmark/progress, clear/expiry, unsafe-link and consent/private event checks pass.');