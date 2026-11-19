const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_CONNECTION_GATE;
if(!root)throw new Error('WORLD_CONNECTION_GATE must identify the current four-terrain publication');
const read=(id,file)=>JSON.parse(fs.readFileSync(path.join(root,id,file),'utf8'));
const ids=['flat','hillside','coastline','valley-pass'];
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6,`${a} != ${b}`);
const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
function distance(p,a,b){const dx=b.X-a.X,dy=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));return Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy);}
const edges=points=>points.map((p,i)=>[p,points[(i+1)%points.length]]);
function inside(p,polygon){let result=false;for(const[a,b]of edges(polygon)){if(distance(p,a,b)<1e-8)return true;if((a.Y>p.Y)!==(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)result=!result;}return result;}
function separation(a,b,c,d){if(cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0)return 0;return Math.min(distance(a,c,d),distance(b,c,d),distance(c,a,b),distance(d,a,b));}
function avoids(a,b,polygon,radius){assert.ok(!inside(a,polygon)&&!inside(b,polygon),'centerline cannot enter forbidden polygon');for(const[c,d]of edges(polygon))assert.ok(separation(a,b,c,d)>=radius-1e-6,'full-width passage must clear forbidden polygon');}
function steepTriangles(f,limit){
 const result=[];
 for(let y=0;y<f.Rows-1;y++)for(let x=0;x<f.Columns-1;x++){
  const h=(u,v)=>f.Heights[(y+v)*f.Columns+x+u],p=(u,v)=>({X:f.Origin.X+(x+u)*f.StepX,Y:f.Origin.Y+(y+v)*f.StepY});
  if(Math.hypot((h(1,0)-h(0,0))/f.StepX,(h(1,1)-h(1,0))/f.StepY)>limit+1e-8)result.push([p(0,0),p(1,0),p(1,1)]);
  if(Math.hypot((h(1,1)-h(0,1))/f.StepX,(h(0,1)-h(0,0))/f.StepY)>limit+1e-8)result.push([p(0,0),p(1,1),p(0,1)]);
 }
 return result;
}
for(const id of ids){
 test(`${id}: complete unchanged source geometry and all six exact journeys`,()=>{
  const w=read(id,'world/world-metadata.json'),catalog=read(id,'catalog.json'),view=Schematic.layout(w);
  assert.equal(w.Nodes.length,9);assert.equal(w.AccessPlan.Sites.length,8);assert.equal(w.RoutePlan.Routes.length,3);assert.equal(view.routes.length,6);
  for(const site of w.AccessPlan.Sites){
   const node=w.Nodes.find(n=>n.Id===site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon.Vertices;
   assert.equal(site.Footprint.Vertices.length,source.length);
   for(let i=0;i<source.length;i++){near(site.Footprint.Vertices[i].X,source[i].X+node.Position[0]);near(site.Footprint.Vertices[i].Y,source[i].Y+node.Position[1]);}
  }
  for(const r of w.RoutePlan.Routes)for(const result of [r.Forward,r.Reverse]){
   assert.equal(result.Status,'Found');assert.deepEqual(view.routes.find(v=>v.id===result.Id).points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
  }
  const html=fs.readFileSync(path.join(root,id,'world/schematic.html'),'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
  assert.equal(fs.readFileSync(path.join(root,id,'world/schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 });
 test(`${id}: independent continuous width, solid, water and terrain-slope clearance`,()=>{
  const w=read(id,'world/world-metadata.json');
  for(const r of w.RoutePlan.Routes){
   const profile=w.RoutePlan.Rules.find(rule=>rule.Id===r.RuleId).Profile;
   const forbidden=[...w.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').map(s=>s.Footprint.Vertices),
    ...w.Surface.Exclusions.map(e=>e.Boundary.Vertices),
    ...w.Surface.Patches.filter(p=>profile.KindCosts[p.Kind]===undefined).map(p=>p.Boundary.Vertices),
    ...steepTriangles(w.Surface.HeightField,profile.MaximumSlope)];
   for(const result of [r.Forward,r.Reverse])for(let i=1;i<result.Path.Points.length;i++){
    const a=result.Path.Points[i-1],b=result.Path.Points[i];
    assert.ok(inside(a,w.Surface.Boundary.Vertices)&&inside(b,w.Surface.Boundary.Vertices));
    for(const[c,d]of edges(w.Surface.Boundary.Vertices))assert.ok(separation(a,b,c,d)>=profile.Radius-1e-6);
    for(const polygon of forbidden)avoids(a,b,polygon,profile.Radius);
   }
  }
 });
}
test('only terrain changes at fixed complete program, source poses and endpoints',()=>{
 const baseline=read('flat','world/world-metadata.json'),source=read('flat','recipe.json');delete source.Surface;
 for(const id of ids.slice(1)){
  const w=read(id,'world/world-metadata.json'),input=read(id,'recipe.json');delete input.Surface;assert.deepEqual(input,source);
  assert.deepEqual(w.Nodes,baseline.Nodes);assert.deepEqual(w.Connections,baseline.Connections);assert.deepEqual(w.AccessPlan.Sites,baseline.AccessPlan.Sites);
  assert.ok(w.RoutePlan.Rules.every(r=>r.Search.Waypoints.length===0));
 }
 const trail=w=>w.RoutePlan.Routes.find(r=>r.RuleId==='trail');
 assert.notDeepEqual(trail(baseline).Forward.Path,trail(read('coastline','world/world-metadata.json')).Forward.Path);
 assert.notDeepEqual(trail(baseline).Forward.Path,trail(read('valley-pass','world/world-metadata.json')).Forward.Path);
});
test('hillside directed ascent/descent and planar/terrain components have independent analytic costs',()=>{
 const w=read('hillside','world/world-metadata.json');
 for(const r of w.RoutePlan.Routes){const profile=w.RoutePlan.Rules.find(x=>x.Id===r.RuleId).Profile;
  for(const result of [r.Forward,r.Reverse]){let length=0,up=0,down=0;
   for(let i=1;i<result.Path.Points.length;i++){const a=result.Path.Points[i-1],b=result.Path.Points[i],delta=(b.X-a.X)*.04;length+=Math.hypot(b.X-a.X,b.Y-a.Y);up+=Math.max(0,delta)*profile.AscentCost;down+=Math.max(0,-delta)*profile.DescentCost;}
   near(result.Cost.Distance,length);near(result.Cost.Terrain,length);near(result.Cost.Ascent,up);near(result.Cost.Descent,down);
   assert.equal(result.Cost.ReusedDistance,0);assert.equal(result.Cost.NewPassage,0);assert.equal(result.Cost.Guidance??0,0);
  }
 }
});
