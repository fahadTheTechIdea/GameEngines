const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const folder=process.env.WORLD_SDK_FIRST;
if(!folder)throw new Error('WORLD_SDK_FIRST must identify the generated SDK output');
const read=p=>JSON.parse(fs.readFileSync(path.join(folder,p),'utf8'));
test('single source publishes a complete inspectable baseline with no fabricated edits',()=>{
    const receipt=read('regional-source-workflow-results.json'),world=read('baseline/world-regions.json');
    assert.deepEqual(receipt.Edits,[]);
    assert.equal(receipt.InitialWorldSha256,receipt.FinalWorldSha256);
    assert.ok(receipt.BaselinePublishedWorldReplayed&&receipt.CheckpointReplayed);
    assert.equal(world.Regions.length,4);
    assert.equal(world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0),20);
    assert.equal(world.Connections.length,3);
    assert.ok(world.Connections.every(c=>c.Forward.Path&&c.Reverse.Path));
    const R=require(path.resolve('src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js'));
    assert.equal(R.layout(world).boxes.length,20);
    assert.ok(fs.existsSync(path.join(folder,'baseline/generation-session.json')));
});
