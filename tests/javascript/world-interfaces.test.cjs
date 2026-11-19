const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_INTERFACE_PACKET||'generated/local-interfaces-final',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('physical interfaces preserve saved crossings, full routes and required content',()=>{
 const view=Schematic.layout(world);assert.equal(view.boxes.length,22);assert.equal(view.interfaceRoutes.length,2);assert.equal(view.routes.length,4);
 for(const entry of world.RoutePlan.Routes){
  const rendered=view.interfaceRoutes.find(i=>i.connectionId===entry.ConnectionId);assert.deepEqual(rendered.Forward,entry.Interface.Forward);
  for(const route of [entry.Forward,entry.Reverse])assert.deepEqual(view.routes.find(r=>r.id===route.Id).points,route.Path.Points.map(p=>({x:p.X,y:p.Y})));
 }
});
test('missing or inconsistent interface geometry and attempts refuse display',()=>{
 for(const mutate of [w=>w.RoutePlan.Routes[0].Interface=null,w=>w.RoutePlan.Rules[0].Interfaces=null,
  w=>w.RoutePlan.Routes[0].Interface.Center.X+=1,w=>w.RoutePlan.Routes[0].Interface.Forward.Crossing.Path.Points[0].X+=1,
  w=>w.RoutePlan.Routes[0].Interface.Forward.FromLeg.Cost.Distance+=1,w=>w.RoutePlan.Routes[0].Interface.Attempts=[],
  w=>w.RoutePlan.Routes[0].Interface.Attempts[0].Accepted=false,w=>w.RoutePlan.Routes[0].Interface.Reverse=null,
  w=>w.RoutePlan.Rules[0].Interfaces[0].ConnectionId='absent',w=>w.RoutePlan.Routes[0].Interface.ToDomainId='missing']){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/physical interface/);
 }
});
test('ordinary and allocation-only packets do not require physical interface bindings',()=>{
 for(const folder of ['local-layouts','terrain-routes','corridor-capacity']){
  const existing=JSON.parse(fs.readFileSync(`generated/${folder}/world-metadata.json`));assert.deepEqual(Schematic.layout(existing).interfaceRoutes,[]);
 }
});
test('new packet embeds exact current renderer and metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
