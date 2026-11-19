const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root='generated/parent-route-api',source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
for(const [name,count] of [['branching/published',25],['rotated/published',25],['compact/published',13],['baseline',13],['edited',25]])test(name+': shared parent selector preserves complete actual packet',()=>{
 const folder=path.join(root,name),w=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'))),before=JSON.stringify(w),S=require(path.resolve(folder,'schematic.js'));
 assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),source);
 const view=S.layout(w);assert.equal(JSON.stringify(w),before);assert.equal(w.Nodes.length,count);assert.equal(view.boxes.length,count);
 assert.deepEqual(w.InteriorPlan.Results,w.PlacementPlan.Definition.SourceWorld.InteriorPlan.Results);
 assert.deepEqual(view.interiorAssemblies,w.InteriorPlan.Results);
 for(const site of w.AccessPlan.Sites)assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(path.join(folder,'schematic.html'),'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),w);
});
test('parent selector kind/index/exclusivity and owner/direction traces cannot be forged',()=>{
 const folder=path.join(root,'branching/published'),S=require(path.resolve(folder,'schematic.js')),read=()=>JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
 const change=(w,f)=>{for(const stage of w.PlacementPlan.Definition.NestedStages)for(const b of stage.Bindings)if(b.Population?.Route?.Parent)f(b.Population.Route);};
 for(const f of [p=>p.Parent.ConnectionKind='missing',p=>p.Parent.ConnectionIndex=128,p=>p.Parent.ConnectionIndex=-1,p=>p.Parent.ConnectionIndex=1,p=>p.Parent.ConnectionKind='bad kind',p=>p.ConnectionId='conflicting']){
  const w=read();change(w,f);assert.throws(()=>S.layout(w));
 }
 let w=read();w.PlacementPlan.Definition.NestedStages[0].ParentNodeId='missing';assert.throws(()=>S.layout(w));
 w=read();w.PlacementPlan.PopulationOrientations[0].Rotations[0]+=10;assert.throws(()=>S.layout(w));
 w=read();w.PlacementPlan.Definition.SourceWorld.Connections.reverse();assert.doesNotThrow(()=>S.layout(w),'source array order is not a selector');
});
