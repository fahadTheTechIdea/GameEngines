"""Independent source-area and path-clearance checks for a regional fixture."""
import json
import math
from pathlib import Path
import sys

root = Path(sys.argv[1])

def polygon(points):
    return [(p["X"], p["Y"]) for p in points["Vertices"]]

def area(points):
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1])))/2

def cross(a,b,c):
    return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])

def in_convex(point,poly):
    signs=[cross(a,b,point) for a,b in zip(poly,poly[1:]+poly[:1])]
    return min(signs)>=-1e-9 or max(signs)<=1e-9

def distance_point_segment(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1];denom=dx*dx+dy*dy
    t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/denom)) if denom else 0
    return math.dist(p,(a[0]+t*dx,a[1]+t*dy))

def distance_segments(a,b,c,d):
    if cross(a,b,c)*cross(a,b,d)<0 and cross(c,d,a)*cross(c,d,b)<0:return 0
    return min(distance_point_segment(a,c,d),distance_point_segment(b,c,d),distance_point_segment(c,a,b),distance_point_segment(d,a,b))

for name in ("baseline","changed"):
    world=json.loads((root/name/"world-regions.json").read_text(encoding="utf-8"))
    assert len(world["Regions"])==6 and sum(len(r["World"]["Nodes"]) for r in world["Regions"])==30
    sites=[];solids=[]
    for region in world["Regions"]:
        data=region["World"];nodes={n["Id"]:n for n in data["Nodes"]}
        for site in data["AccessPlan"]["Sites"]:
            node=nodes[site["NodeId"]];source=data["FootprintDefinitions"][node["ArchetypeId"]]["Polygon"]
            original,placed=polygon(source),polygon(site["Footprint"])
            assert len(original)==len(placed) and abs(area(original)-area(placed))<1e-7
            for (a,b),(c,d) in zip(zip(original,original[1:]+original[:1]),zip(placed,placed[1:]+placed[:1])):
                assert abs(math.dist(a,b)-math.dist(c,d))<1e-7
            sites.append(site)
            if site["Occupancy"]=="Solid":solids.append(polygon(site["Footprint"]))
    assert len(sites)==18 and len(world["Connections"])==7
    obstacles=solids+[polygon(e["Boundary"]) for e in world["Definition"]["Surface"]["Exclusions"]]
    domain=polygon(world["Definition"]["Surface"]["Boundary"])
    checked=0
    for link in world["Connections"]:
        radius=link["Definition"]["Profile"]["Radius"]
        for key,start,end in [("Forward",link["From"],link["To"]),("Reverse",link["To"],link["From"])]:
            route=link[key];assert route and route["Status"]=="Found"
            points=[(v["X"],v["Y"]) for v in route["Path"]["Points"]]
            assert math.dist(points[0],(start["X"],start["Y"]))<1e-7 and math.dist(points[-1],(end["X"],end["Y"]))<1e-7
            for a,b in zip(points,points[1:]):
                assert in_convex(a,domain) and in_convex(b,domain)
                for c,d in zip(domain,domain[1:]+domain[:1]):
                    assert distance_segments(a,b,c,d)>=radius-1e-7,(name,"domain",key,a,b,c,d)
                    checked+=1
                for shape in obstacles:
                    assert not in_convex(a,shape) and not in_convex(b,shape)
                    for c,d in zip(shape,shape[1:]+shape[:1]):
                        assert distance_segments(a,b,c,d)>=radius-1e-7,(name,key,a,b,c,d)
                        checked+=1
    print(f"{name}: 18 source-sized sites, 14 directed routes, {checked} independent swept edge checks")
