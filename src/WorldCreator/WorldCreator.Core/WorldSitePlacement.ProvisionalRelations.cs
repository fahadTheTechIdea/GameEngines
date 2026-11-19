using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldSitePlacementPlanner
{
    /// <summary>Branch-local candidates; only fixed geometry qualification is cached.</summary>
    private sealed class ProvisionalRelationGuide
    {
        private readonly WorldPlacementRequest request;
        private readonly IReadOnlyDictionary<string,WorldPoint> anchors;
        private readonly IReadOnlyDictionary<WorldPoint,string> features;
        private readonly IReadOnlyDictionary<(WorldPoint From,WorldPoint To),string> edges;
        private readonly IReadOnlyDictionary<WorldPoint,string> baseCandidates;
        private readonly WorldSpatialWorkBudget budget;
        private readonly HashSet<string> demandIds;
        private readonly Dictionary<string,List<(WorldPlacementRelation Rule,string Other)>> links;
        private readonly HashSet<WorldPoint> seen;
        private readonly Dictionary<(string DemandId,WorldPoint Position),WorldSitePlacement?> geometry=[];

        public ProvisionalRelationGuide(WorldPlacementRequest request,IReadOnlyDictionary<string,WorldPoint> anchors,
            IReadOnlyDictionary<WorldPoint,string> features,IReadOnlyDictionary<(WorldPoint From,WorldPoint To),string> edges,
            IReadOnlyDictionary<WorldPoint,string> baseCandidates,WorldSpatialWorkBudget budget)
        {
            this.request=request;this.anchors=anchors;this.features=features;this.edges=edges;this.baseCandidates=baseCandidates;this.budget=budget;
            demandIds=request.Demands.Select(d=>d.Id).ToHashSet(StringComparer.Ordinal);
            links=demandIds.ToDictionary(id=>id,_=>new List<(WorldPlacementRelation,string)>(),StringComparer.Ordinal);
            budget.Charge(baseCandidates.Count+request.Relations.Count);seen=baseCandidates.Keys.ToHashSet();
            foreach(var rule in request.Relations)
            {
                links[rule.FromId].Add((rule,rule.ToId));
                if(demandIds.Contains(rule.ToId))links[rule.ToId].Add((rule,rule.FromId));
            }
        }

        private bool HasProvisionalNeighbor(string id,IReadOnlyDictionary<string,WorldPoint> known)
        {
            budget.Charge(links[id].Count);
            return links[id].Any(l=>demandIds.Contains(l.Other)&&known.ContainsKey(l.Other));
        }

        public (string Id,IReadOnlyList<WorldSitePlacement> Choices) Next(IReadOnlyDictionary<string,WorldSitePlacement> selected,
            IReadOnlyDictionary<string,List<WorldSitePlacement>> options,IReadOnlyDictionary<string,WorldPoint> known,
            Func<WorldSiteDemand,IReadOnlyDictionary<WorldPoint,string>,List<WorldSitePlacement>> evaluate,
            Func<WorldSitePlacement,IReadOnlyDictionary<string,WorldPoint>,bool> compatible)
        {
            var pending=request.Demands.Where(d=>!selected.ContainsKey(d.Id)).ToArray();
            foreach(var d in pending)budget.Charge(links[d.Id].Count+1);
            bool Pinned(string id)=>links[id].Any(l=>anchors.ContainsKey(l.Other)&&l.Rule.Minimum==0&&l.Rule.Maximum==0);
            // A zero-size current domain may depend on a source not yet assigned.
            // Do not declare that domain impossible before the source can supply it.
            var pinned=pending.Where(d=>Pinned(d.Id)).OrderBy(d=>options[d.Id].Count).ThenBy(d=>d.Id,StringComparer.Ordinal).FirstOrDefault();
            if(pinned is not null)return(pinned.Id,options[pinned.Id].Where(s=>compatible(s,known)).ToArray());
            var viable=new List<(string Id,IReadOnlyList<WorldSitePlacement> Choices,int Known)>();
            foreach(var demand in pending)
            {
                var choices=Candidates(demand,known,options[demand.Id],evaluate).Where(s=>compatible(s,known)).ToArray();
                bool unresolved=links[demand.Id].Any(l=>demandIds.Contains(l.Other)&&!known.ContainsKey(l.Other));
                if(choices.Length==0)
                {
                    if(!unresolved)return(demand.Id,choices);
                    continue; // A later source can enrich this otherwise empty domain.
                }
                viable.Add((demand.Id,choices,links[demand.Id].Count(l=>known.ContainsKey(l.Other))));
            }
            if(viable.Count==0)return(pending[0].Id,Array.Empty<WorldSitePlacement>());
            var next=viable.OrderBy(v=>v.Choices.Count).ThenByDescending(v=>v.Known).ThenBy(v=>v.Id,StringComparer.Ordinal).First();
            return(next.Id,next.Choices);
        }

        public IReadOnlyList<WorldSitePlacement> Candidates(WorldSiteDemand demand,IReadOnlyDictionary<string,WorldPoint> known,
            IReadOnlyList<WorldSitePlacement> initial,Func<WorldSiteDemand,IReadOnlyDictionary<WorldPoint,string>,List<WorldSitePlacement>> evaluate)
        {
            if(!HasProvisionalNeighbor(demand.Id,known))return initial;
            var input=new Dictionary<WorldPoint,string>();
            budget.Charge(anchors.Count+links[demand.Id].Count);
            var sources=new Dictionary<string,WorldPoint>(anchors,StringComparer.Ordinal);
            foreach(var link in links[demand.Id])if(known.TryGetValue(link.Other,out var position))sources[link.Other]=position;
            RelationCandidates(request,sources,features,edges,budget,(point,source)=>
            {
                budget.Charge();
                if(seen.Add(point)&&seen.Count>request.MaximumCandidates)throw new ArgumentException("WORLD_PLACEMENT_CANDIDATE_BUDGET");
                if(!baseCandidates.ContainsKey(point))input.TryAdd(point,source+":provisional");
            },demand.Id);
            if(input.Count==0)return initial;
            var missing=new Dictionary<WorldPoint,string>();
            foreach(var pair in input){budget.Charge();if(!geometry.ContainsKey((demand.Id,pair.Key)))missing.Add(pair.Key,pair.Value);}
            if(missing.Count>0)
            {
                var qualified=evaluate(demand,missing).ToDictionary(s=>s.Position);
                foreach(var pair in missing){budget.Charge();geometry.Add((demand.Id,pair.Key),qualified.GetValueOrDefault(pair.Key));}
            }
            var choices=new List<WorldSitePlacement>(initial);
            foreach(var pair in input)
            {
                budget.Charge();
                if(geometry[(demand.Id,pair.Key)] is {} site)choices.Add(site with{CandidateSource=pair.Value});
            }
            // Cached positions from other branches are never enumerated here.
            // Relationships and separation are evaluated against this frame's poses.
            return choices;
        }
    }
}
