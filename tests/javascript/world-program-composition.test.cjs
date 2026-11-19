const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_PROGRAM_COMPOSITION_PACKET||'generated/program-composition-final/terrain-fit';
const world=JSON.parse(fs.readFileSync(path.join(folder,'world-metadata.json')));
test('exact saved composition and all physical members reach schematic',()=>{
    assert.equal(Schematic.programComposition(world).Attempts.at(-1).Accepted,true);
    assert.equal(Schematic.layout(world).boxes.length,world.Nodes.length);
    assert.equal(fs.readFileSync(path.join(folder,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('missing or changed composition receipts refuse presentation',()=>{
    for(const change of [w=>w.ProgramCompositionPlan=null,w=>w.ProgramCompositionId=null,w=>w.ProgramCompositionPlan.Attempts.at(-1).Accepted=false,
        w=>w.ProgramCompositionPlan.MixWork=w.ProgramCompositionPlan.Definition.MaximumMixWork+1,
        w=>w.ProgramCompositionPlan.Attempts[0].Attempt=9,w=>w.ProgramCompositionPlan.Attempts.at(-1).Selections[0].Cost++]){
        const w=structuredClone(world);change(w);assert.throws(()=>Schematic.programComposition(w),/Invalid program composition/);
    }
});
test('C# owns rejection replay, viewer retains the engine attempt history',()=>{
    const plan=Schematic.programComposition(world);
    assert.ok(plan.Attempts.length>=2);
    assert.ok(plan.Attempts.slice(0,-1).every(a=>!a.Accepted&&a.Rejection));
    assert.ok(plan.Selections>=plan.Attempts.reduce((n,a)=>n+a.Selections.length,0));
});
