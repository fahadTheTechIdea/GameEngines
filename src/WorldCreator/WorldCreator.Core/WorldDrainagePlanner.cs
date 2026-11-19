using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public enum WorldDrainageTerminalKind { Outlet, RetainedBasin }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldDrainageTerminal(string Id,int Column,int Row,WorldDrainageTerminalKind Kind,string Purpose,string SourceRuleId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldDrainageSource(string Id,int Column,int Row,double Rate,string Purpose,string SourceRuleId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldDrainageDefinition(int SchemaVersion,string Id,string Purpose,string SourceRuleId,string RateUnit,
    IReadOnlyList<WorldDrainageTerminal> Terminals,IReadOnlyList<WorldDrainageSource> Sources,
    double LossFractionPerEdge=0,int MaximumSamples=16384,long MaximumWork=20000000);
public sealed record WorldDrainageNode(int SampleIndex,int Column,int Row,WorldPoint Position,double Elevation,
    int? DownstreamSampleIndex,string TerminalId,double DistanceToTerminal,double LocalInput,double Incoming,double Lost,double Outgoing,double Retained);
public sealed record WorldDrainageReceipt(string TerminalId,WorldDrainageTerminalKind Kind,double Received);
public sealed record WorldDrainagePlan(int SchemaVersion,WorldSurfaceDefinition Surface,WorldDrainageDefinition Definition,string SurfaceSha256,
    IReadOnlyList<WorldDrainageNode> Nodes,IReadOnlyList<WorldDrainageReceipt> Terminals,int MaskedSamples,int BlockedEdges,
    double TotalInput,double TotalLost,double TotalReceived,double ConservationResidual,long Work);

/// <summary>Optional game drainage on the shared heightfield triangle-edge graph.
/// Shortest non-uphill paths resolve flats without modifying elevation. Sources are
/// explicit rates per sample, not rainfall per unit area. No water width is implied.</summary>
public sealed class WorldDrainagePlanner
{
    public WorldDrainagePlan Plan(WorldSurfaceDefinition surface,WorldDrainageDefinition definition,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        static bool Text(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=2048;
        if(definition is null||definition.SchemaVersion!=1||!SettlementCatalogRegistry.ValidId(definition.Id)||!Text(definition.Purpose)||!Text(definition.SourceRuleId)||
            !SettlementCatalogRegistry.ValidId(definition.RateUnit)||definition.Terminals is not {Count:>0 and <=4096}||definition.Sources is null||definition.Sources.Count>4096||
            !double.IsFinite(definition.LossFractionPerEdge)||definition.LossFractionPerEdge is <0 or >1||definition.MaximumSamples is <4 or >263169||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_DRAINAGE_DEFINITION_INVALID");
        definition=definition with{Terminals=Array.AsReadOnly(definition.Terminals.ToArray()),Sources=Array.AsReadOnly(definition.Sources.ToArray())};
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var terminal in definition.Terminals)
            if(terminal is null||!SettlementCatalogRegistry.ValidId(terminal.Id)||!ids.Add(terminal.Id)||!Enum.IsDefined(terminal.Kind)||!Text(terminal.Purpose)||!Text(terminal.SourceRuleId))
                throw new ArgumentException("WORLD_DRAINAGE_TERMINAL_INVALID");
        foreach(var source in definition.Sources)
            if(source is null||!SettlementCatalogRegistry.ValidId(source.Id)||!ids.Add(source.Id)||!Text(source.Purpose)||!Text(source.SourceRuleId)||!double.IsFinite(source.Rate)||source.Rate is <=0 or >1000000000)
                throw new ArgumentException("WORLD_DRAINAGE_SOURCE_INVALID");
        definition=definition with{Terminals=Array.AsReadOnly(definition.Terminals.OrderBy(t=>t.Id,StringComparer.Ordinal).ToArray()),Sources=Array.AsReadOnly(definition.Sources.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray())};
        long rawSamples=surface?.HeightField is {} raw?(long)raw.Columns*raw.Rows:surface?.Terrain is {} terrain?(long)terrain.Columns*terrain.Rows:0;
        if(rawSamples>definition.MaximumSamples)throw new ArgumentException("WORLD_DRAINAGE_SAMPLE_BUDGET");
        var query=new WorldSurfaceQuery(surface!,token);
        var field=query.HeightField?.CompleteField??throw new ArgumentException("WORLD_DRAINAGE_HEIGHTFIELD_REQUIRED");
        int count=field.Heights.Count;if(count>definition.MaximumSamples)throw new ArgumentException("WORLD_DRAINAGE_SAMPLE_BUDGET");
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,failureCode:"WORLD_DRAINAGE_WORK_BUDGET");
        int vertices=(query.Definition.Boundary?.Vertices.Count??0)+query.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+query.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count);
        var samples=new WorldSurfaceSample[count];var active=new bool[count];int masked=0;
        for(int i=0;i<count;i++)
        {
            budget.Charge(1L+vertices);samples[i]=query.Sample(new(field.Origin.X+i%field.Columns*field.StepX,field.Origin.Y+i/field.Columns*field.StepY),token);
            if(samples[i].Status==WorldSurfaceStatus.Ambiguous)throw new ArgumentException("WORLD_DRAINAGE_AMBIGUOUS_SAMPLE:"+i);
            active[i]=samples[i].Status==WorldSurfaceStatus.Sampled;if(!active[i])masked++;
        }
        if(masked==count)throw new ArgumentException("WORLD_DRAINAGE_DOMAIN_EMPTY");
        int Index(int column,int row)
        {
            if(column<0||column>=field.Columns||row<0||row>=field.Rows||!active[row*field.Columns+column])throw new ArgumentException("WORLD_DRAINAGE_ENDPOINT_INVALID");
            return row*field.Columns+column;
        }
        var terminals=new Dictionary<int,WorldDrainageTerminal>();
        foreach(var t in definition.Terminals)if(!terminals.TryAdd(Index(t.Column,t.Row),t))throw new ArgumentException("WORLD_DRAINAGE_TERMINAL_SAMPLE_DUPLICATE");
        var local=new double[count];foreach(var s in definition.Sources){budget.Charge();local[Index(s.Column,s.Row)]+=s.Rate;}
        var reverse=Enumerable.Range(0,count).Select(_=>new List<(int Index,double Length)>()).ToArray();int blocked=0;
        for(int i=0;i<count;i++)
        {
            budget.Charge();if(!active[i])continue;int x=i%field.Columns,y=i/field.Columns;
            // These are precisely the three undirected edge directions of the
            // existing lower-left/upper-right triangulation, not arbitrary D8 cuts.
            foreach(var (dx,dy) in new[]{(1,0),(0,1),(1,1)})
            {
                if(x+dx>=field.Columns||y+dy>=field.Rows)continue;int j=i+dy*field.Columns+dx;if(!active[j])continue;
                budget.Charge(8L*(vertices+1)*(vertices+1));
                if(!query.ContainsTerrainCorridor(new([samples[i].Position,samples[j].Position]),0,token)){blocked++;continue;}
                bool forward=samples[i].Elevation>=samples[j].Elevation,backward=samples[j].Elevation>=samples[i].Elevation;
                if(query.Definition.Patches.Count>0)
                {
                    budget.Charge(8L*(vertices+1)*(vertices+1)+8L*(field.Columns+field.Rows)*(vertices+1));
                    var spans=query.TraceTerrain(samples[i].Position,samples[j].Position,token);double previous=samples[i].Elevation!.Value;
                    foreach(var span in spans)
                    {
                        budget.Charge();var sample=span.Sample;
                        if(sample.Status!=WorldSurfaceStatus.Sampled){forward=backward=false;break;}
                        double Height(WorldPoint p)=>sample.Elevation!.Value+(p.X-sample.Position.X)*sample.GradientX!.Value+(p.Y-sample.Position.Y)*sample.GradientY!.Value;
                        double start=Height(span.From),end=Height(span.To);
                        forward&=start<=previous&&end<=start;backward&=start>=previous&&end>=start;previous=end;
                    }
                    forward&=samples[j].Elevation<=previous;backward&=samples[j].Elevation>=previous;
                }
                double length=samples[i].Position.DistanceTo(samples[j].Position);
                if(forward)reverse[j].Add((i,length));if(backward)reverse[i].Add((j,length));if(!forward&&!backward)blocked++;
            }
        }
        var distance=Enumerable.Repeat(double.PositiveInfinity,count).ToArray();var downstream=Enumerable.Repeat(-1,count).ToArray();var owner=new string?[count];
        var queue=new PriorityQueue<int,(double Distance,int Index)>();var settled=new List<int>();var visited=new bool[count];
        foreach(var (index,t) in terminals.OrderBy(t=>t.Key)){distance[index]=0;owner[index]=t.Id;queue.Enqueue(index,(0,index));}
        while(queue.TryDequeue(out int i,out var priority))
        {
            budget.Charge();if(visited[i]||priority.Distance!=distance[i])continue;visited[i]=true;settled.Add(i);
            foreach(var edge in reverse[i])
            {
                budget.Charge();int j=edge.Index;if(visited[j])continue;double candidate=distance[i]+edge.Length;
                if(candidate<distance[j]||candidate==distance[j]&&i<downstream[j])
                {distance[j]=candidate;downstream[j]=i;owner[j]=owner[i];queue.Enqueue(j,(candidate,j));}
            }
        }
        int unresolved=Enumerable.Range(0,count).FirstOrDefault(i=>active[i]&&!visited[i],-1);
        if(unresolved>=0)
        {
            var error=new ArgumentException("WORLD_DRAINAGE_UNREACHABLE_SAMPLE:"+unresolved);
            error.Data["UnreachableSamples"]=active.Count(a=>a)-settled.Count;error.Data["UsedWork"]=budget.Used;throw error;
        }
        var incoming=new double[count];var lost=new double[count];var outgoing=new double[count];var retained=new double[count];
        for(int k=settled.Count-1;k>=0;k--)
        {
            budget.Charge();int i=settled[k];double amount=local[i]+incoming[i];
            if(downstream[i]<0)retained[i]=amount;
            else{lost[i]=amount*definition.LossFractionPerEdge;outgoing[i]=amount-lost[i];incoming[downstream[i]]+=outgoing[i];}
        }
        var nodes=new List<WorldDrainageNode>();
        for(int i=0;i<count;i++)if(active[i])
        {budget.Charge();nodes.Add(new(i,i%field.Columns,i/field.Columns,samples[i].Position,samples[i].Elevation!.Value,downstream[i]<0?null:downstream[i],owner[i]!,distance[i],local[i],incoming[i],lost[i],outgoing[i],retained[i]));}
        var receipts=terminals.OrderBy(t=>t.Value.Id,StringComparer.Ordinal).Select(t=>new WorldDrainageReceipt(t.Value.Id,t.Value.Kind,retained[t.Key])).ToArray();
        double input=local.Sum(),loss=lost.Sum(),received=retained.Sum(),residual=input-loss-received;
        if(!double.IsFinite(residual)||Math.Abs(residual)>1e-9*Math.Max(1,input))throw new ArgumentException("WORLD_DRAINAGE_CONSERVATION_FAILED");
        return new(1,query.Definition,definition,WorldGenerationDependencyIdentity.Digest(query.Definition),nodes.AsReadOnly(),Array.AsReadOnly(receipts),masked,blocked,input,loss,received,residual,budget.Used);
    }

    public void Validate(WorldDrainagePlan plan,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(plan);
        var expected=Plan(plan.Surface,plan.Definition,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))throw new ArgumentException("WORLD_DRAINAGE_PLAN_MISMATCH");
    }
}
