const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_EDIT_ROOT||'generated/alternate-crossing-edit-20261101-a';
const read=folder=>JSON.parse(fs.readFileSync(`${root}/${folder}/world-metadata.json`,'utf8'));
test('terrain edit switches a real shared-boundary crossing without dropping content',()=>{
 const before=read('world'),after=read('edited');const a=Schematic.layout(before).interfaceRoutes[0],b=Schematic.layout(after).interfaceRoutes[0];
 assert.ok(a.Center.Y>11);assert.ok(b.Center.Y<9);assert.deepEqual(a.Attempts.map(x=>x.Accepted),[false,true]);assert.deepEqual(b.Attempts.map(x=>x.Accepted),[true]);
 assert.deepEqual(before.Nodes,after.Nodes);assert.deepEqual(before.AccessPlan.Sites,after.AccessPlan.Sites);
 assert.deepEqual(before.RegionPlan.IntentPlan.Relations[0].SharedSegments,after.RegionPlan.IntentPlan.Relations[0].SharedSegments);
 assert.ok(a.Forward&&a.Reverse&&b.Forward&&b.Reverse);
});
test('edited route cannot be detached from its generated interface',()=>{
 const world=structuredClone(read('edited'));world.RoutePlan.Routes[0].Interface.Center.Y+=1;
 assert.throws(()=>Schematic.layout(world),/interface/);
});
