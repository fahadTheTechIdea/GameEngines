const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_DOMAIN_PACKET||'generated/world-domains',world=JSON.parse(fs.readFileSync(`${folder}/world-domains.json`));
test('independent floors retain source coordinates and local identities',()=>{
 assert.equal(world.Domains.length,3);assert.equal(world.Domains.reduce((n,d)=>n+d.World.Nodes.length,0),21);
 assert.deepEqual(world.Domains.find(d=>d.Id==='lower').World,world.Domains.find(d=>d.Id==='upper').World);
 for(let i=0;i<world.Domains.length;i++){
  const child=path.join(folder,`domain-${String(i).padStart(4,'0')}`),data=JSON.parse(fs.readFileSync(`${child}/world-metadata.json`));
  assert.deepEqual(data,world.Domains[i].World);assert.equal(Schematic.layout(data).boxes.length,7);
  assert.equal(fs.readFileSync(`${child}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${child}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),data);
 }
});

test('navigation binds each independent world and canonical child page without merging floors',()=>{
 const receipts=world.Domains.map((domain,i)=>{
  const page=fs.readFileSync(`${folder}/domain-${String(i).padStart(4,'0')}/schematic.html`,'utf8');
  const receipt=JSON.parse(/<script id="domain-data" type="application\/json">([\s\S]*?)<\/script>/.exec(page)[1]);
  const url=`http://localhost/fixture/domain-${String(i).padStart(4,'0')}/schematic.html`;
  const before=JSON.stringify(domain.World),validated=Schematic.domainNavigation(receipt,domain.World,url);
  assert.equal(validated.CurrentDomainId,domain.Id);assert.equal(validated.DefinitionSha256,world.DefinitionSha256);
  assert.deepEqual(validated.Entries.map(e=>e.Id),world.Domains.map(d=>d.Id));assert.equal(JSON.stringify(domain.World),before);
  validated.Entries[0].Purpose='changed';assert.notEqual(receipt.Entries[0].Purpose,'changed');
  for(const mutate of [r=>r.Version=2,r=>r.CurrentDomainId='missing',r=>r.Entries[0].Page='javascript:alert(1)',
   r=>r.Entries[0].Page='https://example.invalid/schematic.html',r=>r.Entries[0].Page='../domain-%30%30%30%30/schematic.html',
   r=>r.Entries[i].RecipeSha256='0'.repeat(64),r=>r.Entries[i].CatalogSha256='0'.repeat(64),r=>r.Entries.push(r.Entries[0]),
   r=>r.Transitions[0].FromDomainId='missing',r=>r.Transitions[0].Bidirectional='yes',r=>r.Extra=true]){
   const bad=structuredClone(receipt);mutate(bad);assert.throws(()=>Schematic.domainNavigation(bad,domain.World,url),/Domain navigation/);
  }
  assert.throws(()=>Schematic.domainNavigation(receipt,domain.World,'http://localhost/fixture/other/schematic.html'),/Domain navigation/);
  return receipt;
 });
 const lower=world.Domains.findIndex(d=>d.Id==='lower'),upper=world.Domains.findIndex(d=>d.Id==='upper');
 assert.deepEqual(world.Domains[lower].World,world.Domains[upper].World);
 assert.throws(()=>Schematic.domainNavigation(receipts[lower],world.Domains[upper].World,`http://localhost/fixture/domain-${String(upper).padStart(4,'0')}/schematic.html`),/Domain navigation/);
 assert.equal(Schematic.domainNavigation(null,world.Domains[0].World),null);
});
test('transitions reference exact qualified local ports/openings and graph paths',()=>{
 assert.deepEqual(world.Reachability[0].ForwardTransitions,['entry','stairs']);assert.deepEqual(world.Reachability[0].ReturnTransitions,['stairs','entry']);
 for(const t of world.Transitions)for(const e of [t.From,t.To]){
  const local=world.Domains.find(d=>d.Id===e.Definition.DomainId).World;assert.ok(e.AvailableWidth>=2*e.Definition.Profile.Radius);
  if(e.Definition.AccessPortId){
   const port=local.AccessPlan.Sites.find(s=>s.NodeId===e.Definition.NodeId).Ports.find(p=>p.Id===e.Definition.AccessPortId);
   assert.deepEqual(e.Position,[port.ApproachPosition.X,port.ApproachPosition.Y]);
  }else{
   const opening=local.InteriorPlan.Results.flatMap(r=>r.Openings).find(o=>o.ConnectionId===e.Definition.InteriorConnectionId);
   const point=opening.FromId===e.Definition.NodeId?opening.FromApproach:opening.ToApproach;assert.deepEqual(e.Position,[point.X,point.Y]);
  }
 }
});
test('review index links every independent schematic and preserves source definition',()=>{
 const html=fs.readFileSync(`${folder}/index.html`,'utf8');
 for(let i=0;i<world.Domains.length;i++)assert.ok(html.includes(`domain-${String(i).padStart(4,'0')}/schematic.html`));
 assert.deepEqual(JSON.parse(fs.readFileSync(`${folder}/definition.json`)),world.Definition);
 assert.ok(html.includes('do not create continuous 3D connectors'));
});
