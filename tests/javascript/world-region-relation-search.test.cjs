const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_RELATION_SEARCH_ROOT;assert.ok(root,'Set WORLD_REGION_RELATION_SEARCH_ROOT to the actual packet folder.');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const name of ['analytic','joint','protected'])test(`${name}: saved constrained regions retain every role, assignment and actual relationship`,()=>{
 const p=read(name+'.json'),seeds=[...(p.Request.Seeds||[]),...S.regionIntentSeeds(p)];
 assert.equal(p.Regions.length,p.Request.Intents.length+(p.Request.Seeds||[]).length);
 if(p.Request.RequiredAssignment)assert.deepEqual(p.IntentPlan.Selections.map(s=>s.Component),p.Request.RequiredAssignment);
 for(const proof of p.IntentPlan.Relations){
  const r=proof.Requirement,from=seeds.find(s=>s.Id===r.FromId).Position,to=seeds.find(s=>s.Id===r.ToId).Position;
  assert.deepEqual(proof.From,from);assert.deepEqual(proof.To,to);assert.ok(Math.abs(proof.Distance-Math.hypot(from.X-to.X,from.Y-to.Y))<1e-8);
  assert.ok(proof.Distance>=r.MinimumDistance&&proof.Distance<=r.MaximumDistance);
 }
 assert.ok(p.IntentPlan.Partitions.some(part=>part.AreaPlan.PartitionAttempts.some(a=>a.Status==='Rejected')));
 if(p.Request.RequiredAssignment){const changed=structuredClone(p);changed.Request.RequiredAssignment[0]=1;assert.throws(()=>S.regionIntentSeeds(changed),/Invalid region/);}
});
for(const name of ['world','edited'])test(`${name}: full nested physical world and exact relation proof reach inspection`,()=>{
 const w=read(name+'/world-metadata.json'),view=S.layout(w),plan=S.generatedRegions(w);
 assert.equal(view.boxes.length,11);assert.equal(w.AccessPlan.Sites.length,9);assert.equal(plan.Regions.length,3);
 assert.deepEqual(fs.readFileSync(path.join(root,name,'schematic.js')),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 for(const mutate of [w=>w.RegionPlan.IntentPlan.Relations[0].Distance++,w=>w.RegionPlan.IntentPlan.Relations[0].From.X++,
  w=>w.RegionPlan.Request.IntentRelations[0].MaximumDistance=0,w=>w.RegionPlan.IntentPlan.Relations=[]]){
  const changed=structuredClone(w);mutate(changed);assert.throws(()=>S.generatedRegions(changed),/Invalid region|Invalid generated region/);
 }
});
test('relationship source edit changes region geometry while retaining complete physical program',()=>{
 const a=read('world/world-metadata.json'),b=read('edited/world-metadata.json');
 assert.notDeepEqual(a.RegionPlan.Regions.map(r=>r.Seed.Position),b.RegionPlan.Regions.map(r=>r.Seed.Position));
 assert.equal(a.RegionPlan.SuitableArea,b.RegionPlan.SuitableArea);
 assert.equal(a.AccessPlan.Sites.length,b.AccessPlan.Sites.length);
 assert.ok(a.RegionPlan.IntentPlan.Relations[0].Distance<=49);assert.ok(b.RegionPlan.IntentPlan.Relations[0].Distance>49&&b.RegionPlan.IntentPlan.Relations[0].Distance<=70);
});
