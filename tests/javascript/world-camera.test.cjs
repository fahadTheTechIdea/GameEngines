const {test}=require('node:test'),assert=require('node:assert/strict');
const WorldSchematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
function camera(coordinateSpace,view=[141,48,12,15.3]){
 return {plan:{coordinateSpace,width:194.4},view:[...view],applied:0,applyView(){this.applied++;}};
}
for(const space of ['engine-surface','engine-world-meters'])test(space+': focused source can zoom in and return without moving its center',()=>{
 const c=camera(space),original=[...c.view],center=[c.view[0]+c.view[2]/2,c.view[1]+c.view[3]/2];
 WorldSchematic.prototype.zoom.call(c,1.2);assert.equal(c.applied,1);assert.equal(c.view[2],10);
 assert.ok(Math.abs(c.view[0]+c.view[2]/2-center[0])<1e-10);assert.ok(Math.abs(c.view[1]+c.view[3]/2-center[1])<1e-10);
 WorldSchematic.prototype.zoom.call(c,1/1.2);assert.equal(c.applied,2);c.view.forEach((v,i)=>assert.ok(Math.abs(v-original[i])<1e-10));
});
test('surface focus can zoom out even when below the diagram zoom minimum',()=>{
 const c=camera('engine-surface');WorldSchematic.prototype.zoom.call(c,1/1.2);assert.equal(c.applied,1);assert.ok(Math.abs(c.view[2]-14.4)<1e-10);
});
test('engine camera keeps finite declared minimum/maximum without changing plan geometry',()=>{
 const c=camera('engine-surface'),plan=JSON.stringify(c.plan),view=[...c.view];
 for(const factor of [1000,.0001])WorldSchematic.prototype.zoom.call(c,factor);
 assert.equal(c.applied,0);assert.deepEqual(c.view,view);assert.equal(JSON.stringify(c.plan),plan);
});
test('logical diagram retains its own diagram-unit zoom bound',()=>{
 const c=camera(null);WorldSchematic.prototype.zoom.call(c,1.2);assert.equal(c.applied,0);
 c.view=[0,0,100,80];WorldSchematic.prototype.zoom.call(c,1.2);assert.equal(c.applied,1);
});
