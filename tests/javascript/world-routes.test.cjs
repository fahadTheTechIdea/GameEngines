const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ROUTE_PACKET||'generated/terrain-routes';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('schema 2 surface view displays exact engine polygons, destinations and full route polylines',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.coordinateSpace,'engine-surface');
  assert.equal(plan.unit,'meters');
  assert.deepEqual(plan.boundary,world.Surface.Boundary.Vertices.map(p=>({x:p.X,y:p.Y})));
  assert.equal(plan.terrain.length,2);
  assert.equal(plan.routes.length,2);
  for(const [index,result] of [world.RoutePlan.Routes[0].Forward,world.RoutePlan.Routes[0].Reverse].entries()){
    assert.deepEqual(plan.routes[index].points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
    assert.equal(plan.routes[index].width,2);
    assert.ok(result.Path.Points.some(p=>p.Y>26));
    assert.ok(result.Cost.Distance>85);
  }
  for(const box of plan.boxes){
    assert.equal(box.marker,true);
    assert.ok(Math.abs(box.x+box.width/2-box.node.Position[0])<1e-10);
    assert.ok(Math.abs(box.y+box.height/2-box.node.Position[1])<1e-10);
  }
  assert.deepEqual(plan.corridors,[]); // No synthetic road/grid channels on the physical surface.
  assert.equal(world.MapGeometry,null); // No invented destination footprints.
  assert.deepEqual(Schematic.layout(world),plan);
});

test('current renderer rejects retired schema, malformed geometry, wrong endpoints and orphaned routes',()=>{
  for(const edit of [w=>w.SchemaVersion=1,w=>w.Surface.Boundary.Vertices[0].X=NaN,
    w=>w.RoutePlan.Routes[0].Forward.Path.Points[0].X+=1,w=>w.RoutePlan.Routes[0].RuleId='missing',
    w=>w.RoutePlan.Routes[0].Reverse=null,w=>w.Nodes[1].ParentId=w.Nodes[1].Id,
    w=>w.RoutePlan.Rules[0].Profile.Radius=Infinity]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});

test('saved executable packet uses current source, explicit scope and terrain controls',()=>{
  const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');
  const data=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html);
  assert.deepEqual(JSON.parse(data[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.ok(html.includes('data-toggle="terrain"')&&html.includes('Fit world'));
  assert.equal(world.Coverage,'PartialScope');
  assert.ok(world.RemainingScopes.join(' ').includes('junctions'));
});

test('topology-only schema 2 documents still use the generic hierarchy view',()=>{
  const graph=JSON.parse(fs.readFileSync('generated/layered-exploration/world-metadata.json'));
  const plan=Schematic.layout(graph);
  assert.equal(plan.boxes.length,graph.Nodes.length);
  assert.equal(plan.coordinateSpace,undefined);
});
