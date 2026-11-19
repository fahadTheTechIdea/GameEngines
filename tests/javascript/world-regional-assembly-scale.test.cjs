const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_ASSEMBLY_ROOT||'generated/regional-assembly-public';
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const world=read(path.join(root,'default/world-regions.json'));
test('combined inspection retains all regional nodes, exact solid sources and source reservations',()=>{
 const before=JSON.stringify(world),plan=R.layout(world);
 assert.equal(plan.boxes.length,1032);assert.equal(new Set(plan.boxes.map(b=>b.id)).size,1032);
 assert.equal(plan.boxes.filter(b=>b.physicalAccess).length,1024);
 for(const box of plan.boxes.filter(b=>b.physicalAccess)){
  assert.equal(box.physicalAccess.Occupancy,'Solid');
  const region=world.Regions.find(r=>r.World.Nodes.includes(box.node));
  assert.ok(region);assert.equal(region.World.PlacementPlan.Placement.Sites.length,128);
 }
 assert.equal(JSON.stringify(world),before);
});
test('both required regional directions retain their full saved path',()=>{
 const route=world.Connections[0];assert.equal(route.Forward.Status,'Found');assert.equal(route.Reverse.Status,'Found');
 assert.deepEqual(route.Forward.Path.Points[0],route.From);assert.deepEqual(route.Reverse.Path.Points[0],route.To);
 assert.equal(world.Members.filter(m=>m.Kind==='Path'&&m.Id.startsWith('regional-journey/')).length,2);
});
test('public command and SDK preserve the exact complete world packet',()=>{
 assert.deepEqual(world,read(path.join(path.dirname(root),'regional-assembly-native/world-regions.json')));
 assert.deepEqual(read(path.join(root,'contact/world-regions.json')),read(path.join(path.dirname(root),'regional-assembly-native/contact.world.json')));
});
test('touching whole reservations remain available in the combined view',()=>{
 const contact=read(path.join(root,'contact/world-regions.json')),plan=R.layout(contact);
 assert.equal(plan.boxes.length,4);assert.equal(plan.boxes.filter(b=>b.physicalAccess).length,2);
 assert.equal(contact.Members.filter(m=>m.Kind==='Node'&&m.RegionIds.length===2).length,2);
});
test('failed collision/work requests publish no accepted directory',()=>{
 assert.equal(fs.existsSync(path.join(root,'collision')),false);assert.equal(fs.existsSync(path.join(root,'tight')),false);
});
