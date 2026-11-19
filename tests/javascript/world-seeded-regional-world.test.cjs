const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Regional=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SEEDED_REGIONAL_ROOT||'generated/seeded-world-variety-20261101';
for(const seed of ['world-0','world-1']) test(`${seed}: inspect the complete varied regional world`,()=>{
 const folder=`${root}/${seed}`,world=JSON.parse(fs.readFileSync(`${folder}/world-regions.json`,'utf8'));
 const view=Regional.layout(world),partition=world.Definition.OwnershipPlan.Partition;
 assert.equal(view.boxes.length,30);assert.equal(world.Regions.length,6);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),18);
 assert.equal(world.Connections.length,7);assert.ok(world.Connections.every(c=>c.Forward.Status==='Found'&&c.Reverse.Status==='Found'));
 assert.equal(partition.Pattern.PartitionSearch.Seed,seed);
 Schematic.areaPartitionWitness(partition,()=>{throw Error('invalid partition');});
 const changed=structuredClone(partition);changed.PartitionAttempts.find(a=>a.Status==='Selected').Cut++;
 assert.throws(()=>Schematic.areaPartitionWitness(changed,()=>{throw Error('invalid partition');}));
 assert.deepEqual(fs.readFileSync(`${folder}/regional-schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js'));
});
