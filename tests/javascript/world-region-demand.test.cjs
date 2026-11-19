const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_DEMAND_ROOT||'generated/region-demand-native-r3';
for(const name of ['world','larger']){
 const folder=path.join(root,name),world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'),'utf8'));
 test(`${name}: exact source demand binds all regions and full nested content`,()=>{
  assert.equal(Schematic.layout(world).boxes.length,11);const p=Schematic.regionDemand(world);
  assert.equal(p.Results.length,3);assert.equal(p.Results.reduce((n,r)=>n+r.Sources.length,0),3);
  assert.equal(p.Results.find(r=>r.Rule.RegionId==='beta').RequiredArea,name==='world'?1000:1500);
  assert.equal(world.AccessPlan.Sites.length,9);
 });
 test(`${name}: missing, forged, understated or shifted source receipts refuse presentation`,()=>{
  for(const mutate of [
   w=>w.RegionDemandPlan=null,w=>w.RegionDemandId='absent',w=>w.RegionDemandPlan.Work=w.RegionDemandPlan.Definition.MaximumWork+1,
   w=>w.RegionDemandPlan.Results=[],w=>w.RegionDemandPlan.Results[0].EnvelopeArea++,
   w=>w.RegionDemandPlan.Results[0].Sources[0].Source.Polygon.Vertices[0].X++,
   w=>w.RegionDemandPlan.Results[0].Sources[0].AnchorRadius--,
   w=>w.RegionDemandPlan.Results[0].RequiredArea--,
   w=>w.RegionDemandPlan.Results[0].Sources.push(structuredClone(w.RegionDemandPlan.Results[0].Sources[0])),
   w=>w.RegionDemandPlan.DerivedRequest.Intents[0].MinimumArea=1,
   w=>w.RegionPlan.Request.Intents[0].MinimumArea=1
  ]){
   const changed=structuredClone(world);mutate(changed);
   assert.throws(()=>Schematic.generatedRegions(changed),/Invalid region source demand/);
  }
 });
}
test('composition preserves failed source choice and counts every regional request',()=>{
 const world=JSON.parse(fs.readFileSync(path.join(root,'composed/world-metadata.json'),'utf8'));
 assert.equal(Schematic.layout(world).boxes.length,11);
 const plan=world.ProgramCompositionPlan;assert.equal(plan.Attempts.length,2);assert.equal(plan.Selections,4);
 assert.deepEqual(plan.Attempts.map(a=>a.RegionRequested),[true,true]);
 assert.equal(plan.Attempts[0].Selections[0].Choices[0].ArchetypeId,'huge');
 assert.equal(plan.Attempts[1].Selections[0].Choices[0].ArchetypeId,'wide');
});
test('composition refuses a denied regional request concealed in its receipt',()=>{
 const world=JSON.parse(fs.readFileSync(path.join(root,'composed/world-metadata.json'),'utf8'));
 world.ProgramCompositionPlan.Attempts[0].RegionRequested=false;
 assert.throws(()=>Schematic.layout(world),/Invalid program composition/);
});
