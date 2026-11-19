const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const prefix=process.env.WORLD_CORE_MATRIX_PREFIX||'generated/core-matrix-cli-';
test('current matrix packets preserve every saved node in the common schematic',()=>{
 for(const [name,count] of [['ridge-north',9],['ridge-south',9],['interior-rect',13],['interior-hex',13],['mixed-inland',43],['mixed-coastal',43],['topology',25]]){
  const w=JSON.parse(fs.readFileSync(prefix+name+'/world-metadata.json','utf8')),before=JSON.stringify(w);
  assert.equal(S.layout(w).boxes.length,count);assert.equal(JSON.stringify(w),before);
  if(name==='topology'){assert.equal(w.Surface,null);assert.equal(w.PlacementPlan,null);assert.ok(w.Nodes.every(n=>n.Position==null));}
  assert.equal(fs.readFileSync(prefix+name+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
