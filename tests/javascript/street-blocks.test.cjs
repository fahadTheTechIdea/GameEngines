const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const WorldSchematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.CITY_BLOCK_FIXTURE||'generated/procedural-city/polygon-foundation/diagnostic-current';
const load=()=>JSON.parse(fs.readFileSync(root+'/world-metadata.json'));

test('real C# concave street face survives rendering as exact polygon geometry',()=>{
    const world=load(),blocks=JSON.parse(fs.readFileSync(root+'/blocks.json'));
    const before=JSON.stringify(world),plan=WorldSchematic.layout(world);
    assert.equal(plan.coordinateSpace,'engine-world-meters');
    for(const block of blocks){
        const shape=plan.boxes.find(b=>b.id===block.Id);
        assert.deepEqual(shape.polygon,block.Boundary.map(p=>({x:p.X,y:p.Y})));
        assert.ok(shape.polygon.length>4);
        const area=Math.abs(shape.polygon.reduce((sum,p,i)=>{const q=shape.polygon[(i+1)%shape.polygon.length];return sum+p.x*q.y-q.x*p.y;},0)/2);
        assert.ok(Math.abs(area-block.AreaSquareMeters)<1e-6);
        assert.ok(area<shape.width*shape.height,'renderer preserves concavity instead of drawing bounding box');
    }
    assert.equal(JSON.stringify(world),before);
    assert.equal(fs.readFileSync(root+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
    assert.equal(world.Coverage,'PartialScope');
});

test('polygon corruption rejects instead of silently reverting to rectangular packing',()=>{
    for(const mutate of [
        w=>w.MapGeometry.Polygons.push(w.MapGeometry.Polygons[0]),
        w=>{w.MapGeometry.Polygons[0].NodeId='missing';},
        w=>{w.MapGeometry.Polygons[0].Vertices[0].X=NaN;},
        w=>{w.MapGeometry.Polygons[0].Vertices[0].X=-1;},
        w=>{w.MapGeometry.Polygons[0].Vertices=w.MapGeometry.Polygons[0].Vertices.slice(0,2);},
        w=>{w.MapGeometry.Polygons[0].Vertices[1]=w.MapGeometry.Polygons[0].Vertices[0];},
        w=>{w.MapGeometry.Polygons[0].Vertices=Array(1025).fill({X:50,Y:50});},
        w=>{w.MapGeometry.Polygons[0].Vertices=[{X:30,Y:30},{X:200,Y:150},{X:30,Y:150},{X:180,Y:30}];}
    ]){const bad=load();mutate(bad);assert.throws(()=>WorldSchematic.layout(bad));}
});

test('rectangle-only metadata remains explicitly rectangle-only',()=>{
    const world=load();delete world.MapGeometry.Polygons;
    const plan=WorldSchematic.layout(world);
    assert.ok(plan.boxes.every(b=>b.polygon===undefined));
    assert.equal(plan.coordinateSpace,'engine-world-meters');
});

test('ordinary CLI city package carries every street-extracted polygon through the same viewer',()=>{
    const folder=process.env.CITY_BLOCK_RECT_PACKET||'generated/procedural-city/polygon-foundation/rectangular-regression';
    const world=JSON.parse(fs.readFileSync(folder+'/world-metadata.json'));
    const city=JSON.parse(fs.readFileSync(folder+'/city.json'));
    const plan=WorldSchematic.layout(world),byId=new Map(plan.boxes.map(b=>[b.id,b]));
    assert.equal(world.MapGeometry.Polygons.length,city.Blocks.length);
    for(const block of city.Blocks){
        const shape=byId.get(`${city.Id}/block/${block.Id}`);
        assert.deepEqual(shape.polygon,block.Geometry.Boundary.map(p=>({x:p.X,y:p.Y})));
        assert.ok(block.Geometry.Frontages.every(f=>city.Streets.some(r=>r.Id===f.RoadId)));
    }
});
