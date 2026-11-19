const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_POPULATION_GATE;
if(!root)throw new Error('WORLD_POPULATION_GATE must identify the current composed population publications');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6,`${a} != ${b}`);
const edges=p=>p.Vertices.map((a,i)=>[a,p.Vertices[(i+1)%p.Vertices.length]]);
const area=p=>Math.abs(edges(p).reduce((sum,[a,b])=>sum+a.X*b.Y-a.Y*b.X,0))/2;
const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
function pointDistance(p,a,b){const dx=b.X-a.X,dy=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));return Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy);}
function inside(p,polygon){let result=false;for(const[a,b]of edges(polygon)){if(pointDistance(p,a,b)<1e-8)return true;if((a.Y>p.Y)!==(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)result=!result;}return result;}
function distance(a,b,c,d){if(cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0)return 0;return Math.min(pointDistance(a,c,d),pointDistance(b,c,d),pointDistance(c,a,b),pointDistance(d,a,b));}
function noOverlap(first,second){assert.ok(first.Vertices.every(p=>!inside(p,second)));assert.ok(second.Vertices.every(p=>!inside(p,first)));for(const[a,b]of edges(first))for(const[c,d]of edges(second))assert.ok(distance(a,b,c,d)>1e-8);}
for(const name of ['baseline','changed-seed']){
 test(`${name}: every required member/source and exact saved geometry survives composition`,()=>{
  const w=read(`${name}/world/world-metadata.json`),catalog=read(`${name}/catalog.json`),view=Schematic.layout(w);
  assert.equal(w.Nodes.length,20);assert.equal(w.AccessPlan.Sites.length,11);assert.equal(view.boxes.length,20);assert.equal(view.routes.length,4);
  assert.equal(w.Nodes.filter(n=>n.Kind==='marker').length,8);assert.equal(w.Nodes.filter(n=>n.Kind==='component').length,8);assert.equal(w.Nodes.filter(n=>n.Kind==='resource').length,1);
  for(const site of w.AccessPlan.Sites){const node=w.Nodes.find(n=>n.Id===site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
   near(area(site.Footprint),area(source));for(let i=0;i<source.Vertices.length;i++){near(site.Footprint.Vertices[i].X,source.Vertices[i].X+node.Position[0]);near(site.Footprint.Vertices[i].Y,source.Vertices[i].Y+node.Position[1]);}
  }
  for(const r of w.RoutePlan.Routes)for(const result of [r.Forward,r.Reverse])assert.deepEqual(view.routes.find(x=>x.id===result.Id).points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
  const html=fs.readFileSync(path.join(root,name,'world/schematic.html'),'utf8');assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
 });
 test(`${name}: independent full-width journeys and tagged sightline/spawn/interaction clearance`,()=>{
  const w=read(`${name}/world/world-metadata.json`),plan=w.ContextPlan;
  assert.equal(plan.Protections.length,3);assert.deepEqual(plan.Protections.find(p=>p.Definition.Id==='view').Centerline.Points,[{X:5,Y:5},{X:45,Y:5}]);
  for(const protection of plan.Protections)for(const source of plan.Sources.filter(s=>s.Tags.some(t=>protection.Definition.Tags.includes(t)))){
   const site=w.AccessPlan.Sites.find(s=>s.NodeId===source.NodeId);assert.ok(site);noOverlap(site.Footprint,protection.Boundary);if(site.Reservation)noOverlap(site.Reservation,protection.Boundary);
  }
  for(const r of w.RoutePlan.Routes){const radius=w.RoutePlan.Rules.find(x=>x.Id===r.RuleId).Profile.Radius;
   for(const result of [r.Forward,r.Reverse])for(let i=1;i<result.Path.Points.length;i++){
    const a=result.Path.Points[i-1],b=result.Path.Points[i];assert.ok(inside(a,w.Surface.Boundary)&&inside(b,w.Surface.Boundary));
    for(const[c,d]of edges(w.Surface.Boundary))assert.ok(distance(a,b,c,d)>=radius-1e-6);
    for(const site of w.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid')){assert.ok(!inside(a,site.Footprint)&&!inside(b,site.Footprint));for(const[c,d]of edges(site.Footprint))assert.ok(distance(a,b,c,d)>=radius-1e-6);}
   }
  }
 });
 test(`${name}: independent capability traversal needs the real resource and permits a return`,()=>{
  const w=read(`${name}/world/world-metadata.json`),rule=w.ProgressionPlan.Rules[0],caps=rule.Capabilities;
  assert.deepEqual(caps.CapabilityIds,['activated']);assert.equal(caps.RequireReturnToStart,true);assert.deepEqual(rule.RequiredMilestones,['population/resource-0']);
  const grants=new Map(caps.Grants.map(g=>[g.NodeId,g.CapabilityIds])),gates=new Map(caps.Gates.map(g=>[g.ConnectionId,g.RequiredCapabilityIds]));
  const graph=new Map(w.Nodes.map(n=>[n.Id,[]]));for(const c of w.Connections.filter(c=>rule.ConnectionKinds.includes(c.Kind))){graph.get(c.FromId).push([c.ToId,c.Id]);if(c.Bidirectional)graph.get(c.ToId).push([c.FromId,c.Id]);}
  function reachable(start,acquired){const queue=[[start,acquired]],seen=new Set();for(let i=0;i<queue.length;i++){const[node,active]=queue[i],key=node+'|'+active;if(seen.has(key))continue;seen.add(key);for(const[next,link]of graph.get(node)){if((gates.get(link)||[]).length&&!active)continue;queue.push([next,active||(grants.get(next)||[]).includes('activated')]);}}return seen;}
  const states=reachable(rule.StartNodeId,false);assert.ok(states.has(rule.GoalNodeIds[0]+'|true'));assert.ok(!states.has(rule.GoalNodeIds[0]+'|false'));
  for(const state of states){const split=state.lastIndexOf('|'),node=state.slice(0,split),active=state.slice(split+1)==='true';assert.ok([...reachable(node,active)].some(s=>s.startsWith(rule.StartNodeId+'|')));}
  const trace=w.ProgressionPlan.Results[0].Paths[0];assert.deepEqual(trace.NodeIds,['population/gates-0','population/resource-0','population/gates-1']);assert.deepEqual(trace.CapabilityStates,[[],['activated'],['activated']]);
 });
 test(`${name}: unresolved materials remain measured instead of becoming filler objects`,()=>{
  const w=read(`${name}/world/world-metadata.json`),g=w.GroundCoverPlan;near(area(w.Surface.Boundary),2180);
  const total=status=>g.Cells.filter(c=>c.Status===status).reduce((sum,c)=>{near(c.Area,area(c.Boundary));return sum+area(c.Boundary);},0);
  near(total('Covered'),1);near(total('Unresolved'),2179);near(total('Excluded'),0);near(g.DomainArea,2180);near(g.CoveredArea+g.UnresolvedArea+g.ExcludedArea,g.DomainArea);
  assert.deepEqual(g.Definition.Materials.map(m=>m.Id),['resource-pad']);assert.ok(w.Nodes.every(n=>n.Kind!=='garden'));
 });
}
test('seed edit and rollback preserve complete unrelated source packets and all semantic identities',()=>{
 const a=read('baseline/world/world-metadata.json'),b=read('changed-seed/world/world-metadata.json'),restored=read('session/world-metadata.json');
 assert.deepEqual(a.Nodes.map(n=>n.Id),b.Nodes.map(n=>n.Id));assert.deepEqual(a.PlacementPlan.Definition.SourceWorld,b.PlacementPlan.Definition.SourceWorld);assert.notDeepEqual(a.VariationPlan,b.VariationPlan);
 assert.deepEqual(restored,a);assert.equal(read('session/generation-session.json').Edits.length,2);
});
