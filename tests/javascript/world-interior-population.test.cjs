const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.INTERIOR_POPULATION_PACKET||'generated/interior-population-current-api';
const current=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
for(const [name,count] of [['branching/published',25],['rotated/published',25],['compact/published',13],['lifecycle-baseline',13],['lifecycle-edited',13]])test(name+': every source and generated interior member reaches actual packet inspector',()=>{
 const folder=path.join(root,name),world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'))),before=JSON.stringify(world);
 assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),current);
 const S=require(path.resolve(folder,'schematic.js')),view=S.layout(world),source=world.PlacementPlan.Definition.SourceWorld;
 assert.equal(world.Nodes.length,count);assert.equal(JSON.stringify(world),before);
 assert.deepEqual(new Set(view.boxes.map(b=>b.node.Id)),new Set(world.Nodes.map(n=>n.Id)));
 assert.deepEqual(world.InteriorPlan.Results,source.InteriorPlan.Results);assert.deepEqual(view.interiorAssemblies,source.InteriorPlan.Results);
 for(const result of source.InteriorPlan.Results)for(const route of result.Routes)for(const key of ['Forward','Reverse'])if(route[key]){
  assert.deepEqual(view.routes.find(r=>r.id===route[key].Id).points,route[key].Path.Points.map(p=>({x:p.X,y:p.Y})));
 }
 for(const site of world.AccessPlan.Sites)assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
 const html=fs.readFileSync(path.join(folder,'schematic.html'),'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
});
test('source interior paths and per-member orientation witnesses remain mandatory',()=>{
 const folder=path.join(root,'branching/published'),S=require(path.resolve(folder,'schematic.js'));
 const read=()=>JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
 let w=read();w.PlacementPlan.Definition.SourceWorld.InteriorPlan=null;assert.throws(()=>S.layout(w));
 w=read();w.PlacementPlan.PopulationOrientations[0].Rotations[0]+=10;assert.throws(()=>S.layout(w));
 w=read();w.PlacementPlan.Definition.SourceWorld.InteriorPlan.Results[0].Routes[0].Forward.Path.Points[0].Y+=2;assert.throws(()=>S.layout(w));
});
