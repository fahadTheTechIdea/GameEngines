const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_OFFSET_PHYSICAL_STUDY;
if(!root)throw Error('WORLD_OFFSET_PHYSICAL_STUDY is required');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const study=read('study.json'),world=read('world/world-regions.json');
function clear(route,radius){
 assert.equal(route.Status,'Found');const points=route.Path.Points;assert.ok(points.length>=2);
 for(let i=1;i<points.length;i++){
  const lo=Math.min(points[i-1].X,points[i].X),hi=Math.max(points[i-1].X,points[i].X);
  assert.ok(hi<=200-radius+1e-7||lo>=210+radius-1e-7,'route crosses the full-height terrain barrier');
 }
}
test('the complete declared matrix retains real success and refusal cases',()=>{
 assert.equal(study.Observations.length,12);
 assert.deepEqual(study.Selected,{Barrier:200,Primary:0,Alternate:-500});
 const primary=study.Observations.find(o=>o.Barrier===200&&o.Offset===0);
 const alternate=study.Observations.find(o=>o.Barrier===200&&o.Offset===-500);
 assert.equal(primary.Outcome,'PhysicalRefusal');assert.equal(primary.Failure,'METADATA_REGIONAL_PHYSICAL_COMPONENTS_EXHAUSTED');
 assert.equal(alternate.Outcome,'Generated');
 const source=read('barrier-200-offset-0-request.json'),fallback=read('fallback-request.json');
 assert.deepEqual(fallback.Pattern.PartitionSearch.AlternateBendOffsets,[-500]);
 delete fallback.Pattern.PartitionSearch.AlternateBendOffsets;assert.deepEqual(fallback,source);
});
test('physical feedback changes a declared position and validates its saved witness',()=>{
 assert.equal(R.layout(world).boxes.length,48);const f=world.Definition.OwnershipPlan.PhysicalFeedbackPlan;
 assert.ok(f.Contexts.length>=2);assert.ok(!f.Attempts[0].Accepted&&f.Attempts.at(-1).Accepted);
 assert.ok(f.Contexts[0].SelectedPartitions.every(c=>c.BendOffset===0));
 assert.ok(f.Contexts.at(-1).SelectedPartitions.some(c=>c.BendOffset===-500));
 assert.ok(f.Contexts.at(-1).RequestedPartitions.some(c=>c.BendOffset===-500));
 const forged=structuredClone(world);forged.Definition.OwnershipPlan.PhysicalFeedbackPlan.Contexts.at(-1).SelectedPartitions[0].BendOffset=999;
 assert.throws(()=>R.layout(forged),/Invalid regional/);
});
test('complete source programs and every saved route retain barrier clearance',()=>{
 assert.equal(world.Regions.length,3);assert.equal(world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0),48);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),45);
 for(const region of world.Regions){const local=region.World,source=local.PlacementPlan.Definition.SourceWorld;
  for(const node of source.Nodes)assert.deepEqual(local.Nodes.find(n=>n.Id===node.Id),node);
  const nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
  const lengths=p=>p.Vertices.map((a,i)=>{const b=p.Vertices[(i+1)%p.Vertices.length];return Math.hypot(b.X-a.X,b.Y-a.Y);}).sort((a,b)=>a-b);
  for(const site of local.AccessPlan.Sites){const node=nodes.get(site.NodeId);assert.ok(node);
   const original=local.FootprintDefinitions[node.ArchetypeId];assert.ok(original?.Polygon);
   const before=lengths(original.Polygon),after=lengths(site.Footprint);assert.equal(before.length,after.length);
   before.forEach((n,i)=>assert.ok(Math.abs(n-after[i])<1e-7*Math.max(1,n),'source footprint changed'));}
  for(const link of local.RoutePlan.Routes){const rule=local.RoutePlan.Rules.find(x=>x.Id===link.RuleId);assert.ok(rule);
   clear(link.Forward,rule.Profile.Radius);if(link.Reverse)clear(link.Reverse,rule.Profile.Radius);}}
 assert.equal(world.Connections.length,2);for(const c of world.Connections){clear(c.Forward,c.Definition.Profile.Radius);clear(c.Reverse,c.Definition.Profile.Radius);}
});
