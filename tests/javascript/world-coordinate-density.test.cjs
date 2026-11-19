const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_COORDINATE_DENSITY_ROOT||'generated/coordinate-density-field-final-20261102';
const read=()=>JSON.parse(fs.readFileSync(path.join(root,'world-regions.json'),'utf8'));
test('coordinate density preserves rough terrain and complete physical programs under a compact cell budget',()=>{
 const w=read(),before=JSON.stringify(w),p=R.layout(w);
 assert.equal(fs.readFileSync(path.join(root,'regional-schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js','utf8'));
 const saved=fs.readFileSync(path.join(root,'regional-schematic.js'),'utf8');
 const rendered=require('node:vm').runInNewContext(saved+'\nWorldRegionalSchematic.layout(world);',{
  WorldSchematic:require(path.resolve(root,'schematic.js')),structuredClone,world:w});
 assert.equal(rendered.boxes.length,48);
 assert.equal(JSON.stringify(w),before);assert.equal(p.boxes.length,48);
 assert.equal(w.Regions.reduce((sum,r)=>sum+r.World.AccessPlan.Sites.length,0),45);
 assert.equal(w.Definition.Surface.HeightField.Heights.length,289);
 assert.equal(w.Definition.OwnershipPlan.Source.Pattern.MaximumCells,64);
 assert.ok(Math.abs(w.Definition.OwnershipPlan.Partition.Mass-112896)<1e-6);
 assert.ok(w.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
});
test('changed shared full-field samples are rejected by the regional viewer',()=>{
 const w=read();w.Regions[1].World.Surface.HeightField.Heights[0]+=.1;
 assert.throws(()=>R.layout(w),/Invalid/);
});
