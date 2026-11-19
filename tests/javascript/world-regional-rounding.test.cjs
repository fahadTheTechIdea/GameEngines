const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const folder=process.env.WORLD_REGIONAL_ROUNDING;
if(!folder)throw new Error('WORLD_REGIONAL_ROUNDING must point to the generated regional world');
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-regions.json'),'utf8'));
test('saved regional schematic retains complete content and actual rounded paths',()=>{
    const plan=R.layout(world);
    assert.equal(plan.boxes.length,10);
    for(const route of [world.Connections[0].Forward,world.Connections[0].Reverse]){
        assert.ok(route.RoundingWitness);
        assert.ok(route.Path.Points.length>route.RoundingWitness.SourcePath.Points.length);
    }
    const html=fs.readFileSync(path.join(folder,'schematic.html'),'utf8');
    assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),world);
    assert.equal(fs.readFileSync(path.join(folder,'regional-schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js','utf8'));
});
test('regional viewer rejects a changed or missing rounding witness',()=>{
    for(const change of [r=>delete r.RoundingWitness,r=>r.RoundingWitness.SourceCost.Distance++]){
        const corrupted=structuredClone(world);change(corrupted.Connections[0].Forward);
        assert.throws(()=>R.layout(corrupted),/Invalid regional schematic/);
    }
});
