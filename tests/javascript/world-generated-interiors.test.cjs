const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.GENERATED_INTERIOR_PACKET||'generated/generated-interiors';
const cases=process.env.GENERATED_INTERIOR_CASES?JSON.parse(process.env.GENERATED_INTERIOR_CASES):[['branching',6],['rotated',6],['expanded',8],['hexagonal',6]];
for(const [name,rooms,connections=rooms-1] of cases){
 const read=()=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
 test(`${name}: all generated floors, walls, openings, fixtures and paths reach schematic consumers`,()=>{
  const world=read(),view=Schematic.layout(world),assembly=world.InteriorPlan.Results[0];
  assert.equal(view.boxes.length,1+2*rooms);assert.equal(view.routes.length,2*connections);
  assert.deepEqual(view.interiorAssemblies,[assembly]);
  assert.equal(assembly.RoomNodeIds.length,rooms);assert.equal(assembly.Openings.length,connections);
  assert.equal(world.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').length,rooms);
  for(const route of assembly.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route[direction].Id);
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.equal(rendered.width,.4);
  }
  const summary=JSON.parse(fs.readFileSync(`${root}/${name}/summary.json`,'utf8'));
  assert.equal(summary.InteriorOpenings,connections);assert.equal(summary.InteriorWalls,assembly.Walls.length);
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 });
 test(`${name}: missing interior provenance and inconsistent consumer geometry refuse`,()=>{
  const missing=read();missing.InteriorPlan=null;assert.throws(()=>Schematic.layout(missing),/Invalid generated interior/);
  const door=read();door.InteriorPlan.Results[0].Openings[0].Width+=.1;assert.throws(()=>Schematic.layout(door),/Invalid generated interior/);
  const route=read();route.InteriorPlan.Results[0].Routes[0].Forward.Path.Points[0].X+=1;assert.throws(()=>Schematic.layout(route),/Invalid route geometry/);
  const wall=read();wall.InteriorPlan.Results[0].Walls[0].RoomNodeId='missing';assert.throws(()=>Schematic.layout(wall),/Invalid generated interior/);
 });
}
