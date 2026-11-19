const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','review'].map(name=>{
 const folder=`generated/dependencies-${name}`;
 return {folder,world:read(`${folder}/world-metadata.json`),checkpoint:read(`${folder}/generation-session.json`)};
});
test('registered source identities persist through actual CLI replay and label-only reuse',()=>{
 const [a,b]=packets;
 assert.equal(a.checkpoint.SchemaVersion,2);
 const initial=a.checkpoint.InitialReport,edit=b.checkpoint.Edits.at(-1).Report;
 assert.equal(initial.Stages.length,14);assert.ok(initial.Stages.every(s=>!s.Reused));
 assert.equal(edit.Stages.length,14);assert.ok(edit.Stages.every(s=>s.Reused));
 assert.deepEqual(initial.Dependencies,edit.Dependencies);
 const dependency=edit.Dependencies.find(d=>d.Kind==='catalog'&&d.Id==='modern');
 assert.ok(dependency);
 for(const digest of [dependency.ContentSha256,dependency.SelectionSha256,dependency.Implementation.ConfigurationSha256])assert.match(digest,/^[a-f0-9]{64}$/);
 assert.ok(dependency.Implementation.Type.endsWith('ModernCatalogPolicy'));
 assert.deepEqual(edit.Changes.map(c=>c.Pointer),['/GameProfile']);
 assert.deepEqual({...b.world,GameProfile:a.world.GameProfile,RecipeSha256:a.world.RecipeSha256},a.world);
 assert.equal(b.world.Nodes.length,4);
 assert.deepEqual(a.checkpoint.ProtectedInputs,b.checkpoint.ProtectedInputs);
 assert.equal(fs.existsSync('generated/dependencies-rejected'),false);
});
test('current JavaScript and embedded packet preserve registered world geometry exactly',()=>{
 for(const {folder,world} of packets){
  const view=Schematic.layout(world);assert.equal(view.boxes.length,world.Nodes.length);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
   if(!route[direction])continue;
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
  }
 }
});
