const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_SEEDED_BLUEPRINT_ROOT;
if(!root)throw Error('WORLD_SEEDED_BLUEPRINT_ROOT is required');
const read=(dir,file)=>JSON.parse(fs.readFileSync(path.join(root,dir,file),'utf8'));
const a=read('world-a','world-regions.json'),b=read('world-b','world-regions.json');
const ra=read('world-a','world-design.json'),rb=read('world-b','world-design.json');
const area=p=>{const v=p.Vertices,o=v[0];let n=0;for(let i=1;i+1<v.length;i++)n+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(n)/2;};
const close=(x,y)=>assert.ok(Math.abs(x-y)<1e-7*Math.max(1,Math.abs(x),Math.abs(y)));
test('one complete authored program and distinct explicit seed streams',()=>{
 assert.deepEqual(ra.Source.Slots,[]);assert.deepEqual(ra.Source.Requirements,[]);
 const aa=structuredClone(ra.Source),bb=structuredClone(rb.Source);
 assert.notEqual(aa.SeedPlan.Seed,bb.SeedPlan.Seed);aa.SeedPlan.Seed=bb.SeedPlan.Seed;assert.deepEqual(aa,bb);
 for(const receipt of [ra,rb]){
  assert.equal(receipt.Attempts.length,1);assert.ok(receipt.Attempts[0].Accepted);assert.deepEqual(receipt.Attempts[0].Selections,[]);
  assert.deepEqual(receipt.SeedDerivations.map(d=>d.Target).sort(),['ownership-partition','terrain-detail']);
 }
 assert.notDeepEqual(a.Definition.Regions.map(r=>r.Ownership),b.Definition.Regions.map(r=>r.Ownership));
 const x=a.Regions[0].World.Surface.TerrainCapturePlan.Window,y=b.Regions[0].World.Surface.TerrainCapturePlan.Window;
 let compared=0,different=0;
 for(let row=Math.max(x.FirstRow,y.FirstRow);row<Math.min(x.FirstRow+x.Rows,y.FirstRow+y.Rows);row++)
  for(let col=Math.max(x.FirstColumn,y.FirstColumn);col<Math.min(x.FirstColumn+x.Columns,y.FirstColumn+y.Columns);col++){
   const ax=x.Heights[(row-x.FirstRow)*x.Columns+col-x.FirstColumn],by=y.Heights[(row-y.FirstRow)*y.Columns+col-y.FirstColumn];
   compared++;if(Math.abs(ax-by)>1e-9)different++;
  }
 assert.ok(compared>0&&different>0,'actual terrain differs on shared original lattice samples');
});
for(const [name,world] of [['world-a',a],['world-b',b]])test(`${name} retains physical sources and connected two-way journeys`,()=>{
 assert.equal(R.layout(world).boxes.length,20);assert.equal(world.Regions.length,4);
 const graph=new Map(world.Regions.map(r=>[r.RegionId,[]]));let sites=0;
 for(const region of world.Regions){const local=region.World,nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
  for(const site of local.AccessPlan.Sites){const node=nodes.get(site.NodeId),source=local.FootprintDefinitions[node.ArchetypeId];assert.ok(source?.Polygon);close(area(site.Footprint),area(source.Polygon));sites++;}}
 assert.equal(sites,12);assert.equal(world.Connections.length,3);
 for(const link of world.Connections){graph.get(link.Definition.From.RegionId).push(link.Definition.To.RegionId);graph.get(link.Definition.To.RegionId).push(link.Definition.From.RegionId);
  for(const [route,start,end] of [[link.Forward,link.From,link.To],[link.Reverse,link.To,link.From]]){
   assert.equal(route.Status,'Found');assert.deepEqual(route.Path.Points[0],start);assert.deepEqual(route.Path.Points.at(-1),end);}}
 const seen=new Set([world.Regions[0].RegionId]),todo=[...seen];for(let i=0;i<todo.length;i++)for(const x of graph.get(todo[i]))if(!seen.has(x)){seen.add(x);todo.push(x);}
 assert.equal(seen.size,4);
});
