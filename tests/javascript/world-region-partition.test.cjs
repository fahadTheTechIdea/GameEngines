const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_PARTITION_ROOT||'generated/region-partition-native-r3';
for(const name of ['world','edited']){
 const folder=path.join(root,name),world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'),'utf8'));
 test(`${name}: all seedless regions and nested physical programs reach the exact schematic`,()=>{
  const view=Schematic.layout(world),plan=Schematic.generatedRegions(world);
  assert.equal(view.boxes.length,11);assert.equal(world.AccessPlan.Sites.length,9);
  assert.equal(plan.Request.Seeds.length,0);assert.equal(Schematic.regionIntentSeeds(plan).length,3);
  assert.equal(new Set(plan.IntentPlan.Selections.map(s=>s.Component)).size,1);
  assert.equal(world.PlacementPlan.TemplateDefinition.RegionAnchors.length,3);
  assert.deepEqual(fs.readFileSync(path.join(folder,'schematic.js')),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: missing, duplicated, shifted or changed partition proof refuses presentation`,()=>{
  for(const mutate of [
   p=>p.IntentPlan.Partitions=null,
   p=>p.IntentPlan.Partitions.push(structuredClone(p.IntentPlan.Partitions[0])),
   p=>p.IntentPlan.Partitions[0].IntentIds.pop(),
   p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Position.X++,
   p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Cells[0].Vertices[0].X++,
   p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Interior.Clearance=0,
   p=>p.IntentPlan.Partitions[0].AreaPlan.RelativeWeights[0]++,
   p=>p.IntentPlan.PartitionAttempts=[],
   p=>p.IntentPlan.PartitionAttempts[0].Accepted=false,
   p=>p.IntentPlan.PartitionAttempts[0].Components[0]++,
   p=>p.IntentPlan.Selections[0].Seed.Position.X++,
   p=>p.Request.IntentPartition=null
  ]){const changed=structuredClone(world);mutate(changed.RegionPlan);assert.throws(()=>Schematic.generatedRegions(changed),/Invalid region|Invalid generated region/);}
 });
}
test('all basic region shapes consume the saved exact strata',()=>{
 for(const name of ['uniform','unequal','holed','weighted','islands','protected','related','translated']){
  const plan=JSON.parse(fs.readFileSync(path.join(root,name+'.json'),'utf8'));
  assert.equal(Schematic.regionIntentSeeds(plan).length,3);
 }
});
