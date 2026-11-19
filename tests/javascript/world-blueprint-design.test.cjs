const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_BLUEPRINT_DESIGN_ROOT;
if(!root)throw Error('WORLD_BLUEPRINT_DESIGN_ROOT is required');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const inputs=[read('design-a.json'),read('design-b.json')],receipts=[read('world-a/world-design.json'),read('world-b/world-design.json')];
const worlds=[read('world-a/world-regions.json'),read('world-b/world-regions.json')];
const area=p=>{let n=0;const v=p.Vertices,o=v[0];for(let i=1;i+1<v.length;i++)n+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(n)/2;};
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-7*Math.max(1,Math.abs(a),Math.abs(b)),`${a} != ${b}`);
test('same reusable content and constraints produce declared seeded alternatives',()=>{
 const a=structuredClone(inputs[0]),b=structuredClone(inputs[1]);assert.notEqual(a.SeedPlan.Seed,b.SeedPlan.Seed);
 a.SeedPlan.Seed=b.SeedPlan.Seed;assert.deepEqual(a,b);
 assert.equal(inputs[0].Blueprint.Template.Regions.length,2);assert.equal(inputs[0].Blueprint.Regions.length,4);
 const selected=receipts.map(r=>r.Attempts.at(-1).Selections[0].OptionId);assert.deepEqual(selected,['standard','wide']);
 for(const receipt of receipts){assert.equal(receipt.SeedDerivations.length,3);assert.ok(receipt.Attempts.at(-1).Accepted);}
 assert.notDeepEqual(worlds[0].Definition.Regions.map(r=>r.Ownership),worlds[1].Definition.Regions.map(r=>r.Ownership));
 const windows=worlds.map(w=>w.Regions[0].World.Surface.TerrainCapturePlan.Window),[x,y]=windows;let different=0;
 for(let row=Math.max(x.FirstRow,y.FirstRow);row<Math.min(x.FirstRow+x.Rows,y.FirstRow+y.Rows);row++)
  for(let col=Math.max(x.FirstColumn,y.FirstColumn);col<Math.min(x.FirstColumn+x.Columns,y.FirstColumn+y.Columns);col++)
   if(Math.abs(x.Heights[(row-x.FirstRow)*x.Columns+col-x.FirstColumn]-y.Heights[(row-y.FirstRow)*y.Columns+col-y.FirstColumn])>1e-9)different++;
 assert.ok(different>0,'different actual terrain samples on the shared original lattice');
});
test('selected content has its authored physical size and endpoint',()=>{
 const sizes=[];
 for(let i=0;i<worlds.length;i++){
  const world=worlds[i],receipt=receipts[i];assert.equal(R.layout(world).boxes.length,20);
  assert.equal(world.Regions.length,4);assert.equal(world.Connections.length,3);
  const graph=new Map(world.Regions.map(r=>[r.RegionId,[]]));let sites=0;
  for(const region of world.Regions){const local=region.World,nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
   for(const site of local.AccessPlan.Sites){const node=nodes.get(site.NodeId);assert.ok(node);const definition=local.FootprintDefinitions[node.ArchetypeId];
    assert.ok(definition?.Polygon);near(area(site.Footprint),area(definition.Polygon));sites++;}}
  assert.equal(sites,12);
  for(const link of world.Connections){graph.get(link.Definition.From.RegionId).push(link.Definition.To.RegionId);graph.get(link.Definition.To.RegionId).push(link.Definition.From.RegionId);
   for(const [route,start,end] of [[link.Forward,link.From,link.To],[link.Reverse,link.To,link.From]]){
    assert.equal(route.Status,'Found');assert.deepEqual(route.Path.Points[0],start);assert.deepEqual(route.Path.Points.at(-1),end);}}
  const seen=new Set([world.Regions[0].RegionId]),todo=[...seen];for(let j=0;j<todo.length;j++)for(const id of graph.get(todo[j]))if(!seen.has(id)){seen.add(id);todo.push(id);}
  assert.equal(seen.size,4);
  const endpoint=receipt.Attempts.at(-1).BoundEndpoints.find(e=>e.DestinationId==='arrival');assert.equal(endpoint.RegionId,'arrival');
  const arrival=world.Regions.find(r=>r.RegionId==='arrival').World,node=arrival.Nodes.find(n=>n.Id===endpoint.NodeId);assert.equal(node.Kind,'compound');
  const site=arrival.AccessPlan.Sites.find(s=>s.NodeId===node.Id);assert.ok(site);sizes.push(area(site.Footprint));
 }
 assert.notEqual(sizes[0],sizes[1],'selected authored alternatives must change the actual compound footprint');
});
