using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSurfaceTraversal
{
    private IReadOnlyList<WorldPolygon>? sampledHazards;
    private bool? sampledOverlaysSafe;
    // A sufficient proof, never an approximate acceptance. Complex overlays and
    // envelopes near a bad cell retain the existing exact arrangement owner.
    internal bool AllowsEnvelope(WorldPolygon envelope,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(surface.HeightField is not {} query||
            surface.Definition.Boundary is not {} domain)
            return areas.Matches(envelope,Allows,budget,token);
        if(sampledOverlaysSafe is null)
        {
            // Every possible patch owner must independently pass. Distinct
            // priorities exclude SampleTerrain's same-priority ambiguity even
            // at shared boundaries. Complex overlays use the exact sweep.
            var priorities=new HashSet<int>();bool safe=true;
            foreach(var patch in surface.Definition.Patches)
            {
                token.ThrowIfCancellationRequested();budget.Charge();
                if(!priorities.Add(patch.Priority)||!Profile.KindCosts.ContainsKey(patch.Kind)||
                    Math.Sqrt(patch.GradientX*patch.GradientX+patch.GradientY*patch.GradientY)>Profile.MaximumSlope)
                {safe=false;break;}
            }
            sampledOverlaysSafe=safe;
        }
        if(sampledOverlaysSafe!=true)return areas.Matches(envelope,Allows,budget,token);
        var f=query.Geometry;
        if(domain.Vertices.Any(p=>p.X<f.Origin.X||p.Y<f.Origin.Y||p.X>query.MaxX||p.Y>query.MaxY))
            return areas.Matches(envelope,Allows,budget,token);
        budget.Charge(4L*domain.Vertices.Count*envelope.Vertices.Count);
        if(!domain.ContainsPolygon(envelope))return false;
        // A passable overlay may cover an otherwise forbidden base, so only the
        // exact owner can decide that case.
        if(!Profile.KindCosts.ContainsKey(f.Kind))return areas.Matches(envelope,Allows,budget,token);
        foreach(var exclusion in surface.Definition.Exclusions)
        {
            token.ThrowIfCancellationRequested();var shape=exclusion.Boundary;
            budget.Charge(4L*shape.Vertices.Count*envelope.Vertices.Count);
            if(shape.Vertices.Any(envelope.ContainsPoint)||envelope.Vertices.Any(shape.ContainsPoint))return false;
            for(int i=0;i<shape.Vertices.Count;i++)for(int j=0;j<envelope.Vertices.Count;j++)
                if(PlanarIntersections.Intersect(shape.Vertices[i],shape.Vertices[(i+1)%shape.Vertices.Count],
                    envelope.Vertices[j],envelope.Vertices[(j+1)%envelope.Vertices.Count]).Kind!=IntersectionKind.None)return false;
        }
        if(sampledHazards is null)
        {
            var hazards=new List<WorldPolygon>();
            var window=surface.TerrainDomainWindow!;
            for(int row=window.FirstRow;row<=window.LastRow;row++)for(int col=window.FirstColumn;col<=window.LastColumn;col++)
            {
                token.ThrowIfCancellationRequested();budget.Charge(8);
                var lower=query.SampleCell(col,row,1,0);
                var upper=query.SampleCell(col,row,0,1);
                bool Allowed(WorldScalarSample s)=>Math.Sqrt(s.GradientX*s.GradientX+s.GradientY*s.GradientY)<=Profile.MaximumSlope;
                var a=new WorldPoint(f.Origin.X+col*f.StepX,f.Origin.Y+row*f.StepY);
                var b=new WorldPoint(f.Origin.X+(col+1)*f.StepX,a.Y);
                var c=new WorldPoint(a.X,f.Origin.Y+(row+1)*f.StepY);var d=new WorldPoint(b.X,c.Y);
                if(!Allowed(lower))hazards.Add(new([a,b,d]));
                if(!Allowed(upper))hazards.Add(new([a,d,c]));
            }
            sampledHazards=hazards.AsReadOnly();
        }
        double minX=envelope.Vertices.Min(p=>p.X),minY=envelope.Vertices.Min(p=>p.Y),
            maxX=envelope.Vertices.Max(p=>p.X),maxY=envelope.Vertices.Max(p=>p.Y);
        foreach(var hazard in sampledHazards)
        {
            token.ThrowIfCancellationRequested();budget.Charge(12);
            if(hazard.Vertices.Min(p=>p.X)>maxX+1e-8||hazard.Vertices.Max(p=>p.X)<minX-1e-8||
                hazard.Vertices.Min(p=>p.Y)>maxY+1e-8||hazard.Vertices.Max(p=>p.Y)<minY-1e-8)continue;
            double Cross(WorldPoint a,WorldPoint b,WorldPoint p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
            bool Separated(WorldPolygon a,WorldPolygon b)
            {
                for(int i=0;i<a.Vertices.Count;i++)
                {
                    budget.Charge(b.Vertices.Count);var u=a.Vertices[i];var v=a.Vertices[(i+1)%a.Vertices.Count];
                    double margin=1e-8*Math.Max(1,u.DistanceTo(v));
                    if(b.Vertices.All(p=>Cross(u,v,p)<-margin))return true;
                }
                return false;
            }
            if(Separated(hazard,envelope)||Separated(envelope,hazard))continue;
            IReadOnlyList<WorldPoint> clipped=envelope.Vertices;
            for(int i=0;i<3;i++)
            {var a=hazard.Vertices[i];var b=hazard.Vertices[(i+1)%3];clipped=WorldRegionGeometry.ClipForMeasure(clipped,p=>Cross(a,b,p),budget);}
            if(clipped.Count>=3)
            {
                var witness=new WorldPoint(clipped.Average(p=>p.X),clipped.Average(p=>p.Y));
                bool StrictlyInside(WorldPolygon polygon)
                {
                    for(int i=0;i<polygon.Vertices.Count;i++)
                    {
                        budget.Charge();var a=polygon.Vertices[i];var b=polygon.Vertices[(i+1)%polygon.Vertices.Count];
                        if(Cross(a,b,witness)<=1e-8*Math.Max(1,a.DistanceTo(b)))return false;
                    }
                    return true;
                }
                // A real interior point in an impassable triangle proves rejection.
                // Tangency, tiny intersections and boundary ownership stay exact.
                budget.Charge();
                if(StrictlyInside(hazard)&&StrictlyInside(envelope)&&!Allows(surface.SampleTerrain(witness,token)))return false;
            }
            return areas.Matches(envelope,Allows,budget,token);
        }
        token.ThrowIfCancellationRequested();return true;
    }
}
