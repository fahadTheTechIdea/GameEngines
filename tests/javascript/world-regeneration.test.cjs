const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','moved','review'].map(name=>{
 const folder=`generated/regeneration-${name}`;
 return {name,folder,world:read(`${folder}/world-metadata.json`),checkpoint:read(`${folder}/generation-session.json`)};
});
test('incremental packets exactly preserve independently qualified full-world baselines',()=>{
 assert.equal(fs.readFileSync(`${packets[0].folder}/world-metadata.json`,'utf8'),fs.readFileSync('generated/landform-north-pass/world-metadata.json','utf8'));
 assert.equal(fs.readFileSync(`${packets[1].folder}/world-metadata.json`,'utf8'),fs.readFileSync('generated/landform-south-pass/world-metadata.json','utf8'));
 const [a,b,c]=packets.map(p=>p.world);
 assert.deepEqual(a.Nodes,b.Nodes);assert.deepEqual(a.AccessPlan.Sites,b.AccessPlan.Sites);
 assert.notDeepEqual(a.RoutePlan,b.RoutePlan);
 assert.deepEqual({...c,GameProfile:b.GameProfile,RecipeSha256:b.RecipeSha256},b);
 for(const {world} of packets){const view=Schematic.layout(world);assert.equal(view.boxes.length,9);assert.equal(view.routes.length,6);}
});
test('recorded stage dependencies give the actual terrain invalidation closure',()=>{
 const initial=packets[0].checkpoint.InitialReport,moved=packets[1].checkpoint.Edits.at(-1).Report;
 assert.equal(initial.Stages.length,14);assert.ok(initial.Stages.every(s=>!s.Reused));
 const done=new Set();for(const stage of moved.Stages){assert.ok(stage.DependsOn.every(id=>done.has(id)));done.add(stage.Id);}
 const affected=new Set(['surface']);
 for(const stage of moved.Stages)if(stage.DependsOn.some(id=>affected.has(id)))affected.add(stage.Id);
 assert.equal(affected.size,13);
 for(const stage of moved.Stages)assert.equal(stage.Reused,!affected.has(stage.Id));
 assert.deepEqual(moved.Changes.map(c=>c.Pointer),['/Surface/Terrain/Operations/0/ControlPath/1/Y','/Surface/Terrain/Operations/1/ControlPath/0/Y']);
 const presentation=packets[2].checkpoint.Edits.at(-1).Report;
 assert.ok(presentation.Stages.every(s=>s.Reused));assert.equal(presentation.Changes[0].Pointer,'/GameProfile');
 assert.deepEqual(packets[0].checkpoint.ProtectedInputs,packets[1].checkpoint.ProtectedInputs);
 assert.deepEqual(packets[1].checkpoint.ProtectedInputs,packets[2].checkpoint.ProtectedInputs);
});
test('schematics preserve saved routes, widths, complete hierarchy and current renderer',()=>{
 for(const {folder,world} of packets){
  const view=Schematic.layout(world);
  assert.equal(view.boxes.filter(b=>b.physicalAccess).length,8);
  for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.width,.6);
  }
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 }
});
