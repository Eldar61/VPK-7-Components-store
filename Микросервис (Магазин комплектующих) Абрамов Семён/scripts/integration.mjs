// HTTP integration tests. Use an isolated local database: creates test accounts/products/orders.
// Optional SQL fixtures for expiry require SHOP_TEST_DB_CONNECTION and psql path; never point at production.
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {execFileSync} from 'node:child_process';
const base=process.env.SHOP_URL||'http://localhost:5081',assembly=process.env.ASSEMBLY_URL||'http://localhost:5080';
const serviceKey=process.env.INTEGRATION_KEY||'development-only-service-key';
let passed=0;const reports=[];
async function call(path,method='GET',body,token,key=false,expected=200){
 const start=performance.now();const r=await fetch(base+path,{method,headers:{...(body!==undefined?{'Content-Type':'application/json'}:{}),...(token?{Authorization:'Bearer '+token}:{}),...(key?{'X-Service-Key':serviceKey}:{})},body:body===undefined?undefined:JSON.stringify(body)});const text=await r.text();let data;try{data=text?JSON.parse(text):undefined}catch{data=text}
 assert.equal(r.status,expected,method+' '+path+' '+JSON.stringify(data));
 reports.push({method,path,status:r.status,durationMs:Math.round(performance.now()-start)});
 return data;
}
const test=async(name,fn)=>{await fn();passed++;console.log('PASS '+name)};
const uid=crypto.randomUUID().slice(0,10);
const catalog=await call('/api/catalog');let admin=await call('/api/auth/login','POST',{login:process.env.ADMIN_LOGIN||'admin',password:process.env.ADMIN_PASSWORD||'Shop-local-admin-2026!'});const at=admin.token;
let account=await call('/api/auth/register','POST',{login:'test_'+uid,password:'Example-password-123!'}),ut=account.token;
await test('Seed >=50 real models in each category',async()=>{for(const c of ['CPU','MOTHERBOARD','RAM','GPU','PSU','CASE','STORAGE'])assert.ok(catalog.filter(e=>e.product.category===c).length>=50,c)});
await test('Catalog service-key boundary',async()=>{await call('/api/products','GET',undefined,undefined,false,401);assert.ok((await call('/api/products','GET',undefined,undefined,true)).length>=350)});
await test('Customer cannot read analytics',async()=>{await call('/api/admin/analytics?from=2026-01-01T00:00:00Z&to=2026-01-02T00:00:00Z','GET',undefined,ut,false,403)});
const template=catalog.find(e=>e.product.category==='CPU');
const input={sku:'TEST-'+uid,name:'Integration fixture (not seed catalog)',category:'CPU',active:true,priceRub:100,stockQuantity:1,specs:template.product.specs,sourceUrl:template.sourceUrl,sourceNotes:'Automated test in isolated DB',powerBasis:template.powerBasis,promotionPercent:0,promotionFrom:null,promotionTo:null,version:0};
let fixture=await call('/api/admin/products','POST',input,at,false,201);const productId=fixture.product.id;const items=[{productId,category:'CPU',quantity:1}];let currentInput={...input,version:fixture.version};
async function update(change){currentInput={...currentInput,...change};fixture=await call('/api/admin/products/'+productId,'PUT',currentInput,at);currentInput.version=fixture.version;return fixture}
const range=new URLSearchParams({from:new Date(Date.now()-86400000).toISOString(),to:new Date(Date.now()+86400000).toISOString(),group:'day'});
const stats=()=>call('/api/admin/analytics?'+range,'GET',undefined,at);
const before=(await stats()).summary;
await test('Reject negative stock',async()=>{await call('/api/admin/products/'+productId,'PUT',{...currentInput,stockQuantity:-1},at,false,400)});
await test('Reject missing category specification',async()=>{await call('/api/admin/products/'+productId,'PUT',{...currentInput,specs:{}},at,false,400)});
await test('Maximum single discount and half-away rounding',async()=>{await update({priceRub:0.01,stockQuantity:10,promotionPercent:50,promotionFrom:new Date(Date.now()-10000).toISOString(),promotionTo:new Date(Date.now()+3600000).toISOString()});const q=await call('/api/quotes','POST',{items:[{...items[0],quantity:3}]},undefined,true);assert.equal(q.lines[0].discountPercent,50);assert.equal(q.totalRub,0.02);await update({priceRub:100,stockQuantity:1,promotionPercent:0,promotionFrom:null,promotionTo:null})});
await test('Only admin sets customer type',async()=>{await call('/api/admin/users/'+account.user.id+'/customer-type','PUT',{customerType:'WHOLESALE'},ut,false,403);await call('/api/admin/users/'+account.user.id+'/customer-type','PUT',{customerType:'WHOLESALE'},at)});
await test('Guest STANDARD vs authenticated WHOLESALE',async()=>{const guest=await call('/api/quotes','POST',{items},undefined,true);const customer=await call('/api/customer/quotes','POST',{items},ut);assert.equal(guest.lines[0].discountPercent,0);assert.equal(customer.lines[0].discountPercent,10)});
const create=async(key=crypto.randomUUID(),parts=items)=>call('/api/carts','POST',{externalConfigurationId:crypto.randomUUID(),items:parts,operationKey:key},undefined,true);
const act=(cart,action,key=crypto.randomUUID(),expected=200)=>call('/api/carts/'+cart.id+'/'+action,'POST',{quoteId:cart.quote.id,operationKey:key},undefined,true,expected);
await test('Unknown product, invalid quantity, duplicate lines',async()=>{await call('/api/quotes','POST',{items:[{...items[0],productId:crypto.randomUUID()}]},undefined,true,400);await call('/api/quotes','POST',{items:[{...items[0],quantity:0}]},undefined,true,400);await call('/api/quotes','POST',{items:[...items,...items]},undefined,true,400)});
const importKey=crypto.randomUUID();const importBody={externalConfigurationId:crypto.randomUUID(),items,operationKey:importKey};let first=await call('/api/carts','POST',importBody,undefined,true);
await test('Import idempotency persists original response',async()=>assert.deepEqual(await call('/api/carts','POST',importBody,undefined,true),first));
await test('Reused key with changed request rejected',async()=>{await call('/api/carts','POST',{...importBody,externalConfigurationId:crypto.randomUUID()},undefined,true,409)});
let second=await create();let winner,loser,reserved;
await test('Two concurrent reservations for last unit: exactly one wins',async()=>{let a=await Promise.all([first,second].map(async(c,index)=>{let host=index===1&&process.env.SECOND_SHOP_URL?process.env.SECOND_SHOP_URL:base;let r=await fetch(host+'/api/carts/'+c.id+'/reserve',{method:'POST',headers:{'Content-Type':'application/json','X-Service-Key':serviceKey},body:JSON.stringify({quoteId:c.quote.id,operationKey:crypto.randomUUID()})});return {cart:c,status:r.status,data:await r.json()}}));assert.deepEqual(a.map(x=>x.status).sort(),[200,409]);let w=a.find(x=>x.status===200);winner=w.cart;reserved=w.data;loser=a.find(x=>x.status===409).cart});
await test('Reservation is excluded from sales',async()=>assert.deepEqual((await stats()).summary,before));
await test('Cannot reduce stock below reserved quantity',async()=>{await call('/api/admin/products/'+productId,'PUT',{...currentInput,stockQuantity:0},at,false,409)});
await test('Cancel releases stock, loser can then reserve',async()=>{await act(winner,'cancel');reserved=await act(loser,'reserve');assert.equal(reserved.status,'RESERVED')});
const confirmKey=crypto.randomUUID();let confirmed;
await test('Confirm decreases stock and creates one sale',async()=>{confirmed=await act(reserved,'confirm',confirmKey);assert.equal(confirmed.status,'CONFIRMED');let s=(await stats()).summary;assert.equal(s.orders,before.orders+1);assert.equal(s.units,before.units+1);assert.equal(s.revenueRub,before.revenueRub+100);const view=(await call('/api/admin/products','GET',undefined,at)).find(e=>e.product.id===productId);assert.equal(view.stockQuantity,0);currentInput.version=view.version});
await test('Repeated confirmation does not duplicate sales',async()=>{assert.deepEqual(await act(reserved,'confirm',confirmKey),confirmed);assert.equal((await stats()).summary.orders,before.orders+1)});
await test('Cannot confirm again with a new operation',async()=>{await act(reserved,'confirm',crypto.randomUUID(),409)});
await test('Sale snapshot survives product changes and archive',async()=>{await update({name:'Renamed after sale',priceRub:200,stockQuantity:2});await call('/api/admin/products/'+productId+'/archive','POST',undefined,at,false,204);assert.equal((await stats()).summary.revenueRub,before.revenueRub+100);currentInput.active=false;currentInput.version++;await update({active:true})});
await test('Updated price invalidates an existing quote',async()=>{let c=await create();await update({priceRub:210});await act(c,'reserve',crypto.randomUUID(),409);assert.equal((await call('/api/carts/'+c.id,'GET',undefined,undefined,true)).status,'DRAFT')});
await test('All-or-none multi-position reserve',async()=>{const other=catalog.find(e=>e.product.id!==productId);let c=await create(crypto.randomUUID(),[{...items[0],quantity:3},{productId:other.product.id,category:other.product.category,quantity:1}]);let beforeAvailable=(await call('/api/products?ids='+other.product.id,'GET',undefined,undefined,true))[0].availableQuantity;await act(c,'reserve',crypto.randomUUID(),409);assert.equal((await call('/api/products?ids='+other.product.id,'GET',undefined,undefined,true))[0].availableQuantity,beforeAvailable)});
await test('Customer cannot read another owner cart',async()=>{await call('/api/customer/carts/'+confirmed.id,'GET',undefined,ut,false,404)});
await test('Expired promotion ignored',async()=>{await update({promotionPercent:50,promotionFrom:new Date(Date.now()-7200000).toISOString(),promotionTo:new Date(Date.now()-3600000).toISOString()});let q=await call('/api/quotes','POST',{items},undefined,true);assert.equal(q.lines[0].discountPercent,0)});
await test('Zero-filled and category filtered analytics',async()=>{let result=await call('/api/admin/analytics?from=2000-01-01T00:00:00Z&to=2000-01-04T00:00:00Z&group=day&category=CPU','GET',undefined,at);assert.equal(result.summary.orders,0);assert.ok(result.timeline.every(x=>x.units===0));assert.equal(result.change.ordersPercent,null)});
if(process.env.SHOP_TEST_DB_CONNECTION){
 const sql=q=>execFileSync(process.env.PSQL_PATH||'C:\\Program Files\\PostgreSQL\\18\\bin\\psql.exe',[process.env.SHOP_TEST_DB_CONNECTION,'-v','ON_ERROR_STOP=1','-c',q],{encoding:'utf8',windowsHide:true});
 await test('Expired quote rejected',async()=>{let c=await create();sql("UPDATE \"Carts\" SET \"QuoteJson\" = jsonb_set(\"QuoteJson\"::jsonb,'{expiresAt}',to_jsonb('2000-01-01T00:00:00Z'::text))::text WHERE \"Id\"='"+c.id+"';");await act(c,'reserve',crypto.randomUUID(),409)});
 await test('Expired reserve frees availability and cannot confirm',async()=>{let c=await create();let r=await act(c,'reserve');sql("UPDATE \"Carts\" SET \"ExpiresAt\"=now()-interval '1 minute' WHERE \"Id\"='"+r.id+"';");let status=await call('/api/carts/'+r.id,'GET',undefined,undefined,true);assert.equal(status.status,'EXPIRED');await act(r,'confirm',crypto.randomUUID(),409);assert.equal((await stats()).summary.orders,before.orders+1)});
}
if(process.env.TEST_ASSEMBLY==='1'){
 await call('/api/admin/products/'+productId+'/archive','POST',undefined,at,false,204);
 const candidates=await call('/api/recommendations/builds','POST',{budgetRub:200000});
 await test('Shop checks real complete builds via pure assembly API',async()=>{assert.ok(candidates.length>0);assert.ok(candidates.every(x=>x.technical.compatible&&x.quote.available&&x.quote.totalRub<=200000))});
 const auth=await fetch(assembly+'/api/auth/register',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({login:'shop_integration_'+uid,password:'Example-password-123!'})}).then(r=>r.json());
 const cfg=await fetch(assembly+'/api/configurations',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+auth.token},body:JSON.stringify({name:'Real shop integration',budgetRub:200000,items:candidates[0].items})}).then(r=>r.json());
 const evaluated=await fetch(assembly+'/api/configurations/'+cfg.id+'/evaluate',{method:'POST',headers:{Authorization:'Bearer '+auth.token}}).then(r=>r.json());
 await test('Assembly fetches real products and prices from shop',async()=>{assert.equal(evaluated.technical.compatible,true);assert.equal(evaluated.available,true);assert.ok(evaluated.quote.totalRub>0)});
 fs.mkdirSync('.local',{recursive:true});fs.writeFileSync('.local/complete-build.json',JSON.stringify(candidates[0],null,2));
}
fs.mkdirSync('.local',{recursive:true});fs.writeFileSync('.local/http-test-results.json',JSON.stringify({passed,requests:reports},null,2));
console.log('RESULT '+passed+' HTTP scenarios passed');

