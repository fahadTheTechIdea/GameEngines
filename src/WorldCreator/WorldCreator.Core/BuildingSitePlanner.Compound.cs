using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Planning;

/// <summary>Functional ground area, with metadata provenance; does not imply artwork or a physical equipment assembly.</summary>
public sealed record PlannedSiteArea(string Id,string OwnerId,string Kind,WorldPolygon Boundary,string RuleId,
    string Purpose,SiteBand? Band,string? SpeciesAssetId=null,double PlantSpacingMeters=0,double PlantMarginMeters=0,
    bool AllowsThroughAccess=true,WorldPolygon? ReservedSite=null,string? SourceKey=null,WorldPolyline? Access=null,double AccessWidthMeters=0,string? FrontageRoadId=null,
    IReadOnlyList<BuildingComponentRule>? Components=null);
public sealed record CompoundSitePlan(WorldPolygon? MemberRegion,IReadOnlyList<PlannedSiteArea> Areas,IReadOnlyList<string> Issues);

public sealed partial class BuildingSitePlanner
{
    public static bool PermitsThroughAccess(SiteFeatureKind kind)=>kind is not(SiteFeatureKind.Garden or SiteFeatureKind.GreenBuffer or SiteFeatureKind.Lawn);
    public static void ValidateRules(IReadOnlyList<BuildingSiteRule>? siteRules)
    {
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var rule in siteRules??[])
            if(rule is null||string.IsNullOrWhiteSpace(rule.Id)||!ids.Add(rule.Id)||
                !Enum.IsDefined(rule.Kind)||!Enum.IsDefined(rule.Band)||rule.Kind==SiteFeatureKind.AccessWalkway||
                !double.IsFinite(rule.MinimumDepthMeters)||rule.MinimumDepthMeters<=0||!double.IsFinite(rule.InsetMeters)||rule.InsetMeters<0||
                !double.IsFinite(rule.PlantSpacingMeters)||!double.IsFinite(rule.PlantMarginMeters)||rule.PlantMarginMeters<0||
                rule.SpeciesAssetId is not null&&(string.IsNullOrWhiteSpace(rule.SpeciesAssetId)||rule.PlantSpacingMeters<=0)||
                rule.MaximumDepthMeters is {} maximum&&(!double.IsFinite(maximum)||maximum<rule.MinimumDepthMeters))
                throw new ArgumentException("Invalid building site grammar.");
    }

    /// <summary>Reserves fixed authored-depth bands around a compound's member region, in its local meter frame.</summary>
    public CompoundSitePlan PlanCompound(string ownerId,WorldPolygon site,WorldPoint entrance,IReadOnlyList<BuildingSiteRule> rules,
        double accessWidthMeters=2,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(site);ArgumentNullException.ThrowIfNull(rules);
        ValidateRules(rules);site.Validate();
        if(string.IsNullOrWhiteSpace(ownerId)||rules.Count>64||rules.Any(r=>r.Id.Length>128)||!double.IsFinite(accessWidthMeters)||accessWidthMeters<=0)
            throw new ArgumentException("COMPOUND_SITE_INPUT_INVALID");
        if(site.Vertices.Count!=4||site.Vertices.Select(p=>p.X).Distinct().Count()!=2||site.Vertices.Select(p=>p.Y).Distinct().Count()!=2)
            return new(null,[],["COMPOUND_SITE_GEOMETRY_UNSUPPORTED"]);
        double left=site.Vertices.Min(p=>p.X),right=site.Vertices.Max(p=>p.X),top=site.Vertices.Min(p=>p.Y),bottom=site.Vertices.Max(p=>p.Y);
        bool north=Math.Abs(entrance.Y-top)<1e-6;
        if((!north&&Math.Abs(entrance.Y-bottom)>1e-6)||entrance.X<left||entrance.X>right)
            return new(null,[],["COMPOUND_SITE_ENTRANCE_INVALID"]);
        var areas=new List<PlannedSiteArea>();var issues=new List<string>();
        foreach(var rule in rules)
        {
            token.ThrowIfCancellationRequested();
            var previous=(left,right,top,bottom);
            double x0=left,x1=right,y0=top,y1=bottom,depth=rule.MinimumDepthMeters+2*rule.InsetMeters;
            bool horizontal=rule.Band is SiteBand.Front or SiteBand.Rear;
            if((horizontal?bottom-top:right-left)<=depth+1e-8)
            {if(rule.Required)issues.Add("COMPOUND_SITE_AREA_DOES_NOT_FIT:"+rule.Id);continue;}
            if(horizontal)
            {
                bool atTop=rule.Band==SiteBand.Front?north:!north;
                if(atTop){y1=top+depth;top=y1;}else{y0=bottom-depth;bottom=y0;}
            }
            else if(rule.Band==SiteBand.Left){x1=left+depth;left=x1;}
            else{x0=right-depth;right=x0;}
            x0+=rule.InsetMeters;x1-=rule.InsetMeters;y0+=rule.InsetMeters;y1-=rule.InsetMeters;
            int before=areas.Count;
            if(rule.Band==SiteBand.Front&&rule.Kind!=SiteFeatureKind.Forecourt)
            {
                Add(rule.Id+"-left",x0,y0,Math.Min(x1,entrance.X-accessWidthMeters/2),y1);
                Add(rule.Id+"-right",Math.Max(x0,entrance.X+accessWidthMeters/2),y0,x1,y1);
            }
            else Add(rule.Id,x0,y0,x1,y1);
            if(areas.Count==before)
            {
                if(rule.Required)issues.Add("COMPOUND_SITE_AREA_EMPTY:"+rule.Id);
                else(left,right,top,bottom)=previous;
            }
            if(entrance.X-accessWidthMeters/2<left-1e-6||entrance.X+accessWidthMeters/2>right+1e-6)
            {
                if(rule.Required)issues.Add("COMPOUND_SITE_ENTRY_BLOCKED:"+rule.Id);
                else{areas.RemoveRange(before,areas.Count-before);(left,right,top,bottom)=previous;}
            }
            void Add(string suffix,double a,double b,double c,double d)
            {
                if(c-a<=1e-8||d-b<=1e-8)return;
                string part=suffix==rule.Id?"whole":suffix.EndsWith("-left",StringComparison.Ordinal)?"left":"right";
                areas.Add(new(ownerId+"/site/"+Uri.EscapeDataString(rule.Id)+"/"+part,ownerId,rule.Kind.ToString(),Rectangle(a,b,c,d),rule.Id,
                    rule.Band+" "+rule.Kind,rule.Band,rule.SpeciesAssetId,rule.PlantSpacingMeters,rule.PlantMarginMeters,AllowsThroughAccess:PermitsThroughAccess(rule.Kind)));
            }
        }
        if(entrance.X-accessWidthMeters/2<left-1e-6||entrance.X+accessWidthMeters/2>right+1e-6)
            issues.Add("COMPOUND_SITE_ENTRY_BLOCKED");
        return issues.Count>0?new(null,[],issues):new(Rectangle(left,top,right,bottom),areas,[]);
    }
    private static WorldPolygon Rectangle(double left,double top,double right,double bottom)=>new([new(left,top),new(right,top),new(right,bottom),new(left,bottom)]);
}
