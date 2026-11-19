const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_REGION_FEEDBACK_PACKET||'generated/region-feedback-final/terrain-reselection';
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
test('accepted regional decision and every required member reach current schematic',()=>{
    const p=Schematic.programComposition(world);
    assert.deepEqual(p.Attempts.at(-1).RegionSelection,world.RegionPlan);
    assert.equal(Schematic.layout(world).boxes.length,world.Nodes.length);
    assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('changed or missing regional composition decisions refuse presentation',()=>{
    for(const change of [w=>w.ProgramCompositionPlan.RegionWork=0,w=>w.ProgramCompositionPlan.Attempts.at(-1).RegionSelection=null,
        w=>w.ProgramCompositionPlan.Attempts.at(-1).RegionSelection.Work++,w=>w.ProgramCompositionPlan=null]){
        const w=structuredClone(world);change(w);assert.throws(()=>Schematic.programComposition(w),/Invalid program composition/);
    }
});
test('selected assignment cannot also appear among prior excluded assignments',()=>{
    const w=structuredClone(world),p=w.RegionPlan;
    const selected=p.Request.Intents.map(i=>p.IntentPlan.Selections.find(s=>s.IntentId===i.Id).Component);
    p.Request.ExcludedAssignments=[...(p.Request.ExcludedAssignments||[]),selected];
    assert.throws(()=>Schematic.regionIntentSeeds(p),/Invalid region intent/);
});
