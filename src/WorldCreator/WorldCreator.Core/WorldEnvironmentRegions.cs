using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>One owner for piecewise-affine environmental fields over a planar terrain face.</summary>
internal static class WorldEnvironmentRegions
{
    internal readonly record struct Affine(double Constant,double X,double Y);
    internal sealed record Piece(IReadOnlyList<WorldPoint> Vertices,Dictionary<string,Affine> Fields);
    internal static IReadOnlyList<WorldEnvironmentalField>? CoordinateFields(WorldEnvironmentQuery environment,string fieldId,WorldSpatialWorkBudget budget)
    {
        var needed=new HashSet<string>(StringComparer.Ordinal){fieldId};
        foreach(var field in environment.OrderedFields.Reverse())
        {
            budget.Charge();if(!needed.Contains(field.Id))continue;
            foreach(var term in field.Terms){budget.Charge();if(term.Input==WorldFieldInput.Field)needed.Add(term.FieldId!);}
        }
        var selected=environment.OrderedFields.Where(f=>needed.Contains(f.Id)).ToArray();
        return selected.Length>0&&selected.All(f=>f.Terms.All(t=>t.Input is WorldFieldInput.X or WorldFieldInput.Y or WorldFieldInput.Field))?selected:null;
    }
    internal static double Value(Affine value,WorldPoint origin,WorldPoint point)=>value.Constant+value.X*(point.X-origin.X)+value.Y*(point.Y-origin.Y);
    internal static IReadOnlyList<Piece> Evaluate(WorldEnvironmentQuery environment,WorldSurfaceSample sample,
        IReadOnlyList<WorldPoint> vertices,int maximumPiecesPerRegion,WorldSpatialWorkBudget budget,
        IReadOnlyList<WorldEnvironmentalField>? selectedFields=null)
    {
        var elevation=new Affine(sample.Elevation!.Value,sample.GradientX!.Value,sample.GradientY!.Value);
        Affine Scalar(string id)
        {
            budget.Charge();var value=environment.SampleScalar(id,sample.Position);
            return new(value.Value,value.GradientX,value.GradientY);
        }
        var active=new List<Piece>{new(vertices,new Dictionary<string,Affine>(StringComparer.Ordinal))};
        double Value(Affine value,WorldPoint point)=>value.Constant+value.X*(point.X-sample.Position.X)+value.Y*(point.Y-sample.Position.Y);
        (double Min,double Max) Bounds(Affine value,IReadOnlyList<WorldPoint> polygon,double limit)
        {
            double min=double.PositiveInfinity,max=double.NegativeInfinity;
            foreach(var p in polygon)
            {
                budget.Charge();double v=Value(value,p);
                if(!double.IsFinite(v)||Math.Abs(v)>limit)throw new ArgumentException("WORLD_ENVIRONMENT_VALUE_RANGE");
                min=Math.Min(min,v);max=Math.Max(max,v);
            }
            return(min,max);
        }
        IReadOnlyList<WorldPoint> Clip(IReadOnlyList<WorldPoint> polygon,Affine value,double threshold,bool lower)
        {
            var output=new List<WorldPoint>();
            for(int i=0;i<polygon.Count;i++)
            {
                budget.Charge();var a=polygon[i];var b=polygon[(i+1)%polygon.Count];
                double av=Value(value,a)-threshold,bv=Value(value,b)-threshold;
                bool ai=lower?av<=0:av>=0,bi=lower?bv<=0:bv>=0;
                if(ai)output.Add(a);
                if(ai!=bi)
                {
                    double t=av/(av-bv);
                    output.Add(new(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t));
                }
            }
            return Array.AsReadOnly(output.Distinct().ToArray());
        }
        foreach(var field in selectedFields??environment.OrderedFields)
        {
            var next=new List<Piece>();
            void Add(Piece piece,Affine value)
            {
                if(piece.Vertices.Count==0)return;
                if(next.Count>=maximumPiecesPerRegion)throw new ArgumentException("WORLD_FOOTPRINT_PIECE_BUDGET");
                Bounds(value,piece.Vertices,1e9);
                budget.Charge(piece.Fields.Count+1);
                var values=new Dictionary<string,Affine>(piece.Fields,StringComparer.Ordinal){[field.Id]=value};
                next.Add(new(piece.Vertices,values));
            }
            void Upper(Piece piece,Affine value)
            {
                var range=Bounds(value,piece.Vertices,1e100);
                if(field.Maximum is not {} max||range.Max<=max){Add(piece,value);return;}
                if(range.Min>=max){Add(piece,new(max,0,0));return;}
                Add(piece with{Vertices=Clip(piece.Vertices,value,max,true)},value);
                Add(piece with{Vertices=Clip(piece.Vertices,value,max,false)},new(max,0,0));
            }
            foreach(var piece in active)
            {
                Affine value=new(field.Offset,0,0);
                foreach(var term in field.Terms)
                {
                    budget.Charge();
                    var input=term.Input switch
                    {
                        WorldFieldInput.Elevation=>elevation,WorldFieldInput.Slope=>new(sample.Slope!.Value,0,0),
                        WorldFieldInput.X=>new(sample.Position.X,1,0),WorldFieldInput.Y=>new(sample.Position.Y,0,1),
                        WorldFieldInput.Field=>piece.Fields[term.FieldId!],WorldFieldInput.SampledField=>Scalar(term.FieldId!),
                        _=>throw new ArgumentException("WORLD_ENVIRONMENT_INPUT_INVALID")
                    };
                    value=new(value.Constant+term.Coefficient*input.Constant,value.X+term.Coefficient*input.X,value.Y+term.Coefficient*input.Y);
                    Bounds(value,piece.Vertices,1e100);
                }
                var range=Bounds(value,piece.Vertices,1e100);
                if(field.Minimum is not {} min||range.Min>=min){Upper(piece,value);continue;}
                if(range.Max<=min){Upper(piece,new(min,0,0));continue;}
                Upper(piece with{Vertices=Clip(piece.Vertices,value,min,true)},new(min,0,0));
                Upper(piece with{Vertices=Clip(piece.Vertices,value,min,false)},value);
            }
            active=next;
        }
        return active;
    }
}
