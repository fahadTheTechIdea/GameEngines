using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class CityStreetBlockTests
{
    private static readonly RoadCrossSection Profile=new(4,1,0,0,0,0);
    private static StreetCorridor Road(string id,WorldPoint a,WorldPoint b,RoadCrossSection? profile=null)=>new(id,new([a,b]),profile??Profile);
    private static StreetCorridor[] Loop(params WorldPoint[] points)=>points.Select((p,i)=>Road("edge-"+i,p,points[(i+1)%points.Length])).ToArray();
    private static UrbanBlock[] Extract(IReadOnlyList<StreetCorridor> roads)
    {
        var result=UrbanBlockExtractor.Extract(roads);
        Check(result.Diagnostics.Count==0,string.Join(',',result.Diagnostics));return result.Blocks.ToArray();
    }
    public static async Task RunAsync()
    {
        var tRoads=Loop(new(0,0),new(200,0),new(200,160),new(0,160)).Concat(new[]{
            Road("divider",new(100,0),new(100,160)),Road("local",new(100,80),new(200,80))}).ToArray();
        var tee=Extract(tRoads);
        var raw=UrbanBlockExtractor.ExtractStreetFaces(tRoads);
        Check(raw.Diagnostics.Count==0 && JsonSerializer.Serialize(UrbanBlockExtractor.ReserveStreetFaces(raw.Faces).Blocks)==JsonSerializer.Serialize(tee),"raw street inspection and separate inset reproduce existing block IDs/geometry");
        var rawNarrow=UrbanBlockExtractor.ExtractStreetFaces(Loop(new(0,0),new(3,0),new(3,100),new(0,100)));
        Check(rawNarrow.Diagnostics.Count==0 && rawNarrow.Faces.Count==1 && UrbanBlockExtractor.ReserveStreetFaces(rawNarrow.Faces).Diagnostics.Count>0,"raw face survives inspection when road-width reservation collapses its interior");
        Near(ConvexParcelGeometry.MinimumWidth(new([new(0,0),new(100,0),new(100,80),new(0,80)])),80,"polygon minimum supporting width");
        Near(ConvexParcelGeometry.MinimumSideLength(new([new(0,0),new(100,0),new(100,1),new(100,80),new(0,80)])),80,"collinear topology terminals do not shorten a geometric polygon side");
        Check(tee.Length==3,"T-junction creates three actual faces, including the unsplit left block");
        Check(tee.Select(b=>Math.Round(b.AreaSquareMeters,6)).Order().SequenceEqual(new[]{7296d,7296d,14976d}),"independent T-junction inset areas");
        string json=JsonSerializer.Serialize(tee);
        Check(json==JsonSerializer.Serialize(Extract(tRoads.Reverse().ToArray())),"face geometry, edge owners and IDs independent of corridor input order");
        var closeTerminal=tRoads.Select(r=>r.Id=="divider"?r with{Centerline=new([new(100,0),new(100,1),new(100,160)])}:r).ToArray();
        Check(JsonSerializer.Serialize(Extract(closeTerminal))==json,"a collinear terminal inside the road-corner inset does not fold or change block geometry");
        var triangle=Extract(Loop(new(0,0),new(120,0),new(0,120))).Single();
        Near(triangle.AreaSquareMeters,Math.Pow(116-2*Math.Sqrt(2),2)/2,"triangle reservation retains diagonal boundary");
        Check(triangle.Boundary.Count==3 && triangle.Frontages!.Any(f=>f.RoadStart.X!=f.RoadEnd.X&&f.RoadStart.Y!=f.RoadEnd.Y),"diagonal road ownership retained");
        var diamond=Extract(Loop(new(100,0),new(200,100),new(100,200),new(0,100))).Single();
        Near(diamond.AreaSquareMeters,Math.Pow(Math.Sqrt(20000)-4,2),"rotated-square area from perpendicular street reservations");
        var concave=Extract(Loop(new(0,0),new(120,0),new(120,40),new(40,40),new(40,120),new(0,120))).Single();
        Near(concave.AreaSquareMeters,7056,"concave L block area instead of bounding rectangle");
        Check(!new WorldPolygon(concave.Boundary).ContainsPoint(new(90,90)),"concave void remains outside block");
        var asymmetric=new RoadCrossSection(4,1,3,1,0,0);
        var asymRoads=Loop(new(0,0),new(100,0),new(100,80),new(0,80)).Select(r=>r with{Profile=asymmetric}).ToArray();
        Near(Extract(asymRoads).Single().AreaSquareMeters,6300,"left-side street width, not half of total");
        asymRoads[0]=asymRoads[0] with{Centerline=new(asymRoads[0].Centerline.Points.Reverse().ToArray())};
        var reversed=Extract(asymRoads).Single();
        Near(reversed.AreaSquareMeters,6480,"opposite road direction uses actual right-side width");
        Check(reversed.Frontages!.Single(f=>f.RoadId=="edge-0").OffsetMeters==3,"asymmetric road-side provenance");
        var varying=Loop(new(0,0),new(100,0),new(100,80),new(0,80));
        varying[0]=varying[0] with{Profile=new(12,1,0,0,0,0)};
        Near(Extract(varying).Single().AreaSquareMeters,96*72,"each adjacent road retains its own width");
        var crossing=Loop(new(0,0),new(120,0),new(120,120),new(0,120)).Concat(new[]{
            Road("diagonal-a",new(0,0),new(120,120)),Road("diagonal-b",new(120,0),new(0,120))}).ToArray();
        Check(Extract(crossing).Length==4,"actual crossing is split into four bounded triangular faces");
        var angled=Loop(new(0,0),new(190,20),new(220,180),new(15,200)).Append(Road("crosscut",new(95,10),new(117.5,190))).ToArray();
        Check(JsonSerializer.Serialize(Extract(angled))==JsonSerializer.Serialize(Extract(angled.Reverse().ToArray())),"nonintegral diagonal intersections are input-order invariant");
        var outsideSpur=Loop(new(0,0),new(120,0),new(120,120),new(0,120)).Append(Road("outside",new(60,0),new(60,-20))).ToArray();
        Near(Extract(outsideSpur).Single().AreaSquareMeters,116*116,"exterior approach does not erase its adjacent block");
        var insideSpur=outsideSpur[..^1].Append(Road("inside",new(60,0),new(60,40))).ToArray();
        var nonSimple=UrbanBlockExtractor.Extract(insideSpur);
        Check(nonSimple.Blocks.Count==0 && nonSimple.Diagnostics.Any(d=>d.StartsWith("BLOCK_FACE_INVALID",StringComparison.Ordinal)),"interior spur is diagnosed instead of ignored or filled");
        var collapsed=UrbanBlockExtractor.Extract(Loop(new(0,0),new(10,0),new(10,10),new(0,10)).Select(r=>r with{Profile=new(12,1,0,0,0,0)}).ToArray());
        Check(collapsed.Blocks.Count==0 && collapsed.Diagnostics.Count>0,"collapsed road reservations never become inverted buildable polygons");
        var tooMany=UrbanBlockExtractor.Extract(tRoads,2);
        Check(tooMany.Blocks.Count==0 && tooMany.Diagnostics.Contains("BLOCK_BUDGET_EXCEEDED"),"atomic face budget refusal");
        Check(UrbanBlockExtractor.Extract([Road("open",new(0,0),new(120,0))]).Blocks.Count==0,"open street does not manufacture a bounding box block");
        var overlap=UrbanBlockExtractor.Extract(tRoads.Append(tRoads[0] with{Id="overlap"}).ToArray());
        Check(overlap.Blocks.Count==0 && overlap.Diagnostics.Any(d=>d.StartsWith("COLLINEAR_OVERLAP",StringComparison.Ordinal)),"overlap cannot silently alter face ownership");
        var outer=Loop(new(0,0),new(200,0),new(200,200),new(0,200));
        var inner=Loop(new(60,60),new(140,60),new(140,140),new(60,140)).Select(r=>r with{Id="inner-"+r.Id});
        var hole=UrbanBlockExtractor.Extract(outer.Concat(inner).Append(Road("bridge",new(100,0),new(100,60))).ToArray());
        Check(hole.Blocks.Count==0 && hole.Diagnostics.Count>0,"multiply connected face cannot be silently filled or partially published");
        Check(UrbanBlockExtractor.Extract(Enumerable.Repeat(tRoads[0],4097).ToArray()).Diagnostics.Contains("BLOCK_CORRIDOR_BUDGET_EXCEEDED"),"corridor budget precedes quadratic topology work");
        try{UrbanBlockExtractor.Extract([Road("nonfinite",new(double.NaN,0),new(100,0))]);throw new Exception("Missing nonfinite refusal");}catch(ArgumentException){}
        using(var cancelled=new CancellationTokenSource())
        {
            cancelled.Cancel();try{UrbanBlockExtractor.Extract(tRoads,token:cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        }
        await Task.CompletedTask;
        Console.WriteLine("PASS: street graph polygon faces, T-junctions, diagonals, concavity, asymmetric widths, crossings, determinism, explicit invalid topology/offset refusal, budgets/cancellation and independent geometric checks. Retired city integration is not exercised.");
    }
    public static async Task WriteFixtureAsync(string directory)
    {
        // Diagnostic only: these intentionally authored street loops test extraction, not city composition.
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Fixture output exists");
        var roads=Loop(new(20,20),new(260,40),new(220,140),new(120,130),new(80,240),new(20,220));
        var blocks=Extract(roads);var nodes=new List<WorldMetadataNode>();var boxes=new List<MetadataMapBox>();
        nodes.Add(new("diagnostic","diagnostic","world",null,"Street-face geometry diagnostic; not a generated city",new Dictionary<string,JsonElement>(),[150,140],null));
        boxes.Add(new("diagnostic",0,0,300,280));
        foreach(var block in blocks)
        {
            double x=block.Boundary.Min(p=>p.X),y=block.Boundary.Min(p=>p.Y),right=block.Boundary.Max(p=>p.X),bottom=block.Boundary.Max(p=>p.Y);
            var triangle=LandscapePolygonTriangulator.Triangulate(new(block.Boundary)).OrderByDescending(p=>Math.Abs(p.SignedArea())).First();
            var anchor=new WorldPoint(triangle.Vertices.Average(p=>p.X),triangle.Vertices.Average(p=>p.Y));
            Check(new WorldPolygon(block.Boundary).ContainsPoint(anchor),"diagnostic label/selection anchor is inside its concave polygon");
            nodes.Add(new(block.Id,"block","block","diagnostic","Extracted concave street face; exact C# polygon",JsonSerializer.Deserialize<Dictionary<string,JsonElement>>("{\"schematic\":{\"color\":\"#d8e6d8\",\"footprint\":false}}")!,[anchor.X,anchor.Y],null));
            boxes.Add(new(block.Id,x,y,right-x,bottom-y));
        }
        var metadata=new WorldMetadataDocument(2,"street-face-diagnostic","diagnostic","block",MetadataCoverage.PartialScope,["City design and procedural street growth are not part of this geometry fixture"],new(["x","y"],"meters"),"fixture","fixture",nodes,[],new MetadataLayerPlanner().Plan([],nodes,[]),new Dictionary<string,MetadataStructureDefinition>(),
            new("C# UrbanBlockExtractor geometry diagnostic","meters",300,280,boxes,roads.Select(r=>new MetadataMapRoad(r.Id,r.Centerline.Points[0].X,r.Centerline.Points[0].Y,r.Centerline.Points[1].X,r.Centerline.Points[1].Y,r.Profile.TotalWidthMeters)).ToArray(),blocks.Select(b=>new MetadataMapPolygon(b.Id,b.Boundary)).ToArray()));
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"world-metadata.json"),JsonSerializer.Serialize(metadata,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(directory,"blocks.json"),JsonSerializer.Serialize(blocks,WorldJson.CreateOptions(true)));
        await new WorldMetadataSchematicExporter().WriteAsync(metadata,directory);
        Console.WriteLine("Wrote polygon extraction diagnostic; not an accepted city or art deliverable.");
    }
    private static void Near(double actual,double expected,string message)=>Check(Math.Abs(actual-expected)<1e-6,message+": "+actual+" != "+expected);
    private static void Check(bool valid,string message){if(!valid)throw new Exception("Street block: "+message);}
}
