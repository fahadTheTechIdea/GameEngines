const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SPIRAL_SOURCE_ROOT||'generated/spiral-source-verified-20261101';
for(const name of ['Nested','NestedUnfaced','Program','ProgramUnfaced','ProgramTwelve','Single']){
 const original=JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
 const source=w=>name==='Single'?w.PlacementPlan.TemplateDefinition.Bindings[0]:name.startsWith('Program')?w.PlacementPlan.TemplateDefinition.Programs[0].Bindings[0]:w.PlacementPlan.TemplateDefinition.NestedStages[0].Bindings[0];
 const compiled=w=>[...w.PlacementPlan.Definition.Bindings,...(w.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].filter(b=>b.Population?.Spiral);
 test(`${name}: source, parent frame and complete nested geometry agree`,()=>{
  assert.equal(S.layout(original).boxes.length,name==='Single'?2:name==='ProgramTwelve'?14:6);assert.equal(original.AccessPlan.Sites.length,name==='Single'?1:name==='ProgramTwelve'?13:5);
 });
 test(`${name}: mismatched template, compiled source and anchor refuse`,()=>{
  for(const mutate of [
   w=>source(w).Population.Spiral.StartRadius++,
   w=>delete source(w).Population.Spiral,
   w=>compiled(w)[0].Population.Spiral.RadiusStep++,
   w=>(name==='Single'?w.PlacementPlan.Definition.Request:w.PlacementPlan.Definition.NestedStages[0].Request).Anchors[0].Position.X++
  ]){const changed=structuredClone(original);mutate(changed);assert.throws(()=>S.layout(changed),/spiral population|point suitability/);}
 });
}
