const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_WATER_PACKET||'generated/water-metadata-final',read=f=>JSON.parse(fs.readFileSync(`${root}/${f}`));
const near=(a,b)=>assert.ok(Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b)),`${a}/${b}`);

test('actual CLI packet preserves original terrain and publishes exact water/effective surface',()=>{
 const w=read('world-metadata.json'),p=read('water-plan.json'),d=read('drainage-plan.json'),r=read('recipe.json'),s=read('summary.json');
 assert.deepEqual(w.WaterPlan,p);assert.deepEqual(w.DrainagePlan,d);assert.deepEqual(d.Surface.HeightField,w.Surface.HeightField);
 assert.deepEqual(d.Surface.HeightField,r.Surface.HeightField);assert.equal(d.Surface.Patches.length,0);assert.equal(w.Surface.Patches.length,4);
 assert.equal(w.Nodes.length,3);assert.equal(w.Connections.length,1);assert.equal(s.WetSegments,4);assert.equal(s.WetPatches,4);assert.equal(s.WetKind,'water');
 assert.equal(w.RoutePlan.Routes.length,1);assert.equal(w.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(w.RoutePlan.Routes[0].Reverse.Status,'Found');
});
test('current schematic accepts and annotates every actual wet patch without mutating data',()=>{
 const w=read('world-metadata.json'),before=JSON.stringify(w),layout=S.layout(w);
 assert.equal(layout.coordinateSpace,'engine-surface');assert.equal(layout.boxes.length,3);assert.equal(layout.water.segments.length,4);
 assert.equal(layout.water.byPatch.size,4);assert.equal(layout.terrain.filter(p=>p.water).length,4);
 for(const p of layout.terrain.filter(p=>p.water)){assert.equal(p.kind,'water');assert.equal(p.water.Width,.5);assert.equal(p.water.Flow,16);}
 assert.equal(layout.water.rateUnit,'liters-per-second');assert.equal(JSON.stringify(w),before);
 assert.deepEqual(fs.readFileSync(`${root}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('independent complete-band area and required dry component accounting',()=>{
 const w=read('world-metadata.json'),radius=.25/Math.cos(Math.PI/16),expected=16-8*radius;
 assert.equal(w.RegionPlan.Regions.length,2);near(w.RegionPlan.SuitableArea,expected);near(w.RegionPlan.Regions.reduce((s,r)=>s+r.Area,0),expected);
 const ids=new Set(w.Nodes.map(n=>n.Id));assert.ok(w.Connections.every(c=>ids.has(c.FromId)&&ids.has(c.ToId)));
 for(const r of w.RegionPlan.Regions)assert.ok(r.Seed.Position.Y<2-radius||r.Seed.Position.Y>2+radius);
});
test('saved witness rejects orphan dependencies, altered rates/widths, patches and effective composition',()=>{
 const base=read('world-metadata.json');
 const edits=[w=>w.DrainagePlan=null,w=>w.Surface=null,w=>w.WaterPlan.Segments[0].Flow++,w=>w.WaterPlan.Segments[0].Width++,
  w=>w.WaterPlan.Segments.pop(),w=>w.WaterPlan.Patches[0].Kind='land',w=>w.WaterPlan.Segments[0].PatchIds=[],
  w=>w.WaterPlan.Segments[0].RetainedArea++,w=>w.WaterPlan.BelowThresholdEdges++,w=>w.WaterPlan.DrainageSha256='bad',
  w=>w.Surface.Patches[0].Elevation++,w=>w.Surface.HeightField.Heights[0]++,w=>w.WaterPlan.Work=w.WaterPlan.Definition.MaximumWork+1,
  w=>w.WaterPlan.Patches[0].Priority=w.WaterPlan.Patches[1].Priority];
 for(const edit of edits){const w=structuredClone(base);edit(w);assert.throws(()=>S.layout(w));}
});
test('bounded viewer checks retain engine authority rather than generating alternate water',()=>{
 const w=read('world-metadata.json'),layout=S.layout(w);
 const ids=new Set(w.WaterPlan.Patches.map(p=>p.Id));assert.deepEqual(layout.terrain.filter(p=>p.water).map(p=>p.id).sort(),[...ids].sort());
 for(const p of layout.terrain){const source=w.Surface.Patches.find(s=>s.Id===p.id);assert.deepEqual(p.points,source.Boundary.Vertices.map(v=>({x:v.X,y:v.Y})));}
});
