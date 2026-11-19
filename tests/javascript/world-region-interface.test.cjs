const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_REGION_INTERFACE_PACKET||'generated/region-interface-route-release-cli-world';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
const connection='region-west/explicit/region-gate';
test('actual region interface, exact source content and bidirectional route are retained',()=>{
 const view=Schematic.layout(world),entry=view.interfaceRoutes.find(x=>x.connectionId===connection);
 assert.ok(entry);assert.equal(entry.InterfaceId,'region-gate');assert.equal(entry.FromDomainId,'alpha');assert.equal(entry.ToDomainId,'beta');
 assert.equal(world.Nodes.length,11);assert.equal(world.AccessPlan.Sites.length,9);
 const source=world.RegionPlan.IntentPlan.Relations.find(x=>x.Requirement.Id==='region-gate');
 assert.deepEqual(entry.Span,source.SharedSpan);assert.ok(entry.Forward&&entry.Reverse);
 for(const direction of ['Forward','Reverse']){
  const saved=world.RoutePlan.Routes.find(x=>x.ConnectionId===connection)[direction];
  assert.equal(saved.SearchModel,'domain-interface-v1');
  assert.deepEqual(view.routes.find(x=>x.id===saved.Id).points,saved.Path.Points.map(p=>({x:p.X,y:p.Y})));
 }
});
test('mismatched region, source and crossing witnesses are rejected',()=>{
 for(const mutate of [
  w=>w.RoutePlan.Routes.find(r=>r.ConnectionId===connection).Interface.ToDomainId='gamma',
  w=>w.RoutePlan.Routes.find(r=>r.ConnectionId===connection).Interface.Center.X+=2,
  w=>w.RoutePlan.Rules.find(r=>r.Id==='region-interface').Interfaces[0].Source='LocalAllocation',
  w=>w.RegionPlan.IntentPlan.Relations.find(r=>r.Requirement.Id==='region-gate').SharedSpan.From.X+=2,
  w=>w.RoutePlan.Routes.find(r=>r.ConnectionId===connection).Interface.Reverse=null]){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy));
 }
});
