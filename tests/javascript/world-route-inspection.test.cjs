const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_INSPECTION_ROOT;if(!root)throw new Error('WORLD_INSPECTION_ROOT must identify the current inspection gate packets');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const cases=[['coastal/world-metadata.json',['AuthoredConnection','DestinationNetwork']],
 ['domains/domain-0000/world-metadata.json',['InteriorOpening']],['shared/world-metadata.json',['AuthoredConnection']]];
for(const [file,kinds] of cases)test(`${file}: inspectable routes retain exact source rules, endpoints and decisions`,()=>{
 const w=read(file),before=JSON.stringify(w),view=Schematic.layout(w);assert.ok(view.routes.length);
 const expected=new Map();
 for(const item of [...(w.RoutePlan?.Routes||[]),...(w.NetworkPlan?.Networks||[]).flatMap(n=>n.Plan.Edges),...(w.InteriorPlan?.Results||[]).flatMap(a=>a.Routes)])
  for(const result of [item.Forward,item.Reverse].filter(Boolean))expected.set(result.Id,result);
 assert.equal(view.routes.length,expected.size);
 for(const route of view.routes){
  const saved=route.inspection;assert.ok(saved);assert.deepEqual(saved.Result,expected.get(route.id));
  assert.deepEqual(route.points,saved.Result.Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(route.width,saved.Profile.Radius*2);
  assert.deepEqual(saved.Connection,w.Connections.find(c=>c.Id===route.connectionId));
  if(saved.Kind==='AuthoredConnection')assert.deepEqual(saved.Rule,w.RoutePlan.Rules.find(r=>r.Id===saved.Rule.Id));
  else if(saved.Kind==='DestinationNetwork'){
   assert.deepEqual(saved.Rule,w.NetworkPlan.Rules.find(r=>r.Id===saved.Rule.Id));
   const n=w.NetworkPlan.Networks.find(n=>n.RuleId===saved.Rule.Id);assert.deepEqual(saved.NetworkOptions,n.Plan.Options);assert.deepEqual(saved.NetworkAnalysis,n.Plan.Analysis);
  }else{
   assert.equal(saved.Kind,'InteriorOpening');assert.deepEqual(saved.Rule,w.InteriorPlan.Definition.Rules.find(r=>r.Id===saved.Rule.Id));
   assert.deepEqual(saved.Opening,w.InteriorPlan.Results.flatMap(a=>a.Openings).find(o=>o.ConnectionId===route.connectionId));
  }
 }
 assert.deepEqual([...new Set(view.routes.map(r=>r.inspection.Kind))].sort(),kinds.sort());assert.equal(JSON.stringify(w),before);
 const folder=path.join(root,path.dirname(file));assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('shared physical segments expose every original directed journey without duplicating physical paths',()=>{
 const world=read('shared/world-metadata.json'),view=Schematic.layout(world);assert.ok(view.physicalCorridors.length);
 const routes=new Map(view.routes.map(r=>[r.id,r]));let shared=0;
 for(const group of view.physicalCorridors)for(const segment of group.Segments){
  const ids=[...segment.ForwardJourneys,...segment.ReverseJourneys];assert.ok(ids.length);
  for(const id of ids)assert.ok(routes.get(id)?.inspection.Result.Path.Points.length>=2);
  if(new Set(ids.map(id=>routes.get(id).connectionId)).size>1)shared++;
 }
 assert.ok(shared>0,'at least one physical passage belongs to multiple logical connections');
});
test('regional programs use the same exact local inspection source records',()=>{
 const world=read('regional/world-regions.json');let count=0;
 for(const region of world.Regions){const view=Schematic.layout(region.World);for(const route of view.routes){assert.equal(route.inspection.Connection.Id,route.connectionId);assert.equal(route.inspection.Result.Id,route.id);count++;}}
 assert.ok(count>0);assert.equal(fs.readFileSync(path.join(root,'regional/regional-schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js','utf8'));
});
test('viewer node limit is explicit and malformed spatial data never becomes a fallback diagram',()=>{
 const large=read('large/world-metadata.json');assert.equal(large.Nodes.length,5000);
 assert.equal(Schematic.layout(large).boxes.length,5000);
 const excess=structuredClone(large);excess.Nodes.push({...large.Nodes.at(-1),Id:'excess'});
 assert.throws(()=>Schematic.layout(excess),/1–5000 nodes/);
 const unsupported=read('coastal/world-metadata.json');unsupported.Space.Axes.push('z');
 assert.throws(()=>Schematic.layout(unsupported),/surface/i);
 const invalid=read('coastal/world-metadata.json');invalid.Nodes[0].Position[0]=null;
 assert.throws(()=>Schematic.layout(invalid),/Invalid destination position/);
});
