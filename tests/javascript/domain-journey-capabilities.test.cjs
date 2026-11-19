const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.DOMAIN_JOURNEY_CAPABILITIES_WORLD;
if(!root)throw new Error('DOMAIN_JOURNEY_CAPABILITIES_WORLD must identify the generated output');
test('physical journey acquires the key before the gate and retains it for return',()=>{
  const world=JSON.parse(fs.readFileSync(path.join(root,'world.json'),'utf8'));
  const j=world.Domains.Journeys[0],states=j.ForwardCapabilityStates,back=j.ReturnCapabilityStates;
  const entry=j.Forward.findIndex(s=>s.Kind==='transition'&&s.SourceId==='entry');
  const stairs=j.Forward.findIndex(s=>s.Kind==='transition'&&s.SourceId==='stairs');
  assert.ok(entry>=0&&stairs>entry);assert.equal(states.length,j.Forward.length+1);assert.equal(back.length,j.Return.length+1);
  assert.ok(!states[entry].includes('key'));assert.ok(states[entry+1].includes('key'));assert.ok(states[stairs].includes('key'));assert.ok(back[0].includes('key'));
  for(const sequence of [states,back])for(let i=1;i<sequence.length;i++)for(const id of sequence[i-1])assert.ok(sequence[i].includes(id));
  const html=fs.readFileSync(path.join(root,'index.html'),'utf8'),start=html.indexOf('Explicit domain change stairs');
  assert.ok(start>=0&&html.slice(start,html.indexOf('</li>',start)).includes('capabilities: key'));
});
