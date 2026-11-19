const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_WIND_METADATA_PACKET||'generated/wind-metadata-current-cli';
const read=file=>JSON.parse(fs.readFileSync(`${root}/${file}`));
test('CLI binds original wind/environment inputs to effective occupied terrain',()=>{
 const w=read('world-metadata.json'),r=read('recipe.json'),p=read('wind-moisture-plan.json');
 assert.deepEqual(w.WindMoisturePlan,p);assert.deepEqual(p.AuthoredEnvironment,r.Environment);
 assert.deepEqual(p.Derivation.Surface,w.Surface);assert.notDeepEqual(w.Surface,w.DrainagePlan.Surface);
 assert.equal(p.AuthoredEnvironment.SampledFields,null);assert.equal(w.EnvironmentPlan.WindMoistureId,r.WindMoisture.Id);
 assert.deepEqual(p.Derivation.Definition.EvaporationByKind,r.WindMoisture.EvaporationByKind);
});
test('effective sampled values retain complete node identity and source quantities',()=>{
 const w=read('world-metadata.json'),p=w.WindMoisturePlan.Derivation,s=read('summary.json');
 assert.equal(w.Nodes.length,3);assert.equal(w.RegionPlan.Regions.length,2);assert.equal(w.RoutePlan.Routes.length,1);
 assert.equal(p.Nodes.length,25);assert.equal(s.WindMoistureSamples,25);assert.equal(s.WindMoistureFields,2);assert.equal(s.WindMoistureId,'weather');
 const fields=new Map(w.EnvironmentPlan.Definition.SampledFields.map(f=>[f.Id,f]));
 for(const n of p.Nodes){assert.equal(fields.get('weather-humidity').Values[n.SampleIndex],n.Outgoing);assert.equal(fields.get('weather-rainfall').Values[n.SampleIndex],n.Precipitation);}
});
test('current schematic preserves authoritative water/climate/world data without generating it',()=>{
 const w=read('world-metadata.json'),before=JSON.stringify(w),layout=S.layout(w);
 assert.equal(layout.coordinateSpace,'engine-surface');assert.equal(layout.boxes.length,3);assert.equal(layout.water.segments.length,4);
 assert.equal(JSON.stringify(w),before);assert.deepEqual(fs.readFileSync(`${root}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 const embedded=JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/schematic.html`,'utf8'))[1]);
 assert.deepEqual(embedded,w);
});
