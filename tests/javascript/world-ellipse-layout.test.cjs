const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const layout=process.env.WORLD_ELLIPSE_LAYOUT,facing=process.env.WORLD_ELLIPSE_FACING,adapt=process.env.WORLD_ELLIPSE_ADAPT;
if(!layout||!facing||!adapt)throw new Error('Provide all three generated ellipse fixture roots');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
test('unfaced, tangent-facing and terrain-adapted ellipses retain complete physical worlds',()=>{
    for(const p of [path.join(layout,'ellipse/world-metadata.json'),path.join(facing,'Ellipse/world-metadata.json'),path.join(adapt,'ellipse/world-metadata.json')]){
        const world=read(p);assert.equal(S.layout(world).boxes.length,5);assert.equal(world.AccessPlan.Sites.length,4);
        assert.ok(world.RoutePlan.Routes[0].Forward.Path&&world.RoutePlan.Routes[0].Reverse.Path);
        const altered=structuredClone(world);altered.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral.AxisRatio=.8;
        assert.throws(()=>S.layout(altered),/spiral population|population adaptation|population orientation/);
    }
});
