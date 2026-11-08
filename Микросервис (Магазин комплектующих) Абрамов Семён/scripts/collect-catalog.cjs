const fs=require('fs'),path=require('path'),crypto=require('crypto');
fs.mkdirSync('.local/sources',{recursive:true});
const clean=s=>s.replace(/<br\s*\/?>/gi,'; ').replace(/<[^>]*>/g,' ').replace(/&nbsp;|&#160;/g,' ').replace(/&amp;/g,'&').replace(/&#(?:[0-9]+);/g,' ').replace(/\s+/g,' ').trim();
async function get(url){let f='.local/sources/'+crypto.createHash('sha256').update(url).digest('hex')+'.html';if(fs.existsSync(f))return fs.readFileSync(f,'utf8');let r=await fetch(url,{signal:AbortSignal.timeout(30000)});let t=await r.text();if(!r.ok)throw Error(r.status);fs.writeFileSync(f,t);return t;}
function specs(html){let o={};for(let m of html.matchAll(/<li class="specName">([\s\S]*?)<\/li>[\s\S]*?<\/ul>([\s\S]*?)<\/div>/g))o[clean(m[1])]=clean(m[2]);return o;}
const gpuSlugs=[
'GeForce-RTX-4060-VENTUS-2X-BLACK-8G-OC','GeForce-RTX-4060-GAMING-X-8G','GeForce-RTX-4060-VENTUS-2X-WHITE-8G-OC','GeForce-RTX-4060-AERO-ITX-8G-OC',
'GeForce-RTX-4060-Ti-VENTUS-2X-BLACK-8G-OC','GeForce-RTX-4060-Ti-GAMING-X-8G','GeForce-RTX-4060-Ti-GAMING-X-TRIO-8G','GeForce-RTX-4060-Ti-VENTUS-3X-16G-OC','GeForce-RTX-4060-Ti-GAMING-X-SLIM-16G','GeForce-RTX-4060-Ti-VENTUS-2X-BLACK-16G-OC',
'GeForce-RTX-4070-VENTUS-2X-12G-OC','GeForce-RTX-4070-VENTUS-3X-12G-OC','GeForce-RTX-4070-GAMING-X-TRIO-12G','GeForce-RTX-4070-GAMING-X-SLIM-12G',
'GeForce-RTX-4070-SUPER-12G-VENTUS-2X-OC','GeForce-RTX-4070-SUPER-12G-VENTUS-3X-OC','GeForce-RTX-4070-SUPER-12G-GAMING-X-SLIM',
'GeForce-RTX-4070-Ti-VENTUS-3X-12G-OC','GeForce-RTX-4070-Ti-GAMING-X-TRIO-12G','GeForce-RTX-4070-Ti-SUPRIM-X-12G',
'GeForce-RTX-4070-Ti-SUPER-16G-VENTUS-2X-OC','GeForce-RTX-4070-Ti-SUPER-16G-VENTUS-3X-OC','GeForce-RTX-4070-Ti-SUPER-16G-GAMING-X-SLIM',
'GeForce-RTX-4080-16GB-VENTUS-3X-OC','GeForce-RTX-4080-16GB-GAMING-X-TRIO','GeForce-RTX-4080-16GB-SUPRIM-X',
'GeForce-RTX-4080-SUPER-16G-VENTUS-3X-OC','GeForce-RTX-4080-SUPER-16G-GAMING-X-SLIM','GeForce-RTX-4080-SUPER-16G-SUPRIM-X',
'GeForce-RTX-4090-VENTUS-3X-24G-OC','GeForce-RTX-4090-GAMING-X-TRIO-24G','GeForce-RTX-4090-SUPRIM-X-24G','GeForce-RTX-4090-SUPRIM-LIQUID-X-24G',
'GeForce-RTX-3060-VENTUS-2X-12G-OC','GeForce-RTX-3060-GAMING-X-12G','GeForce-RTX-3060-VENTUS-3X-12G-OC','GeForce-RTX-3060-AERO-ITX-12G-OC',
'GeForce-RTX-3060-Ti-VENTUS-2X-OC','GeForce-RTX-3060-Ti-GAMING-X-TRIO','GeForce-RTX-3060-Ti-VENTUS-3X-OC',
'GeForce-RTX-3070-VENTUS-2X-OC','GeForce-RTX-3070-VENTUS-3X-OC','GeForce-RTX-3070-GAMING-X-TRIO',
'GeForce-RTX-3070-Ti-VENTUS-3X-8G-OC','GeForce-RTX-3070-Ti-GAMING-X-TRIO-8G','GeForce-RTX-3070-Ti-SUPRIM-X-8G',
'GeForce-RTX-3080-VENTUS-3X-10G-OC','GeForce-RTX-3080-GAMING-X-TRIO-10G','GeForce-RTX-3080-SUPRIM-X-10G',
'GeForce-RTX-3080-Ti-VENTUS-3X-12G-OC','GeForce-RTX-3080-Ti-GAMING-X-TRIO-12G','GeForce-RTX-3080-Ti-SUPRIM-X-12G',
'GeForce-RTX-3090-VENTUS-3X-24G-OC','GeForce-RTX-3090-GAMING-X-TRIO-24G','GeForce-RTX-3090-SUPRIM-X-24G','GeForce-RTX-3090-Ti-SUPRIM-X-24G',
'GeForce-RTX-3050-VENTUS-2X-8G-OC','GeForce-RTX-3050-GAMING-X-8G','GeForce-GTX-1660-SUPER-VENTUS-XS-OC','GeForce-GTX-1650-D6-VENTUS-XS-OC'];
const boards=['B550-A-PRO','MAG-B550-TOMAHAWK','MAG-B550M-MORTAR','MAG-B550M-MORTAR-WIFI','MPG-B550-GAMING-PLUS','MPG-B550-GAMING-EDGE-WIFI','MPG-B550-GAMING-CARBON-WIFI','B550M-PRO-VDH-WIFI','B550M-PRO-VDH','B550M-PRO','B450-TOMAHAWK-MAX','B450-TOMAHAWK-MAX-II','B450M-MORTAR-MAX','B450-GAMING-PLUS-MAX','B450M-PRO-VDH-MAX','B450-A-PRO-MAX','A520M-A-PRO','A520M-PRO','MAG-X570-TOMAHAWK-WIFI','MPG-X570-GAMING-PLUS','MPG-X570-GAMING-EDGE-WIFI','MEG-X570-UNIFY','PRO-B650-P-WIFI','PRO-B650M-A-WIFI','PRO-B650M-A','MAG-B650-TOMAHAWK-WIFI','MAG-B650M-MORTAR-WIFI','MPG-B650-EDGE-WIFI','MPG-B650-CARBON-WIFI','PRO-B650-S-WIFI','PRO-B650M-P','PRO-A620M-E','PRO-A620M-B','MAG-X670E-TOMAHAWK-WIFI','MPG-X670E-CARBON-WIFI','PRO-X670-P-WIFI','MAG-Z790-TOMAHAWK-WIFI','MAG-Z790-TOMAHAWK-WIFI-DDR4','PRO-Z790-A-WIFI','PRO-Z790-A-WIFI-DDR4','PRO-Z790-P-WIFI','PRO-Z790-P-WIFI-DDR4','MPG-Z790-EDGE-WIFI','MPG-Z790-CARBON-WIFI','PRO-B760M-A-WIFI','PRO-B760M-A-WIFI-DDR4','PRO-B760M-P-DDR4','PRO-B760-P-WIFI-DDR4','MAG-B760-TOMAHAWK-WIFI','MAG-B760-TOMAHAWK-WIFI-DDR4','MAG-B760M-MORTAR-WIFI','MAG-B760M-MORTAR-WIFI-DDR4','PRO-Z690-A','PRO-Z690-A-DDR4','PRO-B660M-A-DDR4','PRO-B660M-A-WIFI-DDR4','PRO-H610M-G-DDR4','PRO-H610M-E-DDR4','MAG-Z690-TOMAHAWK-WIFI-DDR4','MPG-Z690-EDGE-WIFI-DDR4'];
async function pool(jobs,fn,n=5){let i=0;await Promise.all(Array.from({length:n},async()=>{while(i<jobs.length){let j=jobs[i++];try{await fn(j);}catch(e){console.log('FAIL',j,e.message);}}}));}
async function main(){let result=[];let targets=[...gpuSlugs.map(slug=>({slug,type:'Graphics-Card',category:'GPU'})),...boards.map(slug=>({slug,type:'Motherboard',category:'MOTHERBOARD'}))];
await pool(targets,async x=>{let url='https://www.msi.com/'+x.type+'/'+x.slug+'/Specification';let html=await get(url),s=specs(html);if(Object.keys(s).length<5)throw Error('No specifications');result.push({...x,url,specs:s});console.log('OK',x.slug,Object.keys(s).length);});
fs.writeFileSync('.local/msi-products.json',JSON.stringify(result,null,2));
for(let [name,url] of Object.entries({fractal:'https://www.fractal-design.com/products/cases/',corsair:'https://www.corsair.com/us/en/sitemap.xml',amd:'https://www.amd.com/en/products/specifications/processors.html',kingston:'https://www.kingston.com/en/memory/gaming/kingston-fury-beast-ddr4-memory'})){try{fs.writeFileSync('.local/'+name+'.html',await get(url));console.log('INDEX',name)}catch(e){console.log('INDEXFAIL',name,e.message)}}
}
async function stage2(){
 const amd=fs.readFileSync('.local/amd.html','utf8');const data=JSON.parse(amd.match(/data-json="([^"]+)"/)[1].replace(/&#34;/g,'"').replace(/&amp;/g,'&').replace(/&#39;/g,"'"));fs.writeFileSync('.local/amd-products.json',JSON.stringify(data,null,2));
 const jobs=[['corsair-psu','https://www.corsair.com/us/en/c/psu'],['fractal-power','https://www.fractal-design.com/products/power-supplies/'],['board-sample','https://storage-asset.msi.com/datasheet/mb/global/B550-A-PRO.pdf'],['ram-sample','https://www.kingston.com/datasheets/KF432C16BB_8.pdf']];
 for(const [name,url] of jobs){try{const r=await fetch(url,{signal:AbortSignal.timeout(30000)});if(!r.ok)throw Error(r.status);const b=Buffer.from(await r.arrayBuffer());fs.writeFileSync('.local/'+name+(url.endsWith('.pdf')?'.pdf':'.html'),b);console.log(name,r.status,b.length)}catch(e){console.log(name,e.message)}}
 const links=[...new Set([...fs.readFileSync('.local/fractal.html','utf8').matchAll(/href="([^"]*products\/cases\/[^\"]*)"/g)].map(m=>m[1]))].filter(x=>x.startsWith('/products')&&x.split('/').length>6);
 await pool(links,async url=>{let html=await get('https://www.fractal-design.com'+url);console.log('CASE',url,html.length)},3);
 const result=JSON.parse(fs.readFileSync('.local/msi-products.json'));const failed=[...boards.map(slug=>({slug,category:'MOTHERBOARD',kind:'mb'})),...gpuSlugs.filter(slug=>!result.some(r=>r.slug===slug)).map(slug=>({slug,category:'GPU',kind:'vga'}))];
 await pool(failed,async x=>{const url='https://storage-asset.msi.com/datasheet/'+x.kind+'/global/'+x.slug+'.pdf';let r=await fetch(url,{signal:AbortSignal.timeout(25000)});if(!r.ok)throw Error(r.status);let b=Buffer.from(await r.arrayBuffer());if(b.slice(0,4).toString()!='%PDF')throw Error('Not a PDF');fs.writeFileSync('.local/sources/'+x.slug+'.pdf',b);console.log('PDF',x.slug)},3);
}
async function stage3(){
 const h=fs.readFileSync('.local/corsair-psu.html','utf8');const ps=JSON.parse(h.match(/<script id="__NEXT_DATA__"[^>]*>([\s\S]*?)<\/script>/)[1]).props.pageProps.categoryData.products.items;
 const variants=[...new Map(ps.flatMap(x=>(x.variants||[{product:x}]).map(v=>v.product)).map(p=>[p.sku,p])).values()];fs.writeFileSync('.local/corsair-products.json',JSON.stringify(variants,null,2));console.log('PSU VARIANTS',variants.length);
 await pool(variants,async p=>{await get('https://www.corsair.com/us/en/p/psu/'+p.sku.toLowerCase()+'/'+p.url_key);console.log('PSU',p.sku)},2);
 await pool(boards,async slug=>{const url='https://us.msi.com/Motherboard/'+slug+'/Specification';let html=await get(url);fs.writeFileSync('.local/sources/board-'+slug+'.html',html);console.log('BOARDHTML',slug,Object.keys(specs(html)).length)},2);
 let all=new Set();for(let name of fs.readdirSync('.local/sources').filter(x=>x.endsWith('.html'))){let html=fs.readFileSync('.local/sources/'+name,'utf8');if(!html.includes('fractal-design'))continue;for(let m of html.matchAll(/href="([^\"]*products\/cases\/[^\"]*)"/g)){let link=m[1];if(link.startsWith('/products/cases/')&&link.split('/').length>6)all.add(link);}}
 fs.writeFileSync('.local/fractal-links.json',JSON.stringify([...all],null,2));console.log('CASE LINKS',all.size);
 await pool([...all].slice(0,90),async url=>{await get('https://www.fractal-design.com'+url);console.log('CASEVARIANT',url)},2);
}
async function stage4(){
 const families=['KF426C16BB','KF430C15BB','KF432C16BB','KF432C16BB1','KF432C16RB','KF436C17BB','KF436C18BB','KF436C16RB','KF440C19RB','KF432C16BBA','KF436C18BBA','KF548C38BB','KF552C40BB','KF556C40BB','KF556C36BBE','KF560C40BB','KF560C36BBE','KF560C30BBE','KF564C32BB','KF568C36RS','KF572C38RS','KF580C38RS','KVR24N17S8','KVR24N17D8','KVR26N19S8','KVR26N19D8','KVR32N22S8','KVR32N22D8','KVR48U40BS8','KVR48U40BD8','KVR56U46BS8','KVR56U46BD8'];
 const candidates=families.flatMap(p=>[8,16,32].map(cap=>p+'_'+cap));const verified=[];
 await pool(candidates,async candidate=>{let sku=/^(KF5|KVR(?:48|56))/.test(candidate)?candidate.replace('_','-'):candidate;let url='https://www.kingston.com/datasheets/'+sku+'.pdf';let f='.local/sources/'+sku+'.pdf';if(!fs.existsSync(f)){let r=await fetch(url,{signal:AbortSignal.timeout(20000)});if(!r.ok)return;let b=Buffer.from(await r.arrayBuffer());if(b.slice(0,4).toString()!='%PDF')return;fs.writeFileSync(f,b);}verified.push({sku:sku.replace('_','/'),url,file:f});console.log('RAM',sku)},3);
 fs.writeFileSync('.local/ram-products.json',JSON.stringify(verified,null,2));console.log('RAM COUNT',verified.length);
 for(let [name,url] of [['corsair-ssd','https://www.corsair.com/us/en/c/data-storage/m2-ssd'],['corsair-ssd-history','https://help.corsair.com/hc/en-us/articles/360060189152-SSD-What-are-the-endurance-ratings-for-Corsair-SSDs'],['samsung-sata','https://semiconductor.samsung.com/consumer-storage/internal-ssd/870evo/']]){try{let html=await get(url);fs.writeFileSync('.local/'+name+'.html',html);console.log(name,html.length)}catch(e){console.log(name,e.message)}}
}
function containerAt(s,start){let depth=0,quoted=false,escape=false;for(let i=start;i<s.length;i++){let ch=s[i];if(quoted){if(escape)escape=false;else if(ch==='\\')escape=true;else if(ch==='"')quoted=false;}else{if(ch==='"')quoted=true;else if(ch==='['||ch==='{')depth++;else if(ch===']'||ch==='}'){if(--depth===0)return JSON.parse(s.slice(start,i+1));}}}throw Error('Incomplete JSON');}
async function stage5(){
 const links=JSON.parse(fs.readFileSync('.local/fractal-links.json'));const candidates=[];
 for(let url of links){let html=await get('https://www.fractal-design.com'+url);let stream=[...html.matchAll(/self\.__next_f\.push\((\[[\s\S]*?\])\)<\/script>/g)].map(m=>{try{return JSON.parse(m[1])[1]||''}catch{return ''}}).join('');
 const family=url.split('/').at(-3);const display=family.replaceAll('-',' ');
 for(let m of stream.matchAll(/"sku":"(FD-C-[^"]+)","slug":"([^"]+)","title":"([^"]+)"/g)){let title=m[3].toLowerCase();if(title===display||title.startsWith(display+' '))candidates.push({sku:m[1],name:'Fractal Design '+m[3],url:'https://www.fractal-design.com'+url.split('/').slice(0,-2).join('/')+'/'+m[2]+'/'});}
 }
 const unique=[...new Map(candidates.map(c=>[c.sku,c])).values()];fs.writeFileSync('.local/fractal-candidates.json',JSON.stringify(unique,null,2));console.log('CASES FOUND',unique.length);
 await pool(unique.slice(0,90),async p=>{await get(p.url);console.log('CASESKU',p.sku)},3);
 const ssds=JSON.parse(fs.readFileSync('.local/ssd-products.json'));await pool(ssds.filter(p=>p.sku?.startsWith('CSSD-')),async p=>{await get('https://www.corsair.com/us/en/p/data-storage/'+p.sku.toLowerCase()+'/'+p.url_key);console.log('SSD',p.sku)},3);
}
async function stage7(){const url='https://www.corsair.com/us/en/c/pc-cases';const html=await get(url);fs.writeFileSync('.local/corsair-cases.html',html);const items=JSON.parse(html.match(/<script id="__NEXT_DATA__"[^>]*>([\s\S]*?)<\/script>/)[1]).props.pageProps.categoryData.products.items;const products=[...new Map(items.flatMap(x=>(x.variants||[{product:x}]).map(v=>v.product)).map(x=>[x.sku,x])).values()];fs.writeFileSync('.local/corsair-case-products.json',JSON.stringify(products,null,2));console.log('CORSAIR CASES',products.length);await pool(products.filter(p=>p.sku?.startsWith('CC-')).slice(0,60),async p=>{await get('https://www.corsair.com/us/en/p/pc-cases/'+p.sku.toLowerCase()+'/'+p.url_key);console.log('CORSAIRCASE',p.sku)},3);}
(process.argv.includes('--stage7')?stage7():process.argv.includes('--stage5')?stage5():process.argv.includes('--stage4')?stage4():process.argv.includes('--stage3')?stage3():process.argv.includes('--stage2')?stage2():main()).catch(e=>{console.error(e);process.exitCode=1});

