const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const cases=[
  {env:'SDK_FIRST_PHYSICAL',source:'examples/metadata/world-domains/definition.json',domains:3,physical:true},
  {env:'SDK_FIRST_MIXED',source:'examples/metadata/mixed-domains/definition.json',domains:3,physical:true,logical:true},
  {env:'SDK_FIRST_CORRIDOR',source:'examples/metadata/domain-journeys/gated-corridor-definition.json',domains:2,physical:true,logical:true,corridor:true},
];
for(const c of cases)test(`public SDK first world: ${c.env}`,()=>{
  const root=process.env[c.env];if(!root)throw new Error(`${c.env} is required`);
  const read=name=>JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));
  const receipt=read('domain-workflow-results.json'),world=read('baseline/world-domains.json'),checkpoint=read('baseline/domain-session.json');
  const sourceHash=crypto.createHash('sha256').update(fs.readFileSync(c.source)).digest('hex');
  assert.equal(receipt.Version,2);assert.equal(receipt.DefinitionFileSha256,sourceHash);assert.deepEqual(receipt.EditFiles,[]);
  assert.equal(receipt.InitialWorldSha256,receipt.FinalWorldSha256);assert.equal(receipt.InitialNodes,receipt.FinalNodes);
  assert.equal(receipt.BaselinePublishedWorldReplayed,true);assert.equal(receipt.CheckpointReplayed,true);
  assert.equal(world.Domains.length,c.domains);assert.equal(checkpoint.Edits.length,0);
  assert.equal(checkpoint.WorldSha256,receipt.FinalWorldSha256);assert.equal(receipt.FinalNodes,world.Domains.reduce((n,d)=>n+d.World.Nodes.length,0));
  assert.ok(world.Domains.some(d=>!!d.World.Surface)===c.physical);
  if(c.logical)assert.ok(world.Domains.some(d=>!d.World.Surface));
  if(c.corridor){
    const j=world.Journeys[0];assert.ok(j.Forward.some(s=>s.Kind==='corridor-segment'));
    assert.ok(j.ForwardCapabilityStates.some(s=>s.includes('key'))&&j.ReturnCapabilityStates[0].includes('key'));
  }
  assert.ok(fs.existsSync(path.join(root,'baseline/index.html')));
});
test('public SDK edited world retains explicit edit and checkpoint history',()=>{
  const root=process.env.SDK_FIRST_EDIT;if(!root)throw new Error('SDK_FIRST_EDIT is required');
  const receipt=JSON.parse(fs.readFileSync(path.join(root,'domain-workflow-results.json'),'utf8'));
  const checkpoint=JSON.parse(fs.readFileSync(path.join(root,'edited/domain-session.json'),'utf8'));
  assert.equal(receipt.Version,2);assert.equal(receipt.EditFiles.length,1);assert.equal(checkpoint.Edits.length,1);
  assert.equal(checkpoint.WorldSha256,receipt.FinalWorldSha256);assert.equal(receipt.CheckpointReplayed,true);
});
