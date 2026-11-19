const {test}=require('node:test');
const assert=require('node:assert/strict');
const WorldSchematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');

test('rounded route witness preserves original endpoints and validates source distance',()=>{
    const route={Status:'Found',SearchModel:'feature-visibility-v1/first-feasible/rounded-polyline-v1',
        Path:{Points:[{X:0,Y:0},{X:1,Y:0.75},{X:2,Y:1}]},
        RoundingWitness:{SourcePath:{Points:[{X:0,Y:0},{X:1,Y:1},{X:2,Y:1}]},
            SourceCost:{Distance:Math.SQRT2+1,Terrain:2,Ascent:0,Descent:0}}};
    const fail=()=>{throw new Error('invalid witness');};
    assert.doesNotThrow(()=>WorldSchematic.routeRoundingWitness(route,fail));
    const altered=structuredClone(route);altered.RoundingWitness.SourceCost.Distance=1;
    assert.throws(()=>WorldSchematic.routeRoundingWitness(altered,fail));
    const missing=structuredClone(route);delete missing.RoundingWitness;
    assert.throws(()=>WorldSchematic.routeRoundingWitness(missing,fail));
    const moved=structuredClone(route);moved.Path.Points[0].X=1;
    assert.throws(()=>WorldSchematic.routeRoundingWitness(moved,fail));
});

test('ordered rounding alternatives preserve valid bounded attempt accounting',()=>{
    const route={Status:'Found',SearchModel:'feature-visibility-v1/first-feasible/rounded-polyline-v1',EdgeChecks:8,Work:100,
        Path:{Points:[{X:0,Y:0},{X:1,Y:.75},{X:2,Y:1}]},
        RoundingWitness:{SourcePath:{Points:[{X:0,Y:0},{X:1,Y:1},{X:2,Y:1}]},SourceCost:{Distance:Math.SQRT2+1,Terrain:2,Ascent:0,Descent:0}},
        RoundingAttempts:[{MaximumDeviation:4,Status:'TraceRejected',EdgeChecks:2,Work:20},
            {MaximumDeviation:.1,Status:'Found',EdgeChecks:3,Work:30}]};
    const fail=()=>{throw new Error('invalid witness');};
    assert.doesNotThrow(()=>WorldSchematic.routeRoundingWitness(route,fail));
    for(const mutate of [r=>r.RoundingAttempts[1].MaximumDeviation=5,r=>r.RoundingAttempts[0].Status='Found',
        r=>r.RoundingAttempts[1].Work=100,r=>r.RoundingAttempts[1].EdgeChecks=20]){
        const changed=structuredClone(route);mutate(changed);assert.throws(()=>WorldSchematic.routeRoundingWitness(changed,fail));
    }
});
