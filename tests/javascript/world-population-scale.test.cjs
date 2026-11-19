const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
for(const name of ['256','512','1024','edit'])test(name+': full published population remains inspectable',()=>{
 const folder=path.resolve('generated/population-scale-cli',name,'world'),js=path.join(folder,'schematic.js');
 assert.equal(hash(fs.readFileSync(js)),hash(fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js')));
 const S=require(js),world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json'))),before=JSON.stringify(world),view=S.layout(world);
 const count=name==='edit'?1024:Number(name);assert.equal(view.boxes.length,count+1);assert.equal(world.AccessPlan.Sites.length,count);assert.equal(JSON.stringify(world),before);
 const trace=view.populationAreas[0];assert.equal(trace.NodeIds.length,count);assert.equal(trace.Plan.Strata.length,count);
 for(const site of world.AccessPlan.Sites)assert.ok(view.suitability.get(site.NodeId).some(e=>JSON.stringify(e.Footprint?.Footprint)===JSON.stringify(site.Footprint)));
});
