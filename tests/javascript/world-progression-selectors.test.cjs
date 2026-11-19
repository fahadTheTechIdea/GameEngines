const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const root=process.env.WORLD_PROGRESSION_SELECTORS||'generated/s03-progression-selectors-final-20261102';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}`));
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const key=n=>JSON.stringify([n.RegionId,n.NodeId]);
const ref=n=>JSON.stringify([n.RegionId,n.NodeId,n.Selector?.NodeKind??null,n.Selector?.ArchetypeId??null]);

function qualify(world,rule,result){
 const regionMap=new Map(world.Regions.map(r=>[r.RegionId,r.World]));
 const resolve=n=>{
  if(!n.Selector)return {RegionId:n.RegionId,NodeId:n.NodeId};
  const matches=regionMap.get(n.RegionId).Nodes.filter(v=>v.Kind===n.Selector.NodeKind&&(!n.Selector.ArchetypeId||v.ArchetypeId===n.Selector.ArchetypeId));
  assert.equal(matches.length,1);return {RegionId:n.RegionId,NodeId:matches[0].Id};
 };
 const sources=[rule.Start,...rule.Goals,...rule.RequiredMilestones,...(rule.Capabilities?.Grants??[]).map(g=>g.Node)];
 const selected=new Map(sources.filter(n=>n.Selector).map(n=>[ref(n),n]));
 assert.equal(result.Bindings.length,selected.size);
 for(const binding of result.Bindings){assert.ok(selected.has(ref(binding.Source)));assert.deepEqual(binding.Resolved,resolve(binding.Source));assert.equal(binding.Resolved.Selector,undefined);}
 const adjacency=new Map(),edges=new Map();
 for(const [regionId,local] of regionMap)for(const n of local.Nodes)adjacency.set(key({RegionId:regionId,NodeId:n.Id}),[]);
 const add=(id,kind,a,b,both)=>{
  if(!rule.ConnectionKinds.includes(kind))return;
  const from=key(a),to=key(b),link=JSON.stringify([id.RegionId,id.ConnectionId]);
  edges.set(link,{from,to,both});adjacency.get(from).push({to,link});if(both)adjacency.get(to).push({to:from,link});
 };
 for(const [regionId,local] of regionMap)for(const e of local.Connections)add({RegionId:regionId,ConnectionId:e.Id},e.Kind,
  {RegionId:regionId,NodeId:e.FromId},{RegionId:regionId,NodeId:e.ToId},e.Bidirectional);
 for(const e of world.Connections)add({RegionId:null,ConnectionId:e.Definition.Id},e.Definition.Kind,e.Definition.From,e.Definition.To,e.Definition.Bidirectional);
 const grants=new Map((rule.Capabilities?.Grants??[]).map(g=>[key(resolve(g.Node)),g.CapabilityIds]));
 const gates=new Map((rule.Capabilities?.Gates??[]).map(g=>[JSON.stringify([g.Link.RegionId,g.Link.ConnectionId]),g.RequiredCapabilityIds]));
 const gain=(node,caps)=>[...new Set([...caps,...(grants.get(node)??[])])].sort();
 const start=key(resolve(rule.Start)),initial=gain(start,rule.Capabilities?.InitialCapabilityIds??[]);
 const advance=state=>adjacency.get(state.node).filter(e=>(gates.get(e.link)??[]).every(c=>state.caps.includes(c)))
  .map(e=>({node:e.to,caps:gain(e.to,state.caps)}));
 const reachable=first=>{
  const queue=[first],seen=new Set([JSON.stringify(first)]);
  for(let i=0;i<queue.length;i++)for(const next of advance(queue[i])){const k=JSON.stringify(next);if(!seen.has(k)){seen.add(k);queue.push(next);}}
  return queue;
 };
 const all=reachable({node:start,caps:initial});
 for(const goal of rule.Goals)assert.ok(all.some(s=>s.node===key(resolve(goal))));
 if(rule.Capabilities?.RequireReturnToStart)for(const state of all)assert.ok(reachable(state).some(s=>s.node===start),'every reachable state has its own forward return witness');
 for(const path of result.Paths){
  assert.deepEqual(path.Nodes[0],resolve(rule.Start));assert.equal(path.Links.length,path.Nodes.length-1);
  assert.ok(rule.Goals.some(g=>key(resolve(g))===key(path.Goal)));
  let caps=initial,lastMilestone=-1;
  for(const milestone of rule.RequiredMilestones){const index=path.Nodes.findIndex(n=>key(n)===key(resolve(milestone)));assert.ok(index>lastMilestone);lastMilestone=index;}
  if(path.CapabilityStates)assert.deepEqual(path.CapabilityStates[0],caps);
  for(let i=0;i<path.Links.length;i++){
   const link=path.Links[i],id=JSON.stringify([link.RegionId,link.ConnectionId]),e=edges.get(id),a=key(path.Nodes[i]),b=key(path.Nodes[i+1]);
   assert.ok(e&&((e.from===a&&e.to===b)||(e.both&&e.from===b&&e.to===a)));
   assert.ok((gates.get(id)??[]).every(c=>caps.includes(c)));caps=gain(b,caps);
   if(path.CapabilityStates)assert.deepEqual(path.CapabilityStates[i+1],caps);
  }
 }
 if(rule.Capabilities)assert.equal(result.ReachableStates,all.length);
}

test('complete alternatives bind semantic objectives to actual renamed nodes',()=>{
 const world=read('world/world-regions.json'),receipt=read('world/world-design.json'),before=JSON.stringify(world);
 assert.deepEqual(receipt.Attempts.map(a=>a.Accepted),[false,true]);assert.match(receipt.Attempts[0].Rejection,/^METADATA_REGIONAL_PROGRESSION_SELECTOR_COUNT:.*:0$/);
 assert.equal(world.Regions.flatMap(r=>r.World.Nodes).length,20);assert.equal(world.Regions.flatMap(r=>r.World.AccessPlan.Sites).length,12);
 assert.equal(world.Connections.length,3);assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
 assert.ok(receipt.Attempts.at(-1).WorldCounts.every(c=>c.Satisfied));
 qualify(world,receipt.Source.WorldProgression[0],receipt.Attempts.at(-1).WorldProgression.Results[0]);
 assert.equal(R.layout(world).boxes.length,20);assert.equal(JSON.stringify(world),before);
});

test('independent product-state exploration proves saved capability paths and every-state return',()=>{
 qualify(read('world/world-regions.json'),read('gated-rule.json'),read('gated-plan.json').Results[0]);
});

test('domain adapter retains selector provenance and independent physical floor journeys',()=>{
 const dir=process.env.WORLD_PROGRESSION_SELECTOR_DOMAINS||'generated/s03-progression-selector-domains-current-20261102';
 const world=JSON.parse(fs.readFileSync(`${dir}/world/world.json`)),input=JSON.parse(fs.readFileSync(`${dir}/definition.json`));
 const result=world.Domains.Progression.Results[0],binding=result.Bindings[0],source=input.Progression[0].Start;
 assert.deepEqual(binding.Source,source);assert.equal(binding.Resolved.DomainId,source.DomainId);assert.equal(binding.Resolved.Selector,undefined);
 const domain=world.Domains.Domains.find(d=>d.Id===source.DomainId).World;
 const matches=domain.Nodes.filter(n=>n.Kind===source.Selector.NodeKind&&n.ArchetypeId===source.Selector.ArchetypeId);
 assert.equal(matches.length,1);assert.equal(binding.Resolved.NodeId,matches[0].Id);assert.deepEqual(result.Paths[0].Nodes[0],binding.Resolved);
 assert.deepEqual(world.Selection.Attempts.map(a=>a.Accepted),[false,true]);assert.equal(world.Domains.Domains.length,6);
 assert.ok(world.Domains.Journeys[0].Forward.some(s=>s.SourceId==='stairs'));assert.ok(world.Domains.Journeys[0].Return.length>0);
});

test('CLI entry-role edit reconsiders complete programs and preserves unrelated accepted regions',()=>{
 const dir=process.env.WORLD_PROGRESSION_SELECTOR_SESSION||'generated/s03-selector-session-20261102';
 const file=name=>JSON.parse(fs.readFileSync(`${dir}/${name}`));
 const a=file('baseline/world-regions.json'),b=file('edited/world-regions.json'),clean=file('clean/world-regions.json');
 const initial=file('baseline/generation-session.json'),edited=file('edited/generation-session.json'),report=edited.Edits[0].Report;
 assert.equal(edited.Edits.length,1);assert.deepEqual(b,clean);assert.notEqual(edited.WorldSha256,initial.WorldSha256);
 assert.deepEqual(report.Regions.filter(r=>r.Reused).map(r=>r.RegionId).sort(),['grove','outpost','pass']);
 for(const id of ['grove','outpost','pass'])assert.deepEqual(a.Regions.find(r=>r.RegionId===id).World,b.Regions.find(r=>r.RegionId===id).World);
 for(const world of [a,b]){
  assert.equal(world.Regions.flatMap(r=>r.World.Nodes).length,20);assert.equal(world.Regions.flatMap(r=>r.World.AccessPlan.Sites).length,12);
  assert.equal(world.Connections.length,3);assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
 }
 assert.equal(initial.InitialReport.DesignAttempts.at(-1).Selections[0].OptionId,'different-paths');
 assert.equal(report.DesignAttempts.at(-1).Selections[0].OptionId,'missing-role');
 const changed=file('edited-source.json');qualify(b,changed.WorldProgression[0],report.DesignAttempts.at(-1).WorldProgression.Results[0]);
 assert.equal(file('refusal.json').AcceptedOutputPreserved,true);assert.equal(file('refusal.json').Published,false);
 assert.equal(fs.existsSync(`${dir}/refused`),false);assert.match(fs.readFileSync(`${dir}/refused.log`,'utf8'),/METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED/);
});
