const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_NETWORK_POLICY_ROOT||'generated/network-policies-final';
const read=mode=>JSON.parse(fs.readFileSync(`${root}/${mode}/cli/world-metadata.json`,'utf8'));
for(const mode of ['baseline','water','roadless','renamed'])test(`${mode}: scoped data policies retain source profiles, all parts and exact paths`,()=>{
 const world=read(mode),before=JSON.stringify(world),view=Schematic.layout(world);
 assert.equal(world.Nodes.length,24);assert.equal(view.boxes.length,24);assert.equal(world.AccessPlan.Sites.length,21);
 assert.equal(view.interfaceRoutes.length,3);
 const directions=world.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).concat(world.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse])));
 assert.equal(view.routes.length,directions.length);
 for(const route of directions){
  const rendered=view.routes.find(r=>r.id===route.Id);assert.ok(rendered);
  assert.deepEqual(rendered.points,route.Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.guidanceCost||0,route.Cost.Guidance||0);
 }
 assert.equal(Schematic.routeGuidance(world).length,mode==='roadless'?1:2);
 assert.equal(world.NetworkPlan.Networks.length,mode==='roadless'?1:2);
 if(mode==='roadless')assert.equal(world.Connections.filter(c=>c.Kind==='street').length,0);
 else {
  const network=world.NetworkPlan.Networks.find(n=>n.RuleId==='planned-supply').Plan;
  assert.equal(network.Destinations.length,4);assert.deepEqual(network.Analysis.Bridges,[]);assert.equal(network.Options.Profile.Radius,.6);
 }
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${mode}/cli/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
 assert.deepEqual(fs.readFileSync(`${root}/${mode}/cli/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 assert.equal(JSON.stringify(world),before);
});
test('invalid policy/route receipts reject schematic consumption',()=>{
 for(const mutate of [w=>w.NetworkPlan.Networks[0].Plan.Status='CandidateGraphDisconnected',
  w=>w.NetworkPlan.Networks[0].Plan.Options.Profile.Radius=0,
  w=>w.NetworkPlan.Rules[0].ConnectionKind='wrong-kind',
  w=>w.NetworkPlan.Rules[1].Id=w.NetworkPlan.Rules[0].Id,
  w=>w.NetworkPlan.Networks[0].Plan.Edges[0].Forward.Path.Points[0].X+=1]){
  const copy=read('baseline');mutate(copy);assert.throws(()=>Schematic.layout(copy),/network|route|connection/i);
 }
});
