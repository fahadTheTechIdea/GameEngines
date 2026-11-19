const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_COASTAL_MIXED;
if(!root)throw new Error('WORLD_COASTAL_MIXED must identify the generated coastal fixture');
const read=file=>JSON.parse(fs.readFileSync(path.join(root,file),'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6,`${a} != ${b}`);
const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
function pointDistance(p,a,b){const dx=b.X-a.X,dy=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));return Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy);}
const edges=polygon=>polygon.Vertices.map((a,i)=>[a,polygon.Vertices[(i+1)%polygon.Vertices.length]]);
function inside(p,polygon){let hit=false;for(const [a,b]of edges(polygon)){if(pointDistance(p,a,b)<1e-7)return true;if((a.Y>p.Y)!==(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)hit=!hit;}return hit;}
function segmentDistance(a,b,c,d){
  if(cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0)return 0;
  return Math.min(pointDistance(a,c,d),pointDistance(b,c,d),pointDistance(c,a,b),pointDistance(d,a,b));
}
function separated(a,b,polygon,radius){
  assert.ok(!inside(a,polygon)&&!inside(b,polygon),'path endpoints outside forbidden polygon');
  for(const[c,d]of edges(polygon))assert.ok(segmentDistance(a,b,c,d)>=radius-1e-6,'complete circular passage clears polygon');
}
function directions(world){
  return [...(world.RoutePlan?.Routes||[]).flatMap(r=>[r.Forward,r.Reverse].filter(Boolean).map(result=>({result,profile:world.RoutePlan.Rules.find(x=>x.Id===r.RuleId).Profile}))),
    ...(world.NetworkPlan?.Networks||[]).flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse].filter(Boolean).map(result=>({result,profile:n.Plan.Options.Profile}))))];
}
for(const mode of ['baseline','water','roadless']){
  test(`${mode}: exact hierarchy, source sizes and faithful schematic packet`,()=>{
    const world=read(`${mode}/world-metadata.json`),catalog=read('catalog.json'),before=JSON.stringify(world),view=Schematic.layout(world);
    assert.equal(world.Nodes.length,94);assert.equal(world.AccessPlan.Sites.length,91);assert.equal(world.PlacementPlan.ProgramInstances.length,35);
    assert.equal(world.Nodes.filter(n=>n.Kind==='building').length,28);assert.equal(world.Nodes.filter(n=>n.Kind==='equipment').length,56);
    const nodes=new Map(world.Nodes.map(n=>[n.Id,n]));
    for(const site of world.AccessPlan.Sites){
      const node=nodes.get(site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
      assert.equal(site.Footprint.Vertices.length,source.Vertices.length);
      for(let i=0;i<source.Vertices.length;i++){near(site.Footprint.Vertices[i].X,source.Vertices[i].X+node.Position[0]);near(site.Footprint.Vertices[i].Y,source.Vertices[i].Y+node.Position[1]);}
      if(node.Kind==='building')assert.equal(world.Nodes.filter(n=>n.ParentId===node.Id&&n.Kind==='equipment').length,2);
    }
    assert.equal(view.boxes.length,94);assert.equal(view.routes.length,directions(world).length);
    for(const {result}of directions(world))assert.deepEqual(view.routes.find(r=>r.id===result.Id).points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
    const html=fs.readFileSync(path.join(root,mode,'schematic.html'),'utf8');
    assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
    assert.equal(JSON.stringify(world),before);
  });
}
test('local grid stops at its district and hillside and shore keep their own geometry',()=>{
  const world=read('baseline/world-metadata.json'),plan=world.LocalLayoutPlan;
  assert.deepEqual(new Set(plan.Definition.Layouts.map(l=>l.Strategy)),new Set(['OrientedGrid','PowerDiagram']));
  for(const allocation of plan.Allocations.filter(a=>a.RegionId==='west'))for(const edge of allocation.Shape.BoundaryEdges)assert.ok(edge.From.X<=100+1e-6&&edge.To.X<=100+1e-6);
  assert.ok(world.Surface.Boundary.Vertices.some((a,i)=>{const b=world.Surface.Boundary.Vertices[(i+1)%world.Surface.Boundary.Vertices.length];return a.X!==b.X&&a.Y!==b.Y;}));
  assert.equal(world.Surface.Patches.find(p=>p.Id==='lowland').GradientX,.01);assert.equal(world.Surface.Patches.find(p=>p.Id==='hillside').GradientX,.08);
  const shore=world.Surface.Exclusions.find(x=>x.Id==='shore-reserve').Boundary;
  for(const site of world.AccessPlan.Sites){
    for(const p of site.Footprint.Vertices)assert.ok(!inside(p,shore));
    for(const[a,b]of edges(site.Footprint))separated(a,b,shore,0);
    for(const p of shore.Vertices)assert.ok(!inside(p,site.Footprint));
  }
  for(const guidance of Schematic.routeGuidance(world))assert.ok(guidance.rule.Boundary.Vertices.every(p=>p.X<=100));
  const planned=world.NetworkPlan.Networks.find(n=>n.RuleId==='planned-supply').Plan;
  for(const edge of planned.Edges)for(const route of [edge.Forward,edge.Reverse])assert.ok(route.Path.Points.every(p=>p.X+planned.Options.Profile.Radius<=100+1e-6));
  const hillside=world.NetworkPlan.Networks.find(n=>n.RuleId==='field-paths').Plan;
  for(const edge of hillside.Edges)for(const route of [edge.Forward,edge.Reverse])assert.ok(route.Path.Points.every(p=>p.X-hillside.Options.Profile.Radius>=100-1e-6));
  assert.ok(hillside.Edges.some(e=>e.Forward.Path.Points.some((p,i,ps)=>i&&Math.abs(p.X-ps[i-1].X)>1e-6&&Math.abs(p.Y-ps[i-1].Y)>1e-6)),'hillside paths retain non-grid directions');
});
for(const mode of ['baseline','water'])test(`${mode}: every full-width required direction avoids shore, water and all solid bodies`,()=>{
  const world=read(`${mode}/world-metadata.json`),forbidden=[...world.Surface.Exclusions.map(e=>e.Boundary),...world.Surface.Patches.filter(p=>p.Kind==='water').map(p=>p.Boundary),...world.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').map(s=>s.Footprint)];
  const all=directions(world);assert.ok(all.length>=70);
  for(const{result,profile}of all){
    assert.equal(result.Status,'Found');assert.ok(result.Path.Points.length>=2);
    for(let i=1;i<result.Path.Points.length;i++){
      const a=result.Path.Points[i-1],b=result.Path.Points[i];assert.ok(inside(a,world.Surface.Boundary)&&inside(b,world.Surface.Boundary));
      for(const[c,d]of edges(world.Surface.Boundary))assert.ok(segmentDistance(a,b,c,d)>=profile.Radius-1e-6,'full width inside irregular world boundary');
      for(const polygon of forbidden)separated(a,b,polygon,profile.Radius);
    }
  }
});
test('water changes the real crossing with identical source programs, and roadless input creates no roads',()=>{
  const base=read('baseline/world-metadata.json'),wet=read('water/world-metadata.json'),roadless=read('roadless/world-metadata.json');
  assert.deepEqual(wet.Nodes,base.Nodes);assert.deepEqual(wet.AccessPlan.Sites,base.AccessPlan.Sites);
  const before=base.RoutePlan.Routes.find(r=>r.RuleId==='regional-route').Interface,after=wet.RoutePlan.Routes.find(r=>r.RuleId==='regional-route').Interface;
  assert.notDeepEqual(after.Center,before.Center);assert.ok(after.Attempts.some(a=>!a.Accepted));assert.ok(after.Attempts.at(-1).Accepted);
  assert.equal(directions(roadless).length,0);assert.equal(Schematic.layout(roadless).routes.length,0);assert.equal(roadless.Connections.filter(c=>c.Kind==='street').length,0);
  assert.deepEqual(roadless.Nodes,base.Nodes);assert.deepEqual(roadless.AccessPlan.Sites,base.AccessPlan.Sites);
});
