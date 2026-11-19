const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ACCESS_PACKET||'generated/physical-access-feedback-final';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('solid footprint and port geometry stay exact across routes and networks',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.boxes.filter(b=>b.physicalAccess?.Occupancy==='Solid').length,3);
  const direct=plan.routes.find(r=>r.connectionId==='physical-access/explicit/across'&&!r.reverse);
  assert.deepEqual(direct.points[0],{x:15,y:20});assert.deepEqual(direct.points.at(-1),{x:85,y:20});
  assert.ok(direct.points.length>2);
  for(const box of plan.boxes.filter(b=>b.physicalAccess))assert.deepEqual(box.physicalAccess,world.AccessPlan.Sites.find(s=>s.NodeId===box.id));
});
test('missing occupancy, moved portals, invalid roles and center endpoints reject',()=>{
  for(const edit of [w=>w.AccessPlan.Sites.pop(),w=>w.AccessPlan.Sites[0].Ports[0].ApproachPosition.X++,
    w=>w.AccessPlan.Sites[0].Occupancy='Reservation',w=>w.RoutePlan.Rules[0].FromPortId=null,
    w=>w.RoutePlan.Routes[0].Forward.Path.Points[0].Y=30]){
    const invalid=structuredClone(world);edit(invalid);assert.throws(()=>Schematic.layout(invalid));
  }
});
test('export embeds current viewer and engine access packet',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
