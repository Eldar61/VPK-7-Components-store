// Build a deterministic educational catalog from downloaded manufacturer facts.
// Run collect-catalog.cjs stages first; cached source documents stay in ignored .local/.
const fs=require('fs'),crypto=require('crypto');
const clean=s=>s.replace(/<script[\s\S]*?<\/script>/gi,'').replace(/<style[\s\S]*?<\/style>/gi,'').replace(/<br\s*\/?>|<\/br>/gi,'; ').replace(/<[^>]*>/g,' ').replace(/&nbsp;|&#160;/g,' ').replace(/&quot;|&#34;/g,'"').replace(/&amp;/g,'&').replace(/&reg;/g,'').replace(/\s+/g,' ').trim();
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const hash=s=>crypto.createHash('sha256').update(s).digest('hex');
const cache=url=>'.local/sources/'+hash(url)+'.html';
const get=url=>fs.readFileSync(cache(url),'utf8');
const results=[],sources=[],rejected=[];
function id(s){let h=hash(String(s)).slice(0,32);return h.slice(0,8)+'-'+h.slice(8,12)+'-4'+h.slice(13,16)+'-a'+h.slice(17,20)+'-'+h.slice(20);}
function add(category,sku,name,specs,url,powerBasis,facts={},notes=''){
 url=url||"https://www.amd.com/en/products/specifications/processors.html";sku=Array.isArray(sku)?sku[0]:sku;sku=String(sku||name); const n=results.filter(x=>x.input.category===category).length;const categoryBase={CPU:7500,GPU:18000,MOTHERBOARD:6500,RAM:1600,PSU:4200,CASE:4500,STORAGE:2500};
 const row={id:id(sku),input:{sku,name:clean(name),category,active:true,priceRub:categoryBase[category]+n*310+(n%4)*250,stockQuantity:15+n%20,specs,sourceUrl:url,sourceNotes:'Реальная модель и паспортные характеристики производителя. Цена и остаток учебные. '+notes,powerBasis,promotionPercent:0,promotionFrom:null,promotionTo:null,version:0}};
 if(results.some(x=>x.input.sku===sku))return;
 results.push(row);const technical={...specs};delete technical.estimatedPowerW;const evidence=category==="CPU"||category==="RAM"?facts:category==="GPU"?{declaredPower:facts["Power consumption"],declaredDimensions:facts["Card Dimension (mm)"],declaredConnectors:facts["Power connectors"]}:category==="PSU"?{declaredContinuousPower:facts["Continuous Power W"],declaredFormFactor:facts["PSU Form Factor"]}:{};sources.push({productId:row.id,sku,name:row.input.name,category,sourceUrl:url,retrievedAt:'2026-11-08',facts:{...evidence,compatibilityFields:technical},sourceSha256:fs.existsSync(cache(url))?hash(fs.readFileSync(cache(url))):null,powerBasis,notes});
}
function msiSpecs(html){const o={};for(let m of html.matchAll(/<li class="specName">([\s\S]*?)<\/li>[\s\S]*?<\/ul>([\s\S]*?)<\/div>/g))o[clean(m[1])]=clean(m[2]);for(let m of html.matchAll(/<th\b[^>]*>([\s\S]*?)<\/th>\s*<td\b[^>]*>([\s\S]*?)<\/td>/g))o[clean(m[1])]=clean(m[2]);return o;}
function form(s){if(/micro.?atx|m-atx|matx/i.test(s))return 'mATX';if(/mini.?itx|mitx/i.test(s))return 'Mini-ITX';if(/e.?atx/i.test(s))return 'E-ATX';if(/atx/i.test(s))return 'ATX';return null;}
function forms(s){return [...new Set(s.split(/[,|/;]/).map(form).filter(Boolean))];}
function corsair(p,kind){const url='https://www.corsair.com/us/en/p/'+kind+'/'+p.sku.toLowerCase()+'/'+p.url_key;const html=get(url);const data=JSON.parse(html.match(/<script id="__NEXT_DATA__"[^>]*>([\s\S]*?)<\/script>/)[1]).props.pageProps.productData.productDetail.items[0];const selected=data.variants?.find(x=>x.product?.sku===p.sku)?.product;const specs=Object.fromEntries((selected?.tech_specs?.length?selected.tech_specs:data.tech_specs||[]).map(x=>[x.code,x.value]));return {url,html,specs,name:selected?.name||data.name||p.name};}
async function main(){
 const pdfjs=await import('../.local/catalog-tools/node_modules/pdfjs-dist/legacy/build/pdf.mjs');
 async function pdf(file){let out=file.replace(/\.pdf$/,'.text');if(fs.existsSync(out))return fs.readFileSync(out,'utf8');let d=await pdfjs.getDocument({data:new Uint8Array(fs.readFileSync(file)),useSystemFonts:true,verbosity:0}).promise;let all='';for(let i=1;i<=d.numPages;i++){let page=await d.getPage(i);all+=(await page.getTextContent()).items.map(x=>x.str).join('\n')+'\n';}fs.writeFileSync(out,all);return all;}
 // Official AMD specification table contains exact name, socket, TDP and product link.
 const cpus=read('.local/amd-products.json').items.filter(x=>x.elements.cpuSocket?.value?.some(v=>v==='AM4'||v==='AM5')&&/^AMD Ryzen/.test(x.elements.name?.value||'')&&!/PRO|AI/.test(x.elements.name.value)&&parseFloat(x.elements.defaultTdp?.value)>0);
 // Prefer a mixture of recent and legacy AM4/AM5 retail desktop processors.
 const unique=[...new Map(cpus.map(x=>[x.elements.name.value,x])).values()].sort((a,b)=>a.elements.name.value.localeCompare(b.elements.name.value));
 const cpuChosen=[...unique.filter(x=>x.elements.cpuSocket.value[0]==='AM4'&&/\b5\d{3}/.test(x.elements.name.value)),...unique.filter(x=>x.elements.cpuSocket.value[0]==='AM5').slice(-25)];
 for(let x of cpuChosen){const e=x.elements;const tdp=parseFloat(e.defaultTdp.value);const sku=e.productIdBoxed?.value||e.productIdTray?.value||e.name.value;add('CPU',sku,e.name.value,{socket:e.cpuSocket.value[0],estimatedPowerW:Math.ceil(tdp*1.35)},x.productPages.en,'Учебная оценка ceil(TDP × 1,35). TDP — тепловой параметр производителя, это не измеренная электрическая мощность.',{socket:e.cpuSocket.value[0],tdpW:tdp},'BIOS-поддержка конкретного CPU не проверяется текущим контрактом.');}
 // MSI full specification pages; no guessed socket, ports or capacity.
 for(let file of fs.readdirSync('.local/sources').filter(x=>x.startsWith('board-')&&x.endsWith('.html'))){
  let slug=file.slice(6,-5);try{let s=msiSpecs(fs.readFileSync('.local/sources/'+file,'utf8'));let memory=s.Memory||'';let socket=s.Socket||(s.CPU||'').match(/(?:Socket\s*)?(AM[45]|LGA\s*1700)/i)?.[1]||(/(?:Z790|B760)/.test(slug)?'LGA1700':null);
   let type=Object.keys(s).some(k=>k.startsWith('DDR5'))?'DDR5':Object.keys(s).some(k=>k.startsWith('DDR4'))?'DDR4':memory.match(/DDR[45]/)?.[0];
   let slots=Number(s['Memory Slot']||s.DIMM||memory.match(/(\d+)\s*x?\s*DDR[45]/i)?.[1]);if(!slots){const text=await pdf('.local/sources/'+slug+'.pdf');slots=Number(text.replace(/\s+/g,' ').match(/(\d+)\s*(?:DIMMs|DIMM|x DDR)/i)?.[1]);}
   let max=Number(s['Max Memory (GB)']||s['Max. Memory (GB)']||memory.match(/(?:Capacity|Max(?:imum)?\.?\s*Memory)\s*(\d+)\s*GB/i)?.[1]);let pcie=Number(s['PCI-E x16']||s.Slot?.match(/(\d+)x PCI-E x16/i)?.[1]);
   let sata=Number(s.SATAIII||s['SATA 6Gb/s']||s.Storage?.match(/(\d+)\s*x?\s*SATA/i)?.[1]);let m2=Number(s['M.2 Slot']||s.Storage?.match(/(\d+)\s*x?\s*M\.2/i)?.[1]);let boardForm=form(s['Form Factor']||s['PCB Info']||'');
   if(!socket||!type||!slots||!max||!Number.isFinite(pcie)||!Number.isFinite(sata)||!Number.isFinite(m2)||!boardForm)throw Error(JSON.stringify({socket,type,slots,max,pcie,sata,m2,boardForm}));
   add('MOTHERBOARD','MSI-'+slug,'MSI '+slug.replaceAll('-',' '),{socket:socket.replace(/\s/g,''),memoryType:type,memorySlots:slots,maxMemoryGb:max,pcieX16Slots:pcie,formFactor:boardForm,sataPorts:sata,m2Slots:m2,estimatedPowerW:35},'https://us.msi.com/Motherboard/'+slug+'/Specification','Учебная оценка собственной мощности платы 35 Вт; мощность установленных CPU/RAM/GPU считается отдельно.',s,'Порты указаны номинально; разделение линий с некоторыми CPU и отключение M.2/SATA не моделируются.');
  }catch(e){rejected.push({category:'MOTHERBOARD',sku:slug,reason:e.message});}
 }
 // MSI graphics cards: exact physical card variants, not just GPU chip names.
 const gpuRows=read('.local/msi-products.json').filter(x=>x.category==='GPU');
 const pdfRows=fs.readdirSync('.local/sources').filter(x=>x.startsWith('GeForce-')&&x.endsWith('.pdf')).map(f=>({slug:f.slice(0,-4),file:'.local/sources/'+f}));
 const allGpus=[...new Map([...gpuRows,...pdfRows].map(x=>[x.slug,x])).values()];
 for(let x of allGpus){if(/LIQUID/.test(x.slug))continue;try{let s=x.specs,url=x.url;if(!s){const text=(await pdf(x.file)).replace(/\s+/g,' ');s={};for(let [k,pattern] of [['Power consumption',/Power Consumption\s+(\d+(?:\.\d+)?)\s*W/i],['Power connectors',/Power Connectors\s+(.+?)(?:Recommended|Power Consumption|Card Dimension|Dimensions|Weight)/i],['Card Dimension (mm)',/(?:Card Dimension\s*\(mm\)|Card Dimension|Dimensions)\s+([\d.]+\s*x\s*[\d.]+\s*x\s*[\d.]+)\s*mm/i]])s[k]=text.match(pattern)?.[1]||'';url='https://storage-asset.msi.com/datasheet/vga/global/'+x.slug+'.pdf';}
   const len=Number(s['Card Dimension (mm)']?.match(/([\d.]+)\s*x/i)?.[1]);const watts=Math.max(...(s['Power consumption']?.match(/\d+(?:\.\d+)?/g)||[]).map(Number));let connectors={},raw=s['Power connectors']||'';
   for(let m of raw.matchAll(/(8|6|16)[- ]?pin\s*x\s*(\d+)/gi))connectors[m[1]==='16'?'12VHPWR':'PCIE_'+m[1]+'PIN']=Number(m[2]);
   if(!Object.keys(connectors).length&&/(\d+)\s*x\s*(8|6|16)[- ]?pin/i.test(raw)){let m=raw.match(/(\d+)\s*x\s*(8|6|16)[- ]?pin/i);connectors[m[2]==='16'?'12VHPWR':'PCIE_'+m[2]+'PIN']=Number(m[1]);}
   if(connectors.PCIE_6PIN)continue; if(!len||!Number.isFinite(watts)||watts<=0||(!Object.keys(connectors).length&&!/N\/A|None/i.test(raw)))throw Error(JSON.stringify(s));
   add('GPU','MSI-'+x.slug,'MSI '+(s['Model Name']||x.slug.replaceAll('-',' ')),{gpuLengthMm:Math.ceil(len),requiredPowerConnectors:connectors,estimatedPowerW:watts},url,'Оценка по заявленной MSI Power consumption. Если указано несколько значений, взято максимальное.',s,'16-pin у RTX 30/40 обозначен как 12VHPWR. Переходники не добавляются автоматически.');
  }catch(e){rejected.push({category:'GPU',sku:x.slug,reason:e.message});}
 }
 for(let x of read('.local/ram-products.json')){try{let text=(await pdf(x.file)).replace(/\s+/g,' ');let type=text.match(/DDR[45]/)?.[0];let capacity=Number(text.match(/\((\d+)GB\)/)?.[1]||text.match(/\b(\d+)GB\s+\d+[RGx]/)?.[1]||text.match(/\b(\d+)GB\b/)?.[1]);if(!type||!capacity||!/(?:288|DDR5).*?(?:Pin|pin)|288/i.test(text)||/kit of|kit consisting|kit.*two/i.test(text))throw Error('Not a verified single desktop DIMM');
  add('RAM',x.sku,'Kingston '+(x.sku.startsWith('KF')?'FURY ':'ValueRAM ')+x.sku+' '+capacity+'GB '+type,{memoryType:type,moduleCapacityGb:capacity,estimatedPowerW:type==='DDR5'?6:4},x.url,'Учебная оценка на один модуль: DDR4 4 Вт, DDR5 6 Вт. Не паспортное измерение.',{moduleCapacityGb:capacity,memoryType:type,kind:'single 288-pin DIMM'},'Одна позиция — один модуль, не комплект. XMP/EXPO и частоты в текущем правиле совместимости не проверяются.');
 }catch(e){rejected.push({category:'RAM',sku:x.sku,reason:e.message});}}
 // Corsair's public product JSON provides exact SKUs and wattage, cable tables in HTML provide connectors.
 for(let p of read('.local/corsair-products.json').filter(x=>x.sku?.startsWith('CP-'))){try{const {url,html,specs:s,name}=corsair(p,'psu');let text=clean(html);let power=Number((s['Continuous Power W']||name).match(/(\d{3,4})\s*(?:Watts|W)/i)?.[1]);let f=/SF.?L/i.test(name)?'SFX-L':/\bSF\d|SF Series/i.test(name)?'SFX':s['PSU Form Factor']||'ATX';let connectors={};let pcie=Number(s['PCIe Connector']||s['PCI-E Connector']||0);
  for(let m of text.matchAll(/CABLE\s+(?:PCIe|PCI-E)\s*8[- ]Pin[^]*?TOTAL CONNECTORS\s*(\d+)/gi)){pcie=Math.max(pcie,Number(m[1]));break;}
  const native=text.match(/CABLE\s+12V(?:HPWR|.2x6)[^]*?TOTAL CONNECTORS\s*(\d+)/i);
  let pin16=Number(s['12VHPWR Connector']||s['12V-2x6 Connector']||native?.[1]||0);
  // Table-based legacy cable descriptions carry quantity before the cable name.
  if(!pcie){let m=text.match(/(\d+)\s+(?:PCIe|PCI-E|8-pin PCIe)\s*(?:8[- ]pin)?\s*\(6\+2[- ]pin\)/i);if(m)pcie=Number(m[1]);}
  if(!pcie){let m=text.match(/(\d+)\s+(?:PCIe|PCI-E)\s*(?:8[- ]pin)?\s*\(6\+2\)/i);if(m)pcie=Number(m[1]);}
  if(!pin16&&/(?:Native|INCLUDES|included|1 600W).*?12V.2x6/i.test(text))pin16=1;
  if(pcie)connectors.PCIE_8PIN=pcie;if(pin16){connectors['12VHPWR']=pin16;connectors['12V_2X6']=pin16;}
  if(!power||!Object.keys(connectors).length)throw Error('No verified cable counts: '+JSON.stringify(s));
  add('PSU',p.sku,'CORSAIR '+name+' ['+p.sku+']',{ratedPowerW:power,formFactor:f,powerConnectors:connectors},url,'БП не является потребителем на диаграмме; ratedPowerW — номинальная выходная мощность.',s,'Для включённого 16-pin кабеля указаны совместимые 12VHPWR/12V-2x6 подключения. Сменные кабели других БП использовать нельзя.');
 }catch(e){rejected.push({category:'PSU',sku:p.sku,reason:e.message});}}
 const caseLinks=read('.local/fractal-links.json');
 for(let link of caseLinks){try{const url='https://www.fractal-design.com'+link;let h=get(url);let visible=h.replace(/<script[\s\S]*?<\/script>/g,'').replace(/<style[\s\S]*?<\/style>/g,'').replace(/<[^>]*>/g,'\n').replace(/&quot;/g,'"').replace(/&amp;/g,'&').split(/\n/).map(x=>x.trim()).filter(Boolean);
  const value=key=>{let i=visible.findIndex(x=>x.toLowerCase().trim()===key.toLowerCase());return i<0?'':visible[i+1]};
  let sku=value('SKU'),mb=forms(value('Motherboard compatibility')),psu=value('PSU compatibility').split(/[,/]/).map(s=>s.trim()).filter(Boolean);let gpu=Number(value('GPU max length').match(/\d+/)?.[0]);let ssd=Number(value('Dedicated 2.5" drive mounts')||value('Dedicated 2.5" drive mounts')||0),hdd=Number(value('Dedicated 3.5" drive mounts')||0),combo=Number(value('Combined 3.5"/2.5" drive mounts')||0);
  let title=clean(h.match(/<title>([\s\S]*?)<\/title>/)?.[1]||link.split('/').at(-2)).replace(/\s*[|–-]\s*Fractal.*$/i,'');
  if(!sku||!mb.length||!psu.length||!gpu)throw Error(JSON.stringify({sku,mb,psu,gpu,ssd,hdd,combo}));
  const fanText=value('Fans included');let fans=[...fanText.matchAll(/(\d+)\s*x/gi)].reduce((a,m)=>a+Number(m[1]),0);
  add('CASE',sku,'Fractal Design '+title,{supportedMotherboardForms:mb,supportedPsuForms:psu,maxGpuLengthMm:gpu,driveBays:{'2.5':ssd,'3.5':hdd+combo},estimatedPowerW:fans*3},url,'Учебная оценка вентиляторов корпуса: '+fans+' × 3 Вт; для корпуса без штатных вентиляторов 0 Вт.',{motherboard:value('Motherboard compatibility'),psu:value('PSU compatibility'),gpuMaxLength:value('GPU max length'),ssd,hdd,combinedMounts:combo,fans:fanText},'Комбинированные места выделены только под 3.5 для исключения двойного учёта. Габариты GPU зависят от расположения радиаторов/корзин; дополнительные режимы не моделируются.');
 }catch(e){rejected.push({category:'CASE',sku:link,reason:e.message});}}
 for(let p of read('.local/corsair-case-products.json').filter(x=>x.sku?.startsWith('CC-'))){if(!fs.existsSync(cache('https://www.corsair.com/us/en/p/pc-cases/'+p.sku.toLowerCase()+'/'+p.url_key)))continue;try{let {url,html,specs:s,name}=corsair(p,'pc-cases');let mb=forms(s['Case Supported']||s['Case Motherboard Support']||'');let gpu=Number((s['Maximum GPU Length']||'').match(/\d+/)?.[0]);let psu=s['Case Power Supply']||'ATX';let text=clean(html);
  const mounts=(keys)=>{for(let k of keys)if(s[k]!=null)return Number(s[k].match(/\d+/)?.[0]);return null;};let ssd=mounts(['Case Drive Bays 2.5"','Case Drive Bays 2.5','2.5" Drive Bays']),hdd=mounts(['Case Drive Bays 3.5"','Case Drive Bays 3.5','3.5" Drive Bays']);
  if(ssd==null)ssd=Number(text.match(/(\d+)\s*x?\s*2\.5[”"″]\s*(?:SSD|drive)/i)?.[1]||0);if(hdd==null)hdd=Number(text.match(/(\d+)\s*x?\s*3\.5[”"″]\s*(?:HDD|drive)/i)?.[1]||0);
  if(!mb.length||!gpu)throw Error(JSON.stringify(s));
  add('CASE',p.sku,'CORSAIR '+name+' ['+p.sku+']',{supportedMotherboardForms:mb,supportedPsuForms:psu.split(/[,|]/).map(x=>x.trim()),maxGpuLengthMm:gpu,driveBays:{'2.5':ssd,'3.5':hdd},estimatedPowerW:6},url,'Учебная оценка корпусных вентиляторов 6 Вт. Фактическая мощность зависит от комплектации и оборотов; это не измерение.',s,'Учтены только явно описанные отдельные места накопителей; если число не опубликовано в карточке, места консервативно не заявлены. Тепловой режим, толщина GPU и расположение радиаторов не проверяются.');
 }catch(e){rejected.push({category:'CASE',sku:p.sku,reason:e.message});}}
 for(let p of read('.local/ssd-products.json').filter(x=>x.sku?.startsWith('CSSD-'))){try{let {url,specs:s,name}=corsair(p,'data-storage');if(!/M\.2|NVMe/i.test(name+' '+Object.values(s).join(' ')))throw Error('Not internal M2');add('STORAGE',p.sku,'CORSAIR '+name+' ['+p.sku+']',{storageInterface:'M2',driveForm:'M2',estimatedPowerW:8},url,'Учебная оценка мощности SSD под нагрузкой: 8 Вт. Не измерение и не паспортная мощность.',s,'Все позиции — внутренние M.2 SSD; длина 2230/2242/2280, поколение PCIe и охлаждение пока не моделируются.');}catch(e){rejected.push({category:'STORAGE',sku:p.sku,reason:e.message});}}
 for(const p of read('data/manual-facts.json'))add(p.category,p.sku,p.name,p.specs,p.sourceUrl,p.powerBasis,p.facts,'Проверено по официальной спецификации производителя.'); fs.mkdirSync('data',{recursive:true});fs.writeFileSync('.local/catalog-rejected.json',JSON.stringify(rejected,null,2));
 let counts=Object.fromEntries(['CPU','MOTHERBOARD','RAM','GPU','PSU','CASE','STORAGE'].map(c=>[c,results.filter(x=>x.input.category===c).length]));console.log('COUNTS',counts);console.log('REJECTED',rejected.length);
 // Keep every verified candidate for audit; final seed is generated only after the minimum is satisfied.
 fs.writeFileSync('.local/catalog-candidates.json',JSON.stringify(results,null,2));fs.writeFileSync('.local/source-candidates.json',JSON.stringify(sources,null,2));
 if(Object.values(counts).some(n=>n<50))throw Error('Fewer than 50 verified models in a category; inspect .local/catalog-rejected.json');
 fs.writeFileSync('data/catalog.json',JSON.stringify(results,null,2)+'\n');fs.writeFileSync('data/sources.json',JSON.stringify(sources,null,2)+'\n');
}
main().catch(e=>{console.error(e);process.exitCode=1});

