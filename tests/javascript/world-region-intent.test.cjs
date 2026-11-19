const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_REGION_INTENT_PACKET||'generated/region-intent-accepted/west-pond';
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
test('all source members and terrain-derived region witnesses reach schematic',()=>{
    assert.equal(Schematic.layout(world).boxes.length,17);
    const p=Schematic.generatedRegions(world);
    assert.equal(p.Request.Seeds.length,0);
    assert.equal(Schematic.regionIntentSeeds(p).length,1);
    assert.equal(world.PlacementPlan.TemplateDefinition.Request.Anchors.length,0);
    assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('changed clearance, component selection and evaluation receipts refuse presentation',()=>{
    for(const change of [p=>p.IntentPlan=null,p=>p.IntentPlan.AnchorCells++,p=>p.IntentPlan.Selections[0].Seed.Position.X++,
        p=>p.IntentPlan.Candidates[0].Anchor.UpperBound+=p.Request.AnchorPrecision*2,
        p=>p.IntentPlan.Candidates[0].Anchor.Clearance=p.Request.Intents[0].MinimumClearance-1]){
        const w=structuredClone(world);change(w.RegionPlan);assert.throws(()=>Schematic.generatedRegions(w),/Invalid region intent/);
    }
});
test('computed region anchor binds placement exactly and cannot be replaced by a point',()=>{
    const w=structuredClone(world);w.PlacementPlan.Definition.Request.Anchors.find(a=>a.Id==='arrival').Position.X++;
    assert.throws(()=>Schematic.generatedRegions(w),/Invalid generated region plan/);
    const absent=structuredClone(world);absent.RegionPlan=null;assert.throws(()=>Schematic.generatedRegions(absent),/Invalid generated region plan/);
});
