const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_NESTED_CONTENT_GATE;
if(!root)throw new Error('WORLD_NESTED_CONTENT_GATE must identify current nested-content publications');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const area=p=>Math.abs(p.Vertices.reduce((sum,a,i)=>{const b=p.Vertices[(i+1)%p.Vertices.length];return sum+a.X*b.Y-a.Y*b.X;},0))/2;
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-7,`${a} != ${b}`);
function bounds(p){return {minX:Math.min(...p.Vertices.map(v=>v.X)),maxX:Math.max(...p.Vertices.map(v=>v.X)),minY:Math.min(...p.Vertices.map(v=>v.Y)),maxY:Math.max(...p.Vertices.map(v=>v.Y))};}
test('intrinsic combined demand includes all four solids across both nested branches before placement',()=>{
 const refusal=read('hierarchical-capacity/refusal.json'),proof=read('source-ground/source-proof.json'),w=refusal.Witness;
 assert.equal(refusal.Preflight,true);assert.equal(proof.SourceOnly,true);assert.deepEqual(proof.Witness,w);
 assert.equal(w.SolidNodeIds.length,4);assert.equal(new Set(w.SolidNodeIds).size,4);assert.deepEqual(w.SourceStageIds,['fixtures-0','fixtures-1']);
 assert.deepEqual(w.SolidSourceAreas,[36,36,36,36]);near(w.RequiredSolidArea,w.SolidSourceAreas.reduce((a,b)=>a+b,0));
 near(w.GroundSourceArea,400);near(1-w.RequiredSolidArea/w.GroundSourceArea,.64);assert.ok(.64<w.MinimumOpenFraction);
});
test('fitting control keeps every child, exact physical source and independent open-ground denominator',()=>{
 const w=read('hierarchical-capacity/fit/world-metadata.json'),c=read('hierarchical-capacity/fit-catalog.json');
 assert.equal(w.Nodes.length,8);assert.equal(w.AccessPlan.Sites.length,7);
 const solids=w.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid');assert.equal(solids.length,4);
 const ground=w.AccessPlan.Sites.find(s=>s.NodeId==='ground-capacity/ground-0');near(area(ground.Footprint),400);
 const outer=bounds(ground.Footprint);
 for(const site of w.AccessPlan.Sites){
  const node=w.Nodes.find(n=>n.Id===site.NodeId),source=c.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
  near(area(site.Footprint),area(source));
 }
 for(let i=0;i<solids.length;i++){
  near(area(solids[i].Footprint),36);const a=bounds(solids[i].Footprint);
  assert.ok(a.minX>=outer.minX&&a.maxX<=outer.maxX&&a.minY>=outer.minY&&a.maxY<=outer.maxY);
  for(let j=i+1;j<solids.length;j++){const b=bounds(solids[j].Footprint);assert.equal(Math.max(0,Math.min(a.maxX,b.maxX)-Math.max(a.minX,b.minX))*Math.max(0,Math.min(a.maxY,b.maxY)-Math.max(a.minY,b.minY)),0);}
 }
 near(400-solids.reduce((sum,s)=>sum+area(s.Footprint),0),256);near(256/400,.64);
 const authored=c.Archetypes.find(a=>a.Id==='ground').PhysicalFootprint.GroundUse;assert.equal(authored.MinimumOpenFraction,.6);assert.ok(.64>=authored.MinimumOpenFraction);
});
test('whole regional source alternatives retain terrain-required parents, all children and both journey directions',()=>{
 const w=read('source-ground/packet/world-regions.json');
 assert.ok(w.Regions.length>1);assert.ok(w.Connections.length>0);
 for(const region of w.Regions){
  const local=region.World,parents=local.Nodes.filter(n=>n.Kind==='discovery'),definition=w.Definition.Regions.find(r=>r.Id===region.RegionId);
  assert.equal(parents.length,local.TerrainDemandPlan.Results[0].Count);assert.equal(local.Nodes.length,1+parents.length*2);assert.equal(local.AccessPlan.Sites.length,parents.length*2);
  assert.ok(parents.every(p=>p.ArchetypeId==='landmark-alt'));
  for(const p of parents)assert.equal(local.Nodes.filter(n=>n.ParentId===p.Id).length,1);
  for(const site of local.AccessPlan.Sites){const node=local.Nodes.find(n=>n.Id===site.NodeId),source=definition.Catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;near(area(site.Footprint),area(source));}
 }
 for(const c of w.Connections){assert.equal(c.Forward.Status,'Found');assert.equal(c.Reverse.Status,'Found');assert.ok(c.Forward.Path.Points.length>=2);assert.ok(c.Reverse.Path.Points.length>=2);}
});
