const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const roots=process.env.WORLD_DIRECTIONAL_CAPTURE_ROOTS?JSON.parse(process.env.WORLD_DIRECTIONAL_CAPTURE_ROOTS):[
 'generated/terrain-directional-final-sdk-20261101/captured','generated/terrain-directional-protected-capture-cli-20261101'];
for(const root of roots)test(root+': captured samples and charged work survive saved inspection',()=>{
 const directory=path.resolve(root),world=JSON.parse(fs.readFileSync(path.join(directory,'world-metadata.json'),'utf8'));
 const S=require(path.join(directory,'schematic.js')),field=S.surfaceHeightField(world.Surface),window=world.Surface.TerrainCapturePlan.Window,terrain=world.Surface.Terrain;
 assert.equal(field.values.length,window.Rows*window.Columns);
 const operations=terrain.Operations.reduce((sum,op)=>{const segments=Math.max(1,op.ControlPath.length-1);return sum+segments*(op.DetailProtectionRadius==null?1:2)},0);
 const transformed=(terrain.Detail.OrientationDegrees??0)!==0||(terrain.Detail.AxisStretch??1)!==1;
 assert.equal(window.Work,(1+operations+terrain.Detail.Octaves*4+(transformed?5:0))*window.Rows*window.Columns);
 const altered=structuredClone(world.Surface);altered.TerrainCapturePlan.Window.Work++;assert.throws(()=>S.surfaceHeightField(altered),/Invalid terrain capture/);
 assert.equal(S.layout(world).boxes.length,world.Nodes.length);
});
