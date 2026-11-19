const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ROUTE_REUSE_PACKET||'generated/corridor-reuse';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('reuse attraction survives export as actual shared geometry',()=>{
 const view=Schematic.layout(world),branch=world.RoutePlan.Routes.find(r=>r.ConnectionId.endsWith('b-branch'));
 assert.equal(view.routes.length,4);assert.equal(view.physicalCorridors[0].Segments.length,5);
 assert.deepEqual(branch.Forward.Path.Points,[{X:40,Y:25},{X:40,Y:20},{X:60,Y:20},{X:60,Y:25}]);
 assert.deepEqual(view.routes.find(r=>r.id===branch.Forward.Id).points,branch.Forward.Path.Points.map(p=>({x:p.X,y:p.Y})));
 assert.equal(branch.Forward.Cost.Distance,30);assert.equal(branch.Forward.Cost.ReusedDistance,20);assert.equal(branch.Forward.Cost.NewPassage,50);
 assert.equal(branch.Reverse.Cost.ReusedDistance,30);assert.equal(branch.Reverse.Cost.NewPassage,0);
});
test('directional sharing remains explicit through the common viewer',()=>{
 const group=Schematic.layout(world).physicalCorridors[0];
 assert.equal(group.SharedDependencies.length,1);
 assert.equal(group.Junctions.filter(j=>j.SegmentIds.length===3).length,2);
 const shared=group.Segments.find(s=>s.Id===group.SharedDependencies[0].SegmentId);
 assert.deepEqual(shared.From,{X:40,Y:20});assert.deepEqual(shared.To,{X:60,Y:20});
});
test('exported viewer and metadata match current source and engine file',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const match=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(match[1]),world);
});
