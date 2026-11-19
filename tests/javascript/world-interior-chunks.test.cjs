const {test}=require('node:test'),assert=require('node:assert/strict');
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const root=process.env.WORLD_INTERIOR_CHUNKS;
if(!root)throw new Error('WORLD_INTERIOR_CHUNKS must identify a current native test publication');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));
const digest=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');
const world=read('world/world-metadata.json'),manifest=read('geometry/manifest.json');
const members=new Map();
for(const [ordinal,chunk] of manifest.Chunks.entries()){
 const bytes=fs.readFileSync(path.join(root,'geometry',String(ordinal).padStart(6,'0')+'.json'));
 assert.equal(bytes.length,chunk.Bytes);assert.equal(digest(bytes),chunk.Sha256);
 const packet=JSON.parse(bytes);assert.equal(packet.Version,4);assert.equal(packet.ChunkId,chunk.ChunkId);
 for(const item of packet.Members){
  assert.ok(item.Member.ChunkIds.includes(chunk.ChunkId));const key=item.Member.Kind+':'+item.Member.Id;
  if(members.has(key))assert.deepEqual(item,members.get(key));else members.set(key,item);
 }
}
test('current chunks preserve all exact generated wall and doorway facts',()=>{
 assert.equal(manifest.Version,4);assert.equal(manifest.SourceWorldSha256,world.ChunkPlan.SourceWorldSha256);
 let walls=0;const doors=new Map();
 for(const result of world.InteriorPlan.Results){
  const rule=world.InteriorPlan.Definition.Rules.find(r=>r.Id===result.RuleId);
  for(const roomId of result.RoomNodeIds){
   const member=members.get('Node:'+roomId);assert.ok(member);
   const facts=member.Interiors.find(i=>i.RuleId===result.RuleId);assert.ok(facts);
   assert.equal(facts.Purpose,rule.Purpose);assert.equal(facts.SourceRuleId,rule.SourceRuleId);assert.equal(facts.WallThickness,rule.WallThickness);
   assert.deepEqual(facts.Walls,result.Walls.filter(w=>w.RoomNodeId===roomId));walls+=facts.Walls.length;
   assert.deepEqual(facts.Openings,result.Openings.filter(o=>o.FromId===roomId||o.ToId===roomId));
   const coverage=member.Coverage.filter(c=>c.Kind==='InteriorWall');assert.equal(coverage.length,facts.Walls.length);
   for(const wall of facts.Walls)assert.ok(coverage.some(c=>JSON.stringify(c.Polygon)===JSON.stringify(wall.Footprint)));
   for(const door of facts.Openings){const key=result.RuleId+':'+door.ConnectionId;if(doors.has(key))assert.deepEqual(door,doors.get(key));else doors.set(key,door);}
  }
 }
 assert.equal(walls,34);assert.equal(doors.size,5);assert.equal(members.size,23);
 for(const node of world.Nodes)assert.deepEqual(members.get('Node:'+node.Id).Node,node);
});
test('interior passage centerlines and all hierarchy members remain exact',()=>{
 let paths=0;
 for(const result of world.InteriorPlan.Results)for(const route of result.Routes)for(const direction of [route.Forward,route.Reverse].filter(Boolean)){
  assert.deepEqual(members.get('Path:'+direction.Id).Path,direction.Path);paths++;
 }
 assert.equal(paths,10);assert.equal(world.Nodes.length,13);
});
test('domain publication retains one complete source water mask and current geometry format',()=>{
 const shared=read('domains/shared.json'),domain=shared.Domains.find(d=>d.DomainId==='floor');
 const water=world.Surface.Patches.find(p=>p.Id==='seam-water');
 assert.ok(water);assert.deepEqual(domain.Surface.Patches.filter(p=>p.Id===water.Id),[water]);
 const parent=read('domains/manifest.json');
 for(const [ordinal,entry] of parent.Domains.entries())if(entry.HasChunkPlan){
  const bytes=fs.readFileSync(path.join(root,'domains','domain-'+String(ordinal).padStart(4,'0'),'manifest.json'));
  assert.equal(digest(bytes),entry.ManifestSha256);assert.equal(JSON.parse(bytes).Version,4);
 }
});
