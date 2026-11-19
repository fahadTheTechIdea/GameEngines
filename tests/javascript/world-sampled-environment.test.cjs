const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const packet=process.env.WORLD_SAMPLED_ENVIRONMENT_PACKET||'generated/environment-wind-cli';
const physical=process.env.WORLD_WIND_PHYSICAL_PACKET;
const read=(dir,file)=>JSON.parse(fs.readFileSync(`${dir}/${file}`));
test('CLI persists typed sampled layers and every required destination',()=>{
 const world=read(packet,'world-metadata.json'),recipe=read(packet,'recipe.json');
 assert.equal(world.Nodes.length,4);assert.equal(world.Connections.length,2);
 assert.deepEqual(world.EnvironmentPlan.Definition.SampledFields,recipe.Environment.SampledFields);
 assert.equal(world.EnvironmentPlan.Definition.SampledFields.length,2);
 const inputs=new Map(world.EnvironmentPlan.Definition.SampledFields.map(f=>[f.Id,f]));
 for(const f of world.EnvironmentPlan.Definition.Fields)for(const t of f.Terms)if(t.Input==='SampledField')assert.ok(inputs.has(t.FieldId));
 for(const f of inputs.values()){assert.equal(f.Values.length,f.Columns*f.Rows);assert.equal(f.Unit,'relative');}
});
test('schematic retains engine geometry and embedded sampled inputs without generating climate',()=>{
 const world=read(packet,'world-metadata.json'),before=JSON.stringify(world),layout=S.layout(world);
 assert.equal(layout.boxes.length,4);assert.equal(layout.coordinateSpace,'engine-surface');assert.equal(JSON.stringify(world),before);
 assert.deepEqual(fs.readFileSync(`${packet}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 const html=fs.readFileSync(`${packet}/schematic.html`,'utf8');
 assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
});
test('complete physical scalar world remains inspectable at actual engine coordinates',{skip:!physical},()=>{
 const world=read(physical,'world-metadata.json'),before=JSON.stringify(world),layout=S.layout(world);
 assert.equal(layout.boxes.length,59);assert.equal(world.AccessPlan.Sites.length,58);assert.equal(world.RoutePlan.Routes.length,35);
 assert.equal(world.NetworkPlan.Networks[0].Plan.Edges.length,6);assert.equal(world.EnvironmentPlan.Definition.SampledFields.length,2);
 assert.equal(JSON.stringify(world),before);assert.equal(layout.coordinateSpace,'engine-surface');
});
