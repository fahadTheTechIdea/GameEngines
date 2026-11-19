const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_PROGRAM_MIX_PACKET||'generated/program-mix-accepted/beds-8';
const world=JSON.parse(fs.readFileSync(path.join(root,'world-metadata.json')));
const check=w=>Schematic.programMixes(w,new Map(w.Nodes.map(n=>[n.Id,n])));
test('saved whole-program choices and balance witnesses reach schematic',()=>{
    assert.equal(check(world).length,1);
    assert.equal(Schematic.layout(world).boxes.length,world.Nodes.length);
    assert.equal(fs.readFileSync(path.join(root,'schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('changed cost and capacity balance refuse presentation',()=>{
    for(const change of [a=>a.Cost++,a=>a.Balances[0].Provided++,a=>a.SearchStates=a.Request.Rule.MaximumSearchStates+1,a=>a.Choices.pop()]){
        const w=structuredClone(world);change(w.ProgramMixPlan.Allocations[0]);assert.throws(()=>check(w),/Invalid program mix/);
    }
});
test('missing receipts and missing or extra members refuse presentation',()=>{
    const a=world.ProgramMixPlan.Allocations[0],id=a.Choices[0].NodeId;
    for(const change of [w=>w.ProgramMixPlan=null,w=>w.Nodes=w.Nodes.filter(n=>n.Id!==id),w=>w.Nodes.find(n=>n.Id===id).ProgramMixKey=null,
        w=>w.Nodes.push({...w.Nodes.find(n=>n.Id===id),Id:a.Request.ParentNodeId+'/'+a.Request.PartId+'-99'})]){
        const w=structuredClone(world);change(w);assert.throws(()=>check(w),/Invalid program mix/);
    }
});
