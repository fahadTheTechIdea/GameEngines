const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SEEDED_PARTITIONS_ROOT||'generated/seeded-partition-qualification-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/edited/world-metadata.json`,'utf8'));
test('seeded irregular world presents all declared physical content and partition geometry',()=>{
  const view=Schematic.layout(world),plan=Schematic.generatedRegions(world);
  assert.equal(view.boxes.length,11);
  assert.equal(world.AccessPlan.Sites.length,9);
  assert.equal(plan.Request.IntentPartition.Pattern.PartitionSearch.Seed,'world-1');
  const area=plan.IntentPlan.Partitions[0].AreaPlan;
  assert.ok(area.PartitionAttempts.some(a=>a.Status==='Selected'&&a.AxisOffsetDegrees===45));
  Schematic.areaPartitionWitness(area,()=>{throw Error('invalid partition');});
});
test('invalid seed and modified partition witness are refused',()=>{
  const plan=world.RegionPlan.IntentPlan.Partitions[0].AreaPlan;
  for(const mutate of [
    p=>p.Pattern.PartitionSearch.Seed=' ',
    p=>p.Pattern.PartitionSearch.Seed=42,
    p=>p.PartitionAttempts.find(a=>a.Status==='Selected').Cut+=1
  ]){
    const altered=structuredClone(plan);mutate(altered);
    assert.throws(()=>Schematic.areaPartitionWitness(altered,()=>{throw Error('invalid partition');}));
  }
});
