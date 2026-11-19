const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.DOMAIN_PROGRESSION_WORLD;
if(!root)throw new Error('DOMAIN_PROGRESSION_WORLD must identify the generated world');
test('saved gameplay path obeys source links, domain identities and capability gate',()=>{
  const world=JSON.parse(fs.readFileSync(path.join(root,'world.json'),'utf8'));
  const p=world.Domains.Progression.Results[0].Paths[0],nodes=p.Nodes,links=p.Links,states=p.CapabilityStates;
  assert.equal(links.length,nodes.length-1);assert.equal(states.length,nodes.length);
  const key=nodes.findIndex(n=>n.DomainId==='lower'&&n.NodeId==='generated-interior/rooms-0');assert.ok(key>=0);
  for(let i=0;i<links.length;i++){
    const link=links[i],a=nodes[i],b=nodes[i+1];
    if(link.DomainId==null){
      const t=world.Domains.Transitions.find(t=>t.Definition.Id===link.ConnectionId);assert.ok(t);
      const f=t.From.Definition,to=t.To.Definition,eq=(n,e)=>n.DomainId===e.DomainId&&n.NodeId===e.NodeId;
      assert.ok(eq(a,f)&&eq(b,to)||t.Definition.Bidirectional&&eq(a,to)&&eq(b,f));
      if(link.ConnectionId==='stairs'){assert.ok(i>key);assert.ok(states[i].includes('key'));}
    }else{
      assert.equal(a.DomainId,link.DomainId);assert.equal(b.DomainId,link.DomainId);
      const domain=world.Domains.Domains.find(d=>d.Id===link.DomainId).World;
      const c=domain.Connections.find(c=>c.Id===link.ConnectionId);assert.ok(c);
      assert.ok(c.FromId===a.NodeId&&c.ToId===b.NodeId||c.Bidirectional&&c.ToId===a.NodeId&&c.FromId===b.NodeId);
    }
  }
  assert.ok(world.Domains.Journeys[0].Forward.some(s=>s.SourceId==='stairs'));
  const html=fs.readFileSync(path.join(root,'index.html'),'utf8');assert.ok(html.includes('Gameplay progression')&&html.includes('capabilities: key'));
});
