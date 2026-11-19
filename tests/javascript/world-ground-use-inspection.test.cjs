const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require(path.resolve('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
const root=process.env.WORLD_GROUND_USE_PACKET||'generated/ground-capacity-depth-sdk/edit-0002';
const packet=JSON.parse(fs.readFileSync(path.join(root,'world-domains.json'),'utf8'));
for(const d of packet.Domains)test(d.Id+': inspect every declared ground use without changing geometry',()=>{
 const world=d.World,before=JSON.stringify(world),view=S.layout(world);let expected=0;
 for(const node of world.Nodes){
  const intent=world.FootprintDefinitions?.[node.ArchetypeId]?.GroundUse;
  const box=view.boxes.find(b=>b.id===node.Id);
  if(intent){expected++;assert.deepEqual(box.groundUse,intent);assert.equal(box.physicalAccess.Occupancy,'Reservation');}
  else assert.equal(box?.groundUse,undefined,'unmarked reservations never infer ground-use intent');
 }
 assert.equal(expected,d.Id==='terrain'?9:16);assert.equal(JSON.stringify(world),before);
});
const source=packet.Domains.find(d=>d.Id==='terrain').World;
test('invalid source-open fractions refuse instead of displaying a valid-looking requirement',()=>{
 for(const value of [-.01,1.01,NaN,Infinity,'0.8']){
  const changed=structuredClone(source);changed.FootprintDefinitions['work-yard'].GroundUse.MinimumOpenFraction=value;
  assert.throws(()=>S.layout(changed),/Invalid ground-use declaration/);
 }
});
test('ground-use labels require purpose, rule, kind and a reserved physical source',()=>{
 for(const key of ['Kind','Purpose','SourceRuleId']){
  const changed=structuredClone(source);changed.FootprintDefinitions['work-yard'].GroundUse[key]='';
  assert.throws(()=>S.layout(changed),/Invalid ground-use declaration/);
 }
 const changed=structuredClone(source);changed.FootprintDefinitions.cache.GroundUse={Kind:'activity',Purpose:'Required activity',SourceRuleId:'test:invalid',MinimumOpenFraction:.5};
 assert.throws(()=>S.layout(changed),/Ground use requires reserved physical source/);
});
