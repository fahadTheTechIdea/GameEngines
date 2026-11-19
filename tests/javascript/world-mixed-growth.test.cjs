const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root='generated/mixed-growth-qualified-api',source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
for(const name of ['regional/published','network/published','baseline','edited'])test(name+': full mixed physical world reaches saved inspector',()=>{
 const folder=path.join(root,name),w=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'))),before=JSON.stringify(w),S=require(path.resolve(folder,'schematic.js'));
 assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),source);const view=S.layout(w);
 assert.equal(JSON.stringify(w),before);assert.equal(w.Nodes.length,27);assert.equal(w.AccessPlan.Sites.length,26);assert.equal(view.boxes.length,27);
 const base=w.PlacementPlan.Definition.SourceWorld;
 assert.equal(base.Nodes.length,15);assert.equal(base.AccessPlan.Sites.length,14);assert.deepEqual(w.InteriorPlan.Results,base.InteriorPlan.Results);
 assert.deepEqual(view.interiorAssemblies,base.InteriorPlan.Results);
 for(const site of w.AccessPlan.Sites)assert.ok((view.suitability.get(site.NodeId)||[]).some(o=>JSON.stringify(o.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
 for(const result of base.InteriorPlan.Results)for(const route of result.Routes)for(const key of ['Forward','Reverse'])if(route[key])
  assert.deepEqual(view.routes.find(r=>r.id===route[key].Id).points,route[key].Path.Points.map(p=>({x:p.X,y:p.Y})));
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(path.join(folder,'schematic.html'),'utf8'))[1]),w);
});
