const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/purposeful-population';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
const view=Schematic.layout(world);
test('complete physical population and exact footprints survive the schematic',()=>{
 assert.equal(world.Nodes.length,12);assert.equal(view.boxes.length,12);
 assert.equal(world.AccessPlan.Sites.length,11);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,11);
 assert.equal(world.PlacementPlan.TemplateDefinition.Bindings.length,4);
 assert.equal(world.PlacementPlan.Definition.Bindings.length,11);
 assert.ok(world.PlacementPlan.Definition.Request.Relations.every(r=>r.Minimum===0&&r.Maximum===0));
 const expected={'markers-0':[10,16],'markers-1':[18,16],'markers-2':[26,16],'markers-3':[24,22],
  'boundary-0':[7,26],'boundary-1':[12,26],'boundary-2':[17,26],'boundary-3':[19,27],'resource-0':[38,30]};
 for(const [name,p] of Object.entries(expected)){
  const n=world.Nodes.find(n=>n.Id===`population/${name}`);assert.ok(n);
  assert.ok(n.Position.every((v,i)=>Math.abs(v-p[i])<1e-9));
 }
 assert.equal(fs.existsSync('generated/population-refused'),false);
});
test('all required physical interaction routes use saved coordinates and passage width',()=>{
 assert.equal(view.routes.length,4);
 for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
  const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
  assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.width,.6);
 }
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
