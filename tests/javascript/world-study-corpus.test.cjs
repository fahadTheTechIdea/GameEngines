const {test}=require('node:test'),assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_STUDY_PACKET||'generated/generation-study-expanded';
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));
const report=read('study-report.json');
const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
test('study keeps every declared case and separates generated/rejected outcomes',()=>{
 const input=read('study.json');
 assert.deepEqual(report.Samples.map(s=>s.Id),input.Cases.map(c=>c.Id));
 assert.equal(report.GeneratedCases,report.Samples.filter(s=>s.Outcome==='Generated').length);
 assert.equal(report.RejectedCases,report.Samples.filter(s=>s.Outcome==='Rejected').length);
});
for(const sample of report.Samples)test(sample.Id+': complete saved world or explicit refusal reaches inspection boundary',()=>{
 const child=path.join(root,'cases','case-'+sample.Id);
 if(sample.Outcome==='Rejected'){
  assert.ok(sample.RejectionCode);assert.equal(fs.existsSync(child),false);return;
 }
 assert.equal(sample.Outcome,'Generated');
 const world=JSON.parse(fs.readFileSync(path.join(child,'world-metadata.json'),'utf8'));
 const before=JSON.stringify(world);
 assert.equal(fs.readFileSync(path.join(child,'schematic.js'),'utf8'),source);
 const S=require(path.resolve(child,'schematic.js')),view=S.layout(world);
 assert.equal(JSON.stringify(world),before);assert.equal(view.boxes.length,world.Nodes.length);
 for(const site of world.AccessPlan?.Sites||[])
  assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)),site.NodeId);
 assert.deepEqual(view.interiorAssemblies||[],world.InteriorPlan?.Results||[]);
 for(const assembly of world.InteriorPlan?.Results||[])for(const route of assembly.Routes)
  for(const direction of [route.Forward,route.Reverse].filter(Boolean)){
   const rendered=view.routes.find(r=>r.id===direction.Id);assert.ok(rendered,direction.Id);
   assert.deepEqual(rendered.points,direction.Path.Points.map(p=>({x:p.X,y:p.Y})));
  }
 const html=fs.readFileSync(path.join(child,'schematic.html'),'utf8');
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html);
 assert.ok(embedded);assert.deepEqual(JSON.parse(embedded[1]),world);
});
