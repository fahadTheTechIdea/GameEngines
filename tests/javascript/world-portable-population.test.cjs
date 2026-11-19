const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_PORTABLE_POPULATION_ROOT;
if(!root)throw new Error('WORLD_PORTABLE_POPULATION_ROOT must identify current native publications');
const read=n=>JSON.parse(fs.readFileSync(path.join(root,n,'world-regions.json'),'utf8'));
test('terrain control blocks an observed complete compound and forces its unscaled replacement',()=>{
 const control=JSON.parse(fs.readFileSync(path.join(root,'terrain-control.json'),'utf8'));
 const before=read('world').Regions.find(r=>r.RegionId===control.TargetRegion).World;
 const after=read('terrain').Regions.find(r=>r.RegionId===control.TargetRegion).World;
 const original=before.AccessPlan.Sites.find(s=>s.NodeId===control.TargetNode).Footprint;
 const changed=after.AccessPlan.Sites.find(s=>s.NodeId===control.TargetNode).Footprint;
 assert.deepEqual(original,control.BeforeFootprint);
 const box=p=>({x0:Math.min(...p.Vertices.map(v=>v.X)),x1:Math.max(...p.Vertices.map(v=>v.X)),y0:Math.min(...p.Vertices.map(v=>v.Y)),y1:Math.max(...p.Vertices.map(v=>v.Y))});
 const a=box(original),b=box(control.BlockingVoid),c=box(changed);
 assert.ok(b.x0>a.x0&&b.x1<a.x1&&b.y0>a.y0&&b.y1<a.y1);assert.equal((b.x1-b.x0)*(b.y1-b.y0),560);
 assert.notDeepEqual(after.Nodes.find(n=>n.Id===control.TargetNode).Position,control.BeforePosition);
 assert.equal(Math.max(0,Math.min(b.x1,c.x1)-Math.max(b.x0,c.x0))*Math.max(0,Math.min(b.y1,c.y1)-Math.max(b.y0,c.y0)),0);
 assert.equal((c.x1-c.x0)*(c.y1-c.y0),(a.x1-a.x0)*(a.y1-a.y0));
 assert.deepEqual(before.Nodes.map(n=>n.Id),after.Nodes.map(n=>n.Id));assert.equal(before.AccessPlan.Sites.length,after.AccessPlan.Sites.length);
});
for(const name of ['world','terrain','source-size','more','session'])test(`${name}: complete regional source/content/context binding`,()=>{
 const w=read(name),before=JSON.stringify(w),p=R.layout(w),more=name==='more';assert.equal(JSON.stringify(w),before);
 assert.equal(p.boxes.length,more?87:48);assert.equal(p.views.length,3);assert.equal(w.Regions.reduce((sum,r)=>sum+r.World.AccessPlan.Sites.length,0),more?84:45);
 assert.equal(p.views.reduce((sum,v)=>sum+v.plan.populationDomains.length,0),more?18:12);
 for(const view of p.views){const source=view.packet.World.PlacementPlan.Definition.SourceWorld,analysis=w.Definition.OwnershipPlan.ProgramDemands.find(d=>d.RegionId===view.packet.RegionId).Analysis;
  assert.equal(analysis.SourceProgram.Nodes.length,more?5:4);assert.deepEqual(source.Surface,view.packet.World.Surface);assert.deepEqual(source.RoutePlan.Routes,view.packet.World.RoutePlan.Routes);
  assert.ok(analysis.SourceProgram.Nodes.every(n=>n.Position===null));assert.ok(view.plan.groundCover.UnresolvedArea===0);
 }
});
test('source analysis cannot omit or substitute complete actual source data',()=>{
 for(const change of [a=>a.SourceProgram=null,a=>a.SourceProgram.Nodes.pop(),a=>a.SourceProgram.SourceProgram=structuredClone(a.SourceProgram),
  a=>a.SourceProgram.CatalogSha256='0'.repeat(64),a=>a.SourceProgram.Work=a.Work+1,a=>a.SourceProgram.Connections[0].Kind='forged',
  a=>a.SourceProgram.FootprintDefinitions.compound.Polygon.Vertices[0].X++,a=>a.SourceProgram.RelativePositions.push({NodeId:'forged'})]){
  const w=read('world');change(w.Definition.OwnershipPlan.ProgramDemands[0].Analysis);assert.throws(()=>R.layout(w),/population source|population spatial/);
 }
});
test('explicit context cannot override source spatial facts or lose named anchors',()=>{
 for(const mutate of [p=>p.Context.PlacementAnchorIds.push(p.Context.PlacementAnchorIds[0]),p=>p.Context.PlacementAnchorIds[0]='missing',
  p=>p.Recipe.Origin=[0,0],p=>p.Recipe.Surface={Version:1},p=>p.Recipe.Placement.Request.Anchors.push({Id:'regional-origin',Position:{X:0,Y:0}})]){
  const w=read('world');mutate(w.Definition.Regions[0].Recipe.PopulationSource);mutate(w.Definition.OwnershipPlan.Source.Regions[0].Recipe.PopulationSource);
  assert.throws(()=>R.layout(w),/population spatial|population source anchor/);
 }
});
test('larger program content remains complete; source-size demand derives from original envelopes',()=>{
 const a=read('world'),b=read('source-size'),c=read('more');
 for(const d of a.Definition.OwnershipPlan.ProgramDemands)assert.ok(Math.abs(d.EnvelopeArea-2900)<1e-7);
 for(const d of b.Definition.OwnershipPlan.ProgramDemands)assert.ok(Math.abs(d.EnvelopeArea-3200)<1e-7);
 assert.equal(c.Regions.reduce((sum,r)=>sum+r.World.PlacementPlan.PopulationDomains.reduce((n,t)=>n+t.NodeIds.length,0),0),72);
 assert.equal(c.Connections.length,2);assert.ok(c.Connections.every(r=>r.Reverse));
});
