const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_PARTITION_PINS_ROOT;
assert.ok(root,'Set WORLD_REGION_PARTITION_PINS_ROOT to the actual public/test packet directory.');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const name of ['analytic','mixed','rotated'])test(`${name}: fixed and automatic shared-component anchors reach exact inspection`,()=>{
 const p=read(name+'.json'),seeds=S.regionIntentSeeds(p);
 assert.equal(seeds.length,2);assert.equal(new Set(p.IntentPlan.Selections.map(s=>s.Component)).size,1);
 for(const i of p.Request.Intents.filter(i=>i.RequiredPosition!=null))assert.deepEqual(seeds.find(s=>s.Id===i.Id).Position,i.RequiredPosition);
 for(const mutate of [p=>p.Request.Intents.find(i=>i.RequiredPosition!=null).RequiredPosition.X++,
  p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Interior.UpperBound++,
  p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Interior.Evaluations++,
  p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Position.X++,
  p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Interior.Clearance=0]){
  const changed=structuredClone(p);mutate(changed);assert.throws(()=>S.regionIntentSeeds(changed),/Invalid region/);
 }
});
for(const name of ['world','edited'])test(`${name}: all complete fixed-location programs and source geometry are retained`,()=>{
 const w=read(name+'/world-metadata.json'),view=S.layout(w),plan=S.generatedRegions(w);
 assert.equal(view.boxes.length,11);assert.equal(w.AccessPlan.Sites.length,9);assert.equal(plan.Regions.length,3);
 for(const i of plan.Request.Intents)assert.deepEqual(plan.Regions.find(r=>r.Id===i.Id).Seed.Position,i.RequiredPosition);
 assert.deepEqual(fs.readFileSync(path.join(root,name,'schematic.js')),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('source boundary changes partitioned regions while preserving chosen destinations',()=>{
 const a=read('world/world-metadata.json'),b=read('edited/world-metadata.json');
 assert.deepEqual(a.RegionPlan.Regions.map(r=>r.Seed.Position),b.RegionPlan.Regions.map(r=>r.Seed.Position));
 assert.notDeepEqual(a.RegionPlan.Regions.map(r=>r.Cells),b.RegionPlan.Regions.map(r=>r.Cells));
 assert.equal(a.RegionPlan.SuitableArea,7904);assert.equal(b.RegionPlan.SuitableArea,9504);
});
