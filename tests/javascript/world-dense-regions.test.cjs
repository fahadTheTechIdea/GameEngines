const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_DENSE_REGION_PACKET||'generated/dense-regions-cli';
const read=name=>JSON.parse(fs.readFileSync(`${folder}/${name}`));

test('actual full-resolution packet keeps every sample, destination and complete region',()=>{
 const world=read('world-metadata.json'),recipe=read('recipe.json'),field=world.Surface.HeightField;
 assert.equal(field.Columns,513);assert.equal(field.Rows,513);assert.equal(field.Heights.length,263169);
 assert.deepEqual(field,recipe.Surface.HeightField);assert.equal(world.Nodes.length,3);assert.equal(world.Connections.length,1);
 assert.equal(world.RegionPlan.SuitableArea,32768);assert.equal(world.RegionPlan.Request.Seeds[0].MinimumArea,32768);
 assert.equal(world.RegionPlan.Request.MaximumCells,32);assert.equal(world.RegionPlan.Regions[0].Cells.length,4);
 for(const n of world.Nodes){assert.ok(Math.abs(n.Position[0]-256)+Math.abs(n.Position[1]-256)<=128);}
 for(const route of world.RoutePlan.Routes){assert.equal(route.Forward.Status,'Found');assert.equal(route.Reverse.Status,'Found');}
});

test('renderer consumes merged source geometry and does not replace the terrain with layout cells',()=>{
 const world=read('world-metadata.json'),before=JSON.stringify(world),layout=S.layout(world);
 assert.deepEqual(layout.generatedRegions,world.RegionPlan);assert.equal(layout.boxes.length,3);
 assert.equal(layout.coordinateSpace,'engine-surface');assert.equal(JSON.stringify(world),before);
 for(const corrupt of [w=>w.RegionPlan.Regions[0].Cells.pop(),w=>w.RegionPlan.SuitableArea--,w=>w.RegionPlan.Request.Seeds[0].MinimumArea++]){
  const copy=structuredClone(world);corrupt(copy);assert.throws(()=>S.layout(copy));
 }
});

test('published schematic embeds the exact document and current renderer',()=>{
 const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html);
 assert.ok(embedded);assert.deepEqual(JSON.parse(embedded[1]),read('world-metadata.json'));
 assert.deepEqual(fs.readFileSync(`${folder}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
