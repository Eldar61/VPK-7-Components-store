import fs from 'node:fs';import assert from 'node:assert/strict';
const rows=JSON.parse(fs.readFileSync('data/catalog.json','utf8')),sources=JSON.parse(fs.readFileSync('data/sources.json','utf8'));
const ids=new Set(),skus=new Set();const counts={};
for(const r of rows){assert.ok(!ids.has(r.id));ids.add(r.id);assert.ok(!skus.has(r.input.sku));skus.add(r.input.sku);counts[r.input.category]=(counts[r.input.category]||0)+1;assert.ok(r.input.priceRub>=0);assert.ok(Number.isInteger(r.input.stockQuantity)&&r.input.stockQuantity>=0);assert.ok(r.input.sourceUrl.startsWith('https://'));assert.ok(r.input.powerBasis);assert.ok(sources.some(s=>s.productId===r.id&&s.sku===r.input.sku));if(r.input.category!=='PSU')assert.ok(r.input.specs.estimatedPowerW>=0);else assert.ok(r.input.specs.ratedPowerW>0);if(r.input.category==='RAM')assert.ok(!/K[24][/-]/.test(r.input.sku),'Kits cannot masquerade as individual DIMMs');}
for(const c of ['CPU','MOTHERBOARD','RAM','GPU','PSU','CASE','STORAGE'])assert.ok(counts[c]>=50,c);
assert.equal(sources.length,rows.length);
const build=JSON.parse(fs.readFileSync('docs/example-build.json','utf8'));assert.equal(new Set(build.items.map(i=>i.category)).size,7);assert.ok(build.items.every(i=>ids.has(i.productId)));
console.log('PASS catalog: '+rows.length+' unique real SKUs, sources, example IDs and >=50/category');console.log(counts);

