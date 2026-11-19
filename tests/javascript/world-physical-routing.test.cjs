const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.PHYSICAL_ROUTING_PACKET||'generated/physical-routing-accepted';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
const added=w=>w.RoutePlan.Routes.find(r=>r.RuleId==='new-journey');
test('complete physical world adds a searched journey while keeping every source member and route',()=>{
 assert.equal(Schematic.layout(world).boxes.length,270);assert.equal(world.AccessPlan.Sites.length,269);
 assert.equal(world.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').length,257);assert.equal(world.RoutePlan.Routes.length,20);
 assert.equal(world.RoutePlan.PreservedConnectionIds.length,19);
 const source=world.PlacementPlan.Definition.SourceWorld;
 for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
 for(const route of source.RoutePlan.Routes)assert.deepEqual(world.RoutePlan.Routes.find(r=>r.ConnectionId===route.ConnectionId),route);
 assert.ok(!world.RoutePlan.PreservedConnectionIds.includes(added(world).ConnectionId));assert.ok(added(world).Forward.Path.Points.length>2);
});
test('saved winning-set receipts balance and retain the original candidate cap',()=>{
 const rule=world.RoutePlan.Rules.find(r=>r.Id==='new-journey');assert.equal(rule.Search.Objective,'LowestCost');
 for(const route of [added(world).Forward,added(world).Reverse].filter(Boolean)){
  const a=route.CandidateAudit;assert.ok(a.Proposals>rule.Search.MaximumCandidates);assert.ok(a.Retained<=rule.Search.MaximumCandidates);
  assert.equal(a.Retained,route.CandidateCount);assert.equal(a.Unexamined,0);assert.equal(a.CompleteWinningSet,true);
  assert.equal(a.Proposals,a.Retained+a.BoundPruned+a.GeometryRejected);assert.ok(a.BoundPruned>0);
  assert.ok(route.Cost.Terrain+route.Cost.Ascent+route.Cost.Descent+route.Cost.NewPassage<=a.UpperBound+1e-7);
 }
});
test('altered candidate proofs refuse rendering',()=>{
 for(const change of [a=>a.Proposals++,a=>a.Retained++,a=>a.GeometryRejected=-1,a=>a.Unexamined=1,
  a=>a.CompleteWinningSet=false,a=>a.UpperBound=null,a=>a.UpperBound=.001,a=>a.ProbeCandidates=1000]){
  const copy=structuredClone(world);change(added(copy).Forward.CandidateAudit);assert.throws(()=>Schematic.layout(copy),/route candidate audit/);
 }
});
test('packet embeds the exact saved world and current renderer',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));assert.deepEqual(JSON.parse(embedded[1]),world);
});
