const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_NETWORK_PACKET||'generated/destination-network';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('generated network preserves required destinations and exact selected route geometry',()=>{
  const network=world.NetworkPlan.Networks[0].Plan,plan=Schematic.layout(world);
  assert.equal(network.Status,'Found');assert.equal(network.Destinations.length,4);
  assert.equal(network.Analysis.Components.length,1);assert.deepEqual(network.Analysis.Bridges,[]);
  assert.equal(world.Connections.length,network.Edges.length);
  assert.equal(plan.routes.length,network.Edges.length*2);
  for(const edge of network.Edges)for(const result of [edge.Forward,edge.Reverse]){
    const route=plan.routes.find(r=>r.id===result.Id);assert.ok(route);
    assert.deepEqual(route.points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
    assert.equal(route.width,network.Options.Profile.Radius*2);
  }
  assert.equal(plan.boxes.length,5);assert.deepEqual(plan.corridors,[]);
});

test('independent traversal still connects all destinations after removing each selected edge',()=>{
  const network=world.NetworkPlan.Networks[0].Plan,ids=network.Destinations.map(d=>d.Id);
  for(const removed of network.Edges){
    const visited=new Set([ids[0]]),queue=[ids[0]];
    while(queue.length){const current=queue.shift();for(const {Link:e} of network.Edges.filter(e=>e!==removed)){
      const next=e.FromId===current?e.ToId:e.ToId===current?e.FromId:null;
      if(next&&!visited.has(next)){visited.add(next);queue.push(next);}
    }}
    assert.equal(visited.size,ids.length);
  }
});

test('malformed network presentation rejects and current viewer is packaged exactly',()=>{
  for(const edit of [w=>w.NetworkPlan.Networks[0].Plan.Status='RedundancyUnresolved',w=>w.NetworkPlan.Networks[0].RuleId='missing',
    w=>w.NetworkPlan.Networks[0].Plan.Edges[0].Forward.Path.Points[0].X+=4]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const html=fs.readFileSync(`${folder}/schematic.html`,'utf8');
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
});
