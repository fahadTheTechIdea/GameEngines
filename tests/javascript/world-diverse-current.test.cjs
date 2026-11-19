const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_DIVERSE_CURRENT_ROOT||'generated';
const packets=['inland','coastal'].map(name=>{
 const dir=path.resolve(root,`current-world-${name}-20261101`);
 return {name,world:JSON.parse(fs.readFileSync(path.join(dir,'world-metadata.json'),'utf8')),S:require(path.join(dir,'schematic.js'))};
});
for(const {name,world:w,S} of packets){
 test(`${name}: declared content and all physical routes reach the saved schematic`,()=>{
  const v=S.layout(w),counts=Object.fromEntries([...new Set(w.Nodes.map(n=>n.Kind))].sort().map(k=>[k,w.Nodes.filter(n=>n.Kind===k).length]));
  assert.deepEqual(counts,{'compound':7,'environment-feature':18,'equipment':13,'ground-area':9,'region':1,'structure':13});
  assert.equal(w.Coverage,'PartialScope');assert.equal(w.Nodes.length,61);assert.equal(w.AccessPlan.Sites.length,60);
  assert.equal(w.RoutePlan.Routes.length,51);assert.equal(w.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges).length,6);
  assert.ok(w.RoutePlan.Routes.every(r=>r.Forward.Status==='Found'&&r.Reverse?.Status==='Found'));
  assert.ok(w.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges).every(e=>e.Forward.Status==='Found'&&e.Reverse?.Status==='Found'));
  assert.equal(v.boxes.length,61);assert.equal(v.routes.length,114);
 });
 test(`${name}: altered physical route endpoint is rejected`,()=>{
  const changed=structuredClone(w);changed.RoutePlan.Routes[0].Forward.Path.Points[0].X+=1;
  assert.throws(()=>S.layout(changed),/Invalid route geometry/);
 });
}
test('coastal terrain changes actual positions and routes without dropping declared nodes',()=>{
 const [a,b]=packets.map(p=>p.world),ids=w=>w.Nodes.map(n=>n.Id).sort(),byId=new Map(b.Nodes.map(n=>[n.Id,n]));
 assert.deepEqual(ids(a),ids(b));assert.notDeepEqual(a.Surface,b.Surface);
 assert.ok(a.Nodes.some(n=>JSON.stringify(n.Position)!==JSON.stringify(byId.get(n.Id).Position)));
 assert.notDeepEqual(a.LocalLayoutPlan,b.LocalLayoutPlan);assert.notDeepEqual(a.RoutePlan,b.RoutePlan);assert.notDeepEqual(a.NetworkPlan,b.NetworkPlan);
});
