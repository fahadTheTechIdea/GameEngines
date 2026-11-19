const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_ROUTE_JUNCTIONS;
if(!root)throw new Error('WORLD_ROUTE_JUNCTIONS must identify the generated junction fixture');
const read=file=>JSON.parse(fs.readFileSync(path.join(root,file),'utf8'));
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-7,`${a} != ${b}`);
const distance=(a,b)=>Math.hypot(b.X-a.X,b.Y-a.Y);
// An independent rectangle/affine oracle: split at every patch edge, integrate
// each winning plane, then account for jumps between intervals AND path legs.
// It neither calls the engine nor implements its visibility-graph search.
function evaluate(surface,profile,points){
  let length=0,ascent=0,descent=0,previous=null;
  for(let i=1;i<points.length;i++){
    const a=points[i-1],b=points[i],cuts=[0,1];
    for(const patch of surface.Patches){
      const xs=patch.Boundary.Vertices.map(p=>p.X),ys=patch.Boundary.Vertices.map(p=>p.Y);
      for(const [start,end,bounds] of [[a.X,b.X,[Math.min(...xs),Math.max(...xs)]],[a.Y,b.Y,[Math.min(...ys),Math.max(...ys)]]]){
        if(start!==end)for(const edge of bounds){const t=(edge-start)/(end-start);if(t>0&&t<1)cuts.push(t);}
      }
    }
    const sorted=[...new Set(cuts)].sort((a,b)=>a-b),at=t=>({X:a.X+(b.X-a.X)*t,Y:a.Y+(b.Y-a.Y)*t});
    for(let j=1;j<sorted.length;j++){
      const lo=at(sorted[j-1]),hi=at(sorted[j]),mid=at((sorted[j-1]+sorted[j])/2);
      const patch=surface.Patches.filter(p=>{
        const xs=p.Boundary.Vertices.map(p=>p.X),ys=p.Boundary.Vertices.map(p=>p.Y);
        return mid.X>=Math.min(...xs)&&mid.X<=Math.max(...xs)&&mid.Y>=Math.min(...ys)&&mid.Y<=Math.max(...ys);
      }).sort((a,b)=>b.Priority-a.Priority)[0];
      if(!patch||!profile.KindCosts[patch.Kind]||Math.hypot(patch.GradientX,patch.GradientY)>profile.MaximumSlope)return null;
      const height=p=>patch.Elevation+(p.X-patch.Origin.X)*patch.GradientX+(p.Y-patch.Origin.Y)*patch.GradientY;
      const start=height(lo),end=height(hi),jump=previous===null?0:start-previous;
      if(Math.abs(jump)>profile.MaximumHeightStep+1e-8)return null;
      length+=distance(lo,hi);
      ascent+=Math.max(0,jump)+Math.max(0,end-start);descent+=Math.max(0,-jump)+Math.max(0,start-end);previous=end;
    }
  }
  return {length,ascent:ascent*profile.AscentCost,descent:descent*profile.DescentCost};
}
function checkCost(result,cost){assert.ok(cost);near(result.Cost.Distance,cost.length);near(result.Cost.Terrain,cost.length);near(result.Cost.Ascent,cost.ascent);near(result.Cost.Descent,cost.descent);}
test('a full height bank is not traversable by splitting its edge',()=>{
  const f=read('barrier.json');assert.notEqual(f.Result.Status,'Found');
  assert.equal(evaluate(f.Surface,f.Request.Profile,[f.Request.From,{X:40,Y:10},f.Request.To]),null);
});
test('saved ramp path matches exhaustive simple paths in the declared five-vertex graph',()=>{
  const f=read('ramp.json'),vertices=[f.Request.From,{X:40,Y:10},{X:29,Y:16},{X:41,Y:16},f.Request.To];
  const links=[[0,1],[0,2],[2,3],[3,1],[1,4]],paths=[];
  function visit(ids){
    const last=ids.at(-1);if(last===4){const points=ids.map(i=>vertices[i]),cost=evaluate(f.Surface,f.Request.Profile,points);if(cost)paths.push({points,cost});return;}
    for(const [a,b] of links){const next=a===last?b:b===last?a:null;if(next!==null&&!ids.includes(next))visit([...ids,next]);}
  }
  visit([0]);assert.equal(paths.length,1);assert.deepEqual(f.Result.Path.Points,paths[0].points);
  assert.deepEqual(f.Compiled.Path,f.Result.Path);checkCost(f.Result,paths[0].cost);checkCost(f.Compiled,paths[0].cost);
  near(paths[0].cost.ascent,20);assert.deepEqual(f.Reverse.Path.Points,[...paths[0].points].reverse());
  checkCost(f.Reverse,evaluate(f.Surface,f.ReverseRequest.Profile,f.Reverse.Path.Points));near(f.Reverse.Cost.Descent,40);
});
test('permitted step costs survive insertion of a path vertex',()=>{
  const f=read('step-cost.json');
  for(const result of [f.Straight,f.Split,f.Searched]){assert.equal(result.Status,'Found');checkCost(result,evaluate(f.Surface,f.Request.Profile,result.Path.Points));near(result.Cost.Ascent,.005);}
  near(f.Straight.Cost.Terrain+f.Straight.Cost.Ascent,f.Split.Cost.Terrain+f.Split.Cost.Ascent);
});
test('published metadata retains both independently traversable required directions',()=>{
  const world=read('world/world-metadata.json'),recipe=read('recipe.json');
  assert.equal(world.Nodes.length,3);assert.equal(world.RoutePlan.Routes.length,1);
  const route=world.RoutePlan.Routes[0];
  for(const result of [route.Forward,route.Reverse]){assert.equal(result.Status,'Found');checkCost(result,evaluate(recipe.Surface,recipe.RouteRules[0].Profile,result.Path.Points));}
  assert.equal(fs.existsSync(path.join(root,'blocked-world')),false);
});
