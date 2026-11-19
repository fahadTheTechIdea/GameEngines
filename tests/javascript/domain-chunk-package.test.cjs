const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
function inspect(root,composed){
  if(!root)throw new Error('Package output root is required');
  const read=name=>fs.readFileSync(path.join(root,name)),receipt=JSON.parse(read('publication.json'));
  const manifestBytes=read('package/manifest.json'),manifest=JSON.parse(manifestBytes),sharedBytes=read('package/shared.json'),shared=JSON.parse(sharedBytes);
  const selection=JSON.parse(read('selection.json')),world=JSON.parse(read('world.json'));
  assert.equal(manifest.Version,2);
  assert.equal(hash(manifestBytes),receipt.ManifestSha256);assert.equal(hash(sharedBytes),manifest.SharedSha256);
  assert.equal(manifest.SourceSha256,hash(read('world.json')));assert.equal(shared.SourceSha256,manifest.SourceSha256);
  assert.deepEqual(shared.SharedTransitions,composed?world.Domains.Transitions:world.Transitions);
  assert.deepEqual(shared.SharedRegionalRoutes,composed?world.RegionalRoutes:[]);
  for(const chosen of selection.Domains){
    const ordinal=manifest.Domains.findIndex(d=>d.DomainId===chosen.DomainId),domain=manifest.Domains[ordinal];assert.ok(domain);
    if(!domain.HasChunkPlan){assert.equal(chosen.ChunkIds.length,0);continue;}
    const folder=`package/domain-${String(ordinal).padStart(4,'0')}`,childBytes=read(`${folder}/manifest.json`),child=JSON.parse(childBytes);
    assert.equal(hash(childBytes),domain.ManifestSha256);assert.equal(child.SourceWorldSha256,domain.SourceWorldSha256);
    const actual=new Map();
    for(const id of chosen.ChunkIds){
      const i=child.Chunks.findIndex(c=>c.ChunkId===id);assert.ok(i>=0);
      const packetBytes=read(`${folder}/${String(i).padStart(6,'0')}.json`),packet=JSON.parse(packetBytes);
      assert.equal(hash(packetBytes),child.Chunks[i].Sha256);assert.equal(packet.ChunkId,id);
      if(chosen.Surface){
        const terrainBytes=read(`${folder}/terrain-${String(i).padStart(6,'0')}.json`),terrain=JSON.parse(terrainBytes);
        assert.equal(hash(terrainBytes),domain.Terrain[i].Sha256);assert.deepEqual(terrain,chosen.Terrain.find(t=>t.ChunkId===id));
      }
      for(const member of packet.Members){const key=member.Member.Kind+':'+member.Member.Id;if(actual.has(key))assert.deepEqual(actual.get(key),member);else actual.set(key,member);}
    }
    assert.equal(actual.size,chosen.Geometry.length);
    for(const member of chosen.Geometry)assert.deepEqual(actual.get(member.Member.Kind+':'+member.Member.Id),member);
  }
  return {world,selection};
}
test('independent floors and logical domain use pinned selective packets',()=>{
  const {selection}=inspect(process.env.DOMAIN_CHUNK_PACKAGE,false);
  assert.ok(selection.Domains.some(d=>d.DomainId==='lower')&&selection.Domains.some(d=>d.DomainId==='upper'));
  assert.ok(selection.Domains.find(d=>d.DomainId==='progression').SharedNodes.length>0);
});
test('regional package retains cross-region route and portal dependencies',()=>{
  const {selection}=inspect(process.env.REGIONAL_DOMAIN_CHUNK_PACKAGE,true);
  assert.ok(selection.SharedRegionalRoutes[0].Forward.Path.Points.length>1);
  assert.ok(selection.SharedTransitions.some(t=>t.Definition.Id==='portal'));
});
