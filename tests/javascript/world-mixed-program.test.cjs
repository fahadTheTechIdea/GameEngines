const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_MIXED_PACKET||'generated/mixed-program-qualification-final';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world/world-metadata.json`));

for(const name of ['inland','coastal'])test(`${name}: complete physical content and nested layout data retain engine coordinates`,()=>{
 const world=read(name),view=Schematic.layout(world),plan=view.localLayouts;
 assert.equal(world.Nodes.length,43);assert.equal(world.AccessPlan.Sites.length,42);
 assert.deepEqual(plan,world.LocalLayoutPlan);
 assert.equal(plan.Definition.Layouts.length,3);assert.equal(plan.Allocations.length,9);
 assert.equal(world.RoutePlan.Routes.reduce((n,r)=>n+1+(r.Reverse?1:0),0),70);
 const terminal=plan.Allocations.filter(a=>!plan.Definition.Layouts.some(l=>l.ParentAllocationId===a.Id));
 assert.equal(terminal.length,7);
 assert.ok(Math.abs(terminal.reduce((n,a)=>n+a.Shape.Area,0)-(name==='inland'?26550:25590))<1e-6);
 for(const receipt of plan.Areas){
  const parent=receipt.ParentAllocationId?plan.Allocations.find(a=>a.Id===receipt.ParentAllocationId).Shape:world.RegionPlan.Regions[0];
  assert.equal(receipt.RegionArea,parent.Area);
 }
 assert.equal(fs.readFileSync(`${root}/${name}/world/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});

test('missing parents, cycles, duplicate scope owners and false parent receipts refuse display',()=>{
 const world=read('inland');
 for(const mutate of [
  p=>p.Definition.Layouts.find(l=>l.Id==='ordered-layout').ParentAllocationId='missing',
  p=>p.Definition.Layouts.find(l=>l.Id==='districts').ParentAllocationId='workshop',
  p=>p.Definition.Layouts.find(l=>l.Id==='ordered-layout').ParentAllocationId='irregular',
  p=>p.Areas.find(a=>a.LayoutId==='ordered-layout').ParentAllocationId='irregular',
  p=>p.Areas.find(a=>a.LayoutId==='ordered-layout').RegionArea+=100,
 ]){const copy=structuredClone(world);mutate(copy.LocalLayoutPlan);assert.throws(()=>Schematic.layout(copy),/local layout/);}
});

test('directed route limit counts both directions',()=>{
 const world=read('inland');
 world.RoutePlan.Routes=Array.from({length:65},()=>structuredClone(world.RoutePlan.Routes[0]));
 assert.throws(()=>Schematic.layout(world),/route/i);
});
