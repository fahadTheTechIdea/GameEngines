const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_LOCAL_CONSTRAINT_ROOT;assert.ok(root,'Set WORLD_LOCAL_CONSTRAINT_ROOT to the actual packet folder.');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
for(const name of ['analytic','weighted','joint','nested','chain'])test(`${name}: exact saved areas and shared-boundary requirements retain every program`,()=>{
 const p=read(name+'.json');
 for(const generated of p.GeneratedAreas){S.areaPartitionWitness(generated.AreaPlan,()=>{throw new Error('Invalid saved cut witness');});
  const source=p.Definition.Layouts.find(l=>l.Id===generated.LayoutId);
  generated.AreaPlan.Strata.forEach((s,i)=>assert.ok(s.Area>=source.ProgramIntents[i].MinimumArea));
 }
 for(const edge of p.Interfaces){const r=p.Definition.Interfaces.find(r=>r.Id===edge.Id);assert.deepEqual(edge.Requirement,r);
  assert.ok(Math.hypot(edge.SelectedSpan.To.X-edge.SelectedSpan.From.X,edge.SelectedSpan.To.Y-edge.SelectedSpan.From.Y)>=r.MinimumSpan);
 }
 if(name==='chain')assert.equal(p.Allocations.length,32);
 if(name==='joint')assert.equal(p.Allocations.length,4);
});
for(const name of ['world','edited'])test(`${name}: complete physical program and selected interface reach exact inspection`,()=>{
 const w=read(name+'/world-metadata.json'),view=S.layout(w),p=S.localLayouts(w);
 assert.equal(view.boxes.length,7);assert.equal(w.Nodes.filter(n=>n.Kind==='station').length,4);assert.equal(w.AccessPlan.Sites.length,2);assert.equal(view.interfaceRoutes.length,1);
 assert.deepEqual(fs.readFileSync(path.join(root,name,'schematic.js')),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 for(const mutate of [w=>w.LocalLayoutPlan.Definition.Interfaces[0].MinimumSpan=100,
  w=>w.LocalLayoutPlan.Interfaces[0].SelectedSpan.To.X++,w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Area--,
  w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Position.X++,w=>w.LocalLayoutPlan.GeneratedAreas=[]]){
  const changed=structuredClone(w);mutate(changed);assert.throws(()=>S.localLayouts(changed),/local layout|local repartition/);
 }
});
test('interface edit changes actual orientation and retains declared sources',()=>{
 const a=read('world/world-metadata.json'),b=read('edited/world-metadata.json');
 assert.notDeepEqual(a.Nodes.map(n=>n.Position),b.Nodes.map(n=>n.Position));assert.deepEqual(a.Nodes.map(({Position,...n})=>n),b.Nodes.map(({Position,...n})=>n));
 const span=w=>{const s=w.LocalLayoutPlan.Interfaces[0].SelectedSpan;return Math.hypot(s.To.X-s.From.X,s.To.Y-s.From.Y);};
 assert.equal(span(a),40);assert.equal(span(b),30);assert.equal(a.RegionPlan.SuitableArea,b.RegionPlan.SuitableArea);
});
