const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_OWNERSHIP_DENSITY_ROOT||'generated/coordinate-density-owner-final-20261102';
const read=n=>JSON.parse(fs.readFileSync(path.join(root,n,'world-regions.json'),'utf8'));
for(const name of ['world','reversed','piecewise','source-size','more','session','irregular'])test(`${name}: density-owned complete world remains interpretable`,()=>{
 const w=read(name),before=JSON.stringify(w),p=R.layout(w),more=name==='more',o=w.Definition.OwnershipPlan;
 assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,more?87:48);assert.equal(p.views.length,3);
 assert.equal(w.Regions.reduce((sum,r)=>sum+r.World.AccessPlan.Sites.length,0),more?84:45);
 assert.equal(o.Source.Pattern.DensityFieldId,'demand');assert.ok(o.Source.OwnershipEnvironment.Fields.some(f=>f.Id==='demand'));
 const mass=name==='irregular'?75264*1.5-56*44*(1+308/336):75264*(name==='piecewise'?1.085:1.5);
 assert.ok(Math.abs(o.Partition.Mass-mass)<1e-5);
 assert.equal(p.views.reduce((sum,v)=>sum+v.plan.populationDomains.length,0),more?18:12);
 for(const v of p.views){assert.equal(v.plan.groundCover.UnresolvedArea,0);assert.deepEqual(v.packet.World.Surface,v.packet.World.PlacementPlan.Definition.SourceWorld.Surface);}
});
test('ownership measure requires explicit selected field and environment',()=>{
 for(const change of [s=>delete s.OwnershipEnvironment,s=>s.OwnershipEnvironment.Fields=[],s=>s.Pattern.DensityFieldId='absent',s=>s.Pattern.DensityFieldId=null]){
  const w=read('world');change(w.Definition.OwnershipPlan.Source);assert.throws(()=>R.layout(w),/ownership derivation/);
 }
});
test('positive mass and original weighted shares remain hard structural checks',()=>{
 for(const change of [a=>a.Mass=0,a=>a.Mass=-1,a=>a.Strata[0].Mass++,a=>a.Strata[0].Area=1]){
  const w=read('world');change(w.Definition.OwnershipPlan.Partition);assert.throws(()=>R.layout(w),/ownership derivation/);
 }
});
test('different demand fields change actual program poses with every member retained',()=>{
 const a=read('world'),b=read('reversed');assert.notDeepEqual(a.Definition.Regions.map(r=>r.Ownership),b.Definition.Regions.map(r=>r.Ownership));
 for(let i=0;i<3;i++){
  assert.notDeepEqual(a.Regions[i].World.Nodes.map(n=>n.Position),b.Regions[i].World.Nodes.map(n=>n.Position));
  assert.deepEqual(a.Regions[i].World.Nodes.map(n=>[n.Id,n.ArchetypeId,n.ParentId]),b.Regions[i].World.Nodes.map(n=>[n.Id,n.ArchetypeId,n.ParentId]));
 }
});
