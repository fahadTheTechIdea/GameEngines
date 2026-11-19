/* Dependency-free schematic presentation. Engine geometry is authoritative when supplied. */
class WorldSchematic {
    static layout(world) {
        if (!world || world.SchemaVersion !== 2 || typeof world.Id !== 'string' || !Array.isArray(world.RemainingScopes) ||
            world.RemainingScopes.some(s => typeof s !== 'string') || !Array.isArray(world.Nodes) || !Array.isArray(world.Connections) ||
            world.Nodes.length === 0 || world.Nodes.length > 5000 || world.Connections.length > 20000)
            throw new Error('Schematic requires 1–5000 nodes and at most 20000 connections.');
        const nodes = new Map(), children = new Map();
        for (const node of world.Nodes) {
            if (!node || typeof node.Id !== 'string' || nodes.has(node.Id) || typeof node.Kind !== 'string')
                throw new Error('Invalid or duplicate schematic node.');
            nodes.set(node.Id, node); children.set(node.Id, []);
        }
        const roots = [];
        for (const node of world.Nodes) {
            if (node.ParentId == null) roots.push(node);
            else {
                if (!nodes.has(node.ParentId)) throw new Error('Schematic parent is missing.');
                children.get(node.ParentId).push(node);
            }
        }
        if (roots.length !== 1) throw new Error('Schematic needs one root.');
        WorldSchematic.capacityAllocations(world,nodes,children);
        const linkIds = new Set();
        for (const link of world.Connections) {
            if (!link || typeof link.Id !== 'string' || linkIds.has(link.Id) || !nodes.has(link.FromId) || !nodes.has(link.ToId))
                throw new Error('Invalid schematic connection.');
            linkIds.add(link.Id);
        }
        WorldSchematic.serviceAllocations(world,nodes);
        WorldSchematic.variation(world,nodes);
        WorldSchematic.contextEvaluations(world,nodes);        WorldSchematic.progression(world,nodes);
        if(world.CorridorPlan!=null&&world.Surface==null)throw new Error('Physical corridors require a surface.');
        if(world.EnvironmentPlan!=null&&world.Surface==null)throw new Error('Environmental point checks require a surface.');
        if (world.Surface != null) return WorldSchematic.surfaceLayout(world,nodes,children,roots[0]);
        if (world.MapGeometry != null) {
            const map = world.MapGeometry, depths = new Map(), active = new Set();
            const visit = (node, depth) => {
                if (depth > 32 || active.has(node.Id)) throw new Error('Invalid spatial hierarchy.');
                active.add(node.Id); depths.set(node.Id, depth);
                children.get(node.Id).forEach(child => visit(child, depth + 1));
            };
            visit(roots[0], 0);
            if (depths.size !== nodes.size || map.Unit !== 'meters' || ![map.Width,map.Height].every(v => Number.isFinite(v) && v > 0) ||
                !Array.isArray(map.Boxes) || map.Boxes.length !== nodes.size || !Array.isArray(map.Roads) || map.Roads.length > 40000)
                throw new Error('Invalid engine map geometry.');
            const seen = new Set();
            const boxes = map.Boxes.map(b => {
                if (!b || !nodes.has(b.NodeId) || seen.has(b.NodeId) || ![b.X,b.Y,b.Width,b.Height].every(Number.isFinite) ||
                    b.Width <= 0 || b.Height <= 0 || b.X < -1e-6 || b.Y < -1e-6 || b.X+b.Width > map.Width+1e-6 || b.Y+b.Height > map.Height+1e-6)
                    throw new Error('Invalid engine map box.');
                seen.add(b.NodeId);
                return { id:b.NodeId,node:nodes.get(b.NodeId),x:b.X,y:b.Y,width:b.Width,height:b.Height,
                    depth:depths.get(b.NodeId),container:children.get(b.NodeId).length > 0 };
            });
            const corridors = map.Roads.map(r => {
                if (!r || ![r.X1,r.Y1,r.X2,r.Y2,r.Width].every(Number.isFinite) || r.Width <= 0 ||
                    r.X1 < 0 || r.Y1 < 0 || r.X2 < 0 || r.Y2 < 0 || r.X1 > map.Width || r.X2 > map.Width || r.Y1 > map.Height || r.Y2 > map.Height)
                    throw new Error('Invalid engine map road.');
                return { x1:r.X1,y1:r.Y1,x2:r.X2,y2:r.Y2,width:r.Width,owner:r.Id };
            });
            if (map.Polygons != null) {
                if (!Array.isArray(map.Polygons) || map.Polygons.length > boxes.length) throw new Error('Invalid engine polygons.');
                const byId = new Map(boxes.map(b => [b.id,b])), seenPolygons = new Set();
                let vertexCount = 0;
                for (const polygon of map.Polygons) {
                    const box = polygon && byId.get(polygon.NodeId), points = polygon?.Vertices;
                    if (!box || seenPolygons.has(polygon.NodeId) || !Array.isArray(points) || points.length < 3 || points.length > 1024 ||
                        (vertexCount += points.length) > 40000) throw new Error('Invalid engine polygon.');
                    seenPolygons.add(polygon.NodeId);
                    for (let i=0; i<points.length; i++) {
                        const a=points[i],b=points[(i+1)%points.length];
                        if (!a || !b || ![a.X,a.Y].every(Number.isFinite) || a.X < box.x-1e-6 || a.Y < box.y-1e-6 ||
                            a.X > box.x+box.width+1e-6 || a.Y > box.y+box.height+1e-6 ||
                            Math.hypot(a.X-b.X,a.Y-b.Y) <= 1e-8) throw new Error('Invalid engine polygon vertex.');
                    }
                    const area=points.reduce((sum,a,i)=>{const b=points[(i+1)%points.length];return sum+a.X*b.Y-b.X*a.Y;},0)/2;
                    if (!Number.isFinite(area) || Math.abs(area)<=1e-8) throw new Error('Invalid engine polygon area.');
                    const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
                    const on=(a,b,p)=>Math.abs(cross(a,b,p))<=1e-8 && p.X>=Math.min(a.X,b.X)-1e-8 && p.X<=Math.max(a.X,b.X)+1e-8 && p.Y>=Math.min(a.Y,b.Y)-1e-8 && p.Y<=Math.max(a.Y,b.Y)+1e-8;
                    for (let i=0;i<points.length;i++) for(let j=i+1;j<points.length;j++) {
                        if(j===i+1 || i===0&&j===points.length-1) continue;
                        const a=points[i],b=points[(i+1)%points.length],c=points[j],d=points[(j+1)%points.length];
                        if (on(a,b,c)||on(a,b,d)||on(c,d,a)||on(c,d,b)||
                            cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0) throw new Error('Self-intersecting engine polygon.');
                    }
                    box.polygon=points.map(p=>({x:p.X,y:p.Y}));
                }
            }
            return {boxes,corridors,width:map.Width,height:map.Height,world,coordinateSpace:'engine-world-meters'};
        }
        const visited = new Set(), sizes = new Map();
        const hint = node => node.Properties?.schematic ?? {};
        const number = (value, fallback, minimum, maximum) => {
            const result = value ?? fallback;
            if (!Number.isFinite(result) || result < minimum || result > maximum) throw new Error('Invalid schematic dimensions.');
            return result;
        };
        const measure = (node, depth) => {
            if (depth > 32 || visited.has(node.Id)) throw new Error('Schematic hierarchy is cyclic or too deep.');
            visited.add(node.Id);
            const h = hint(node), nested = children.get(node.Id);
            const padding = number(h.padding, depth === 0 ? 40 : 18, 8, 160);
            const gap = number(h.gap, depth === 0 ? 56 : 26, 8, 160);
            const header = number(h.header, depth === 0 ? 44 : 36, 24, 100);
            const columns = number(h.columns, Math.ceil(Math.sqrt(nested.length || 1)), 1, 100);
            if (!Number.isInteger(columns)) throw new Error('Schematic columns must be whole numbers.');
            let size;
            if (!nested.length) size = { width: number(h.width, 120, 48, 600), height: number(h.height, 78, 36, 600) };
            else {
                const measures = nested.map(child => measure(child, depth + 1));
                const cols = Math.min(columns, nested.length), rows = Math.ceil(nested.length / cols);
                const cellWidth = Math.max(...measures.map(s => s.width)), cellHeight = Math.max(...measures.map(s => s.height));
                size = { width: padding * 2 + cols * cellWidth + (cols - 1) * gap,
                    height: padding * 2 + header + rows * cellHeight + (rows - 1) * gap,
                    padding, gap, header, cols, rows, cellWidth, cellHeight };
            }
            sizes.set(node.Id, size); return size;
        };
        const root = roots[0], size = measure(root, 0);
        if (visited.size !== nodes.size) throw new Error('Schematic contains unreachable or cyclic nodes.');
        const boxes = [], corridors = [];
        const place = (node, x, y, depth) => {
            const s = sizes.get(node.Id), nested = children.get(node.Id), h = hint(node);
            const box = { id: node.Id, node, x, y, width: s.width, height: s.height, depth, container: nested.length > 0 };
            boxes.push(box);
            if (nested.length && h.corridors === true) {
                const left = x + s.padding, top = y + s.padding + s.header;
                const right = x + s.width - s.padding, bottom = y + s.height - s.padding;
                for (let c = 1; c < s.cols; c++) {
                    const cx = left + c * s.cellWidth + (c - 0.5) * s.gap;
                    corridors.push({ x1: cx, y1: top, x2: cx, y2: bottom, width: s.gap * 0.55, owner: node.Id });
                }
                for (let r = 1; r < s.rows; r++) {
                    const cy = top + r * s.cellHeight + (r - 0.5) * s.gap;
                    corridors.push({ x1: left, y1: cy, x2: right, y2: cy, width: s.gap * 0.55, owner: node.Id });
                }
            }
            nested.forEach((child, i) => {
                const childSize = sizes.get(child.Id);
                place(child, x + s.padding + (i % s.cols) * (s.cellWidth + s.gap) + (s.cellWidth - childSize.width) / 2,
                    y + s.padding + s.header + Math.floor(i / s.cols) * (s.cellHeight + s.gap) + (s.cellHeight - childSize.height) / 2, depth + 1);
            });
        };
        place(root, 24, 24, 0);
        return { boxes, corridors, width: size.width + 48, height: size.height + 48, world };
    }

    static surfaceLayout(world,nodes,children,root) {
        const surface=world.Surface, seen=new Set(), depths=new Map();
        const visit=(node,depth)=>{
            if(depth>32||seen.has(node.Id))throw new Error('Invalid surface hierarchy.');
            seen.add(node.Id);depths.set(node.Id,depth);children.get(node.Id).forEach(n=>visit(n,depth+1));
        };
        visit(root,0);
        if(seen.size!==nodes.size || surface.SchemaVersion!==1 || surface.Unit!==world.Space?.Unit ||
            world.Space.Axes?.length!==2 || !Array.isArray(surface.Patches) || surface.Patches.length>1024 ||
            !Array.isArray(surface.Exclusions) || surface.Exclusions.length>256)throw new Error('Invalid surface frame or budget.');
        let vertices=0;
        const polygon=p=>{
            if(!p || !Array.isArray(p.Vertices) || p.Vertices.length<3 || p.Vertices.length>1024 ||
                (vertices+=p.Vertices.length)>16384 || p.Vertices.some(v=>!v||![v.X,v.Y].every(n=>Number.isFinite(n)&&Math.abs(n)<=10000000)))
                throw new Error('Invalid surface polygon.');
            return p.Vertices.map(v=>({x:v.X,y:v.Y}));
        };
        const boundary=polygon(surface.Boundary), terrain=surface.Patches.map(p=>{
            if(!p || typeof p.Kind!=='string' || typeof p.Id!=='string' || !Number.isInteger(p.Priority))throw new Error('Invalid terrain patch.');
            return {id:p.Id,kind:p.Kind,priority:p.Priority,points:polygon(p.Boundary),purpose:p.Purpose};
        }).sort((a,b)=>a.priority-b.priority||a.id.localeCompare(b.id));
        const exclusions=surface.Exclusions.map(e=>({id:e.Id,purpose:e.Purpose,points:polygon(e.Boundary)}));
        const minX=Math.min(...boundary.map(p=>p.x)),minY=Math.min(...boundary.map(p=>p.y));
        const width=Math.max(...boundary.map(p=>p.x))-minX,height=Math.max(...boundary.map(p=>p.y))-minY;
        if(width<=0||height<=0)throw new Error('Empty surface extent.');
        const marker=Math.min(width,height)*.014, boxes=[];
        for(const node of world.Nodes) {
            if(node.Position==null)continue;
            if(!Array.isArray(node.Position)||node.Position.length!==2||!node.Position.every(Number.isFinite))throw new Error('Invalid destination position.');
            const [x,y]=node.Position;
            boxes.push({id:node.Id,node,x:x-marker,y:y-marker,width:marker*2,height:marker*2,marker:true,
                depth:depths.get(node.Id),container:false});
        }
        const access=WorldSchematic.physicalAccess(world,nodes);
        const endpoint=(id,portId)=>{
            const site=access.get(id);
            if(portId==null){if(site?.Occupancy==='Solid')throw new Error('Solid endpoint needs an entrance.');return nodes.get(id)?.Position;}
            const port=site?.Ports.find(p=>p.Id===portId);if(!port)throw new Error('Missing route entrance.');
            return [port.ApproachPosition.X,port.ApproachPosition.Y];
        };
        const routes=[],rules=new Map(),connectionMap=new Map(world.Connections.map(c=>[c.Id,c]));
        const routePlan={Rules:[],Routes:[]};
        if(world.RoutePlan!=null){
            if(!Array.isArray(world.RoutePlan.Rules)||world.RoutePlan.Rules.length>16||!Array.isArray(world.RoutePlan.Routes)||world.RoutePlan.Routes.reduce((count,r)=>count+1+(r?.Reverse?1:0),0)>128)
                throw new Error('Invalid route plan.');
            routePlan.Rules.push(...world.RoutePlan.Rules);routePlan.Routes.push(...world.RoutePlan.Routes);
        }
        if(world.NetworkPlan!=null){
            const network=world.NetworkPlan;
            if(!Array.isArray(network.Rules)||network.Rules.length>4||!Array.isArray(network.Networks)||network.Networks.length!==network.Rules.length)
                throw new Error('Invalid network plan.');
            const networkRules=new Map(network.Rules.map(r=>[r.Id,r])),seenNetworks=new Set();
            if(networkRules.size!==network.Rules.length)throw new Error('Duplicate network rule.');
            for(const n of network.Networks){
                const rule=networkRules.get(n.RuleId),plan=n.Plan;
                if(!rule||seenNetworks.has(n.RuleId)||plan?.Status!=='Found'||!Array.isArray(plan.Edges)||plan.Edges.length>496)
                    throw new Error('Invalid generated network.');
                seenNetworks.add(n.RuleId);
                const ruleId=`network/${rule.Id}`;
                routePlan.Rules.push({Id:ruleId,ConnectionKind:rule.ConnectionKind,Profile:plan.Options?.Profile,FromPortId:rule.PortId,ToPortId:rule.PortId});
                for(const edge of plan.Edges)routePlan.Routes.push({ConnectionId:edge.Link?.Id,RuleId:ruleId,Forward:edge.Forward,Reverse:edge.Reverse});
            }
        }
        if(routePlan.Rules.length){
            for(const rule of routePlan.Rules){
                if(!rule||rules.has(rule.Id)||!Number.isFinite(rule.Profile?.Radius)||rule.Profile.Radius<=0)throw new Error('Invalid route rule.');
                rules.set(rule.Id,rule);
            }
            const routed=new Set();
            for(const entry of routePlan.Routes){
                const rule=rules.get(entry.RuleId),link=connectionMap.get(entry.ConnectionId);
                if(!rule||!link||routed.has(link.Id)||link.Kind!==rule.ConnectionKind||(!link.Bidirectional)!==(entry.Reverse==null))throw new Error('Invalid routed connection.');
                routed.add(link.Id);
                for(const [result,reverse] of [[entry.Forward,false],[entry.Reverse,true]]){
                    if(reverse&&result==null)continue;
                    const points=result?.Path?.Points,from=endpoint(reverse?link.ToId:link.FromId,reverse?rule.ToPortId:rule.FromPortId),to=endpoint(reverse?link.FromId:link.ToId,reverse?rule.FromPortId:rule.ToPortId);
                    if(result?.Status!=='Found'||result.SurfaceId!==surface.Id||result.ProfileId!==rule.Profile.Id||
                        !Array.isArray(points)||points.length<2||points.length>1024||points.some(p=>!p||![p.X,p.Y].every(Number.isFinite))||
                        !from||!to||points[0].X!==from[0]||points[0].Y!==from[1]||points.at(-1).X!==to[0]||points.at(-1).Y!==to[1])
                        throw new Error('Invalid route geometry.');
                    routes.push({id:result.Id,connectionId:link.Id,purpose:result.Purpose,sourceRuleId:result.SourceRuleId,
                        points:points.map(p=>({x:p.X,y:p.Y})),width:rule.Profile.Radius*2,reverse});
                }
            }
        }
        const heightField=surface.HeightField==null?null:WorldSchematic.heightField(surface.HeightField,surface.Unit);
        if(surface.Terrain!=null&&!heightField)throw new Error('Generated terrain field missing. Regenerate with the engine.');
        const suitability=WorldSchematic.environmentObservations(world,nodes);
        for(const [nodeId,entries] of WorldSchematic.placementObservations(world,nodes)){
            if(!suitability.has(nodeId))suitability.set(nodeId,[]);suitability.get(nodeId).push(...entries);
        }
        for(const box of boxes){
            box.physicalAccess=access.get(box.id);
            const entries=suitability.get(box.id)||[],vertices=entries.flatMap(e=>e.Footprint?.Footprint.Vertices||[]);
            if(vertices.length){
                const x=Math.min(...vertices.map(p=>p.X)),y=Math.min(...vertices.map(p=>p.Y));
                box.focusBounds={x,y,width:Math.max(...vertices.map(p=>p.X))-x,height:Math.max(...vertices.map(p=>p.Y))-y};
                box.catalogFootprint=entries.some(e=>e.Placement?.FootprintSource?.SourceId?.startsWith('archetype:')||e.Placement?.FootprintSource?.SourceId?.startsWith('structure:'));
            }
        }
        let viewMinX=minX,viewMinY=minY,viewMaxX=minX+width,viewMaxY=minY+height;
        for(const entries of suitability.values())for(const entry of entries)for(const p of entry.Footprint?.Footprint.Vertices||[]){
            viewMinX=Math.min(viewMinX,p.X);viewMinY=Math.min(viewMinY,p.Y);viewMaxX=Math.max(viewMaxX,p.X);viewMaxY=Math.max(viewMaxY,p.Y);
        }
        const margin=Math.max(viewMaxX-viewMinX,viewMaxY-viewMinY)*.04;
        const feasibility=WorldSchematic.placementFeedback(world);
        const physicalCorridors=WorldSchematic.physicalCorridors(world,routes);
        const generatedRegions=WorldSchematic.generatedRegions(world);
        const localLayouts=WorldSchematic.localLayouts(world);        const interfaceRoutes=WorldSchematic.interfaceRoutes(world,localLayouts);        const layoutAdaptation=WorldSchematic.layoutAdaptation(world);
        return {world,boxes,corridors:[],physicalCorridors,generatedRegions,localLayouts,interfaceRoutes,layoutAdaptation,terrain,exclusions,boundary,routes,heightField,suitability,feasibility,protections:world.ContextPlan?.Protections||[],unit:surface.Unit,
            minX:viewMinX-margin,minY:viewMinY-margin,width:viewMaxX-viewMinX+margin*2,height:viewMaxY-viewMinY+margin*2,coordinateSpace:'engine-surface',marker};
    }

    static layoutAdaptation(world) {        const plan=world.LayoutAdaptationPlan;if(plan==null)return null;        const fail=()=>{throw new Error('Invalid local repartition plan. Regenerate with the engine.');};        const policy=plan.Policy,original=plan.OriginalLayouts,actual=world.LocalLayoutPlan?.Definition;        if(plan.Version!==1||!policy||!Array.isArray(policy.Dials)||policy.Dials.length<1||policy.Dials.length>3||            !Number.isInteger(policy.MaximumAttempts)||policy.MaximumAttempts<1||policy.MaximumAttempts>64||            !Array.isArray(plan.Attempts)||!plan.Attempts.length||plan.Attempts.length>policy.MaximumAttempts||            !Number.isSafeInteger(plan.Work)||plan.Work<0||plan.Work>policy.MaximumWork||!actual||!Array.isArray(original?.Layouts)||            original.Layouts.length!==actual.Layouts.length||!plan.OriginalPlacement)fail();        const dials=new Map(policy.Dials.map(d=>[d.Id,d]));if(dials.size!==policy.Dials.length)fail();        let previous=-1;        for(let i=0;i<plan.Attempts.length;i++){            const a=plan.Attempts[i],last=i===plan.Attempts.length-1;            if(a?.Ordinal!==i+1||a.Accepted!==last||!a.Steps||Object.keys(a.Steps).length!==dials.size||                !Number.isSafeInteger(a.Work)||a.Work<0||typeof a.LayoutSha256!=='string'||!/^[a-f0-9]{64}$/.test(a.LayoutSha256)||                (last?a.Rejection!=null:typeof a.Rejection!=='string'||!a.Rejection.length))fail();            let distance=0;            for(const [id,value] of Object.entries(a.Steps)){                const d=dials.get(id);if(!d||!Number.isInteger(value)||value<d.MinimumSteps||value>d.MaximumSteps||i===0&&value!==0)fail();                distance+=Math.abs(value);            }            if(distance<previous)fail();previous=distance;        }        const selected=plan.Attempts.at(-1).Steps;        const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);        for(const source of original.Layouts){            const expected=JSON.parse(JSON.stringify(source)),found=actual.Layouts.find(l=>l.Id===source.Id);if(!found)fail();            if(source.Id===policy.LayoutId)for(const d of policy.Dials){                if(!Number.isFinite(d.Step)||d.Step<=0)fail();const delta=d.Step*selected[d.Id];                if(d.Kind==='PowerInfluence'){                    const p=expected.Programs.find(p=>p.Id===d.ProgramId);if(!p||expected.Strategy!=='PowerDiagram')fail();p.Influence+=delta;                }else if(d.Kind==='GridWidth'||d.Kind==='GridHeight'){                    if(expected.Strategy!=='OrientedGrid'||!expected.Grid||d.ProgramId!=null)fail();expected.Grid[d.Kind==='GridWidth'?'CellWidth':'CellHeight']+=delta;                }else fail();            }            expected.Programs.sort((a,b)=>a.Id<b.Id?-1:a.Id>b.Id?1:0);            if(!same(expected,found))fail();        }        if(!original.Layouts.some(l=>l.Id===policy.LayoutId)||!same([...original.Interfaces].sort((a,b)=>a.Id<b.Id?-1:1),actual.Interfaces))fail();        return plan;    }    static interfaceRoutes(world,layouts) {        const fail=()=>{throw new Error('Invalid physical interface route. Regenerate with the engine.');};        const point=p=>p&&[p.X,p.Y].every(Number.isFinite);        const equal=(a,b)=>point(a)&&point(b)&&a.X===b.X&&a.Y===b.Y;        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b));        const on=(p,span)=>point(p)&&point(span?.From)&&point(span?.To)&&            near(Math.hypot(p.X-span.From.X,p.Y-span.From.Y)+Math.hypot(p.X-span.To.X,p.Y-span.To.Y),Math.hypot(span.To.X-span.From.X,span.To.Y-span.From.Y));        const result=[],bindings=new Map();        for(const rule of world.RoutePlan?.Rules||[])if(rule.Interfaces!=null){            if(!Array.isArray(rule.Interfaces)||rule.Interfaces.length<1||rule.Interfaces.length>16)fail();            for(const binding of rule.Interfaces){                if(!binding||bindings.has(binding.ConnectionId)||!Number.isInteger(binding.MaximumCrossingCandidates)||binding.MaximumCrossingCandidates<1||binding.MaximumCrossingCandidates>16)fail();                bindings.set(binding.ConnectionId,{binding,rule});            }        }        for(const route of world.RoutePlan?.Routes||[]){            const selected=bindings.get(route.ConnectionId),item=route.Interface;            if(!selected){if(item!=null)fail();continue;}            bindings.delete(route.ConnectionId);            const {binding,rule}=selected,required=layouts?.Interfaces.find(i=>i.Id===binding.InterfaceId);            if(!item||!required||route.RuleId!==rule.Id||item.InterfaceId!==required.Id||                ![required.Requirement.FromId,required.Requirement.ToId].includes(item.FromAllocationId)||                ![required.Requirement.FromId,required.Requirement.ToId].includes(item.ToAllocationId)||item.FromAllocationId===item.ToAllocationId||                !required.Segments.some(s=>equal(s.From,item.Span?.From)&&equal(s.To,item.Span?.To))||!on(item.Center,item.Span)||                !Array.isArray(item.Attempts)||item.Attempts.length<1||item.Attempts.length>binding.MaximumCrossingCandidates)fail();            for(let i=0;i<item.Attempts.length;i++){                const a=item.Attempts[i],last=i===item.Attempts.length-1;                if(a?.Ordinal!==i+1||!point(a.Center)||!Number.isSafeInteger(a.Work)||a.Work<0||a.Accepted!==last||                    (last?(a.Rejection!=null||!equal(a.Center,item.Center)):typeof a.Rejection!=='string'||!a.Rejection.length))fail();            }            for(const [combined,journey] of [[route.Forward,item.Forward],[route.Reverse,item.Reverse]]){                if(combined==null){if(journey!=null)fail();continue;}                if(combined.SearchModel!=='allocation-interface-v1'||!journey)fail();                const parts=[journey.FromLeg,journey.Crossing,journey.ToLeg];                for(let i=0;i<parts.length;i++){                    const p=parts[i],suffix=['/from','/crossing','/to'][i];                    if(p?.Status!=='Found'||p.Id!==combined.Id+suffix||p.SurfaceId!==combined.SurfaceId||p.ProfileId!==combined.ProfileId||                        !Array.isArray(p.Path?.Points)||p.Path.Points.length<2||p.Path.Points.length>256||!p.Path.Points.every(point))fail();                }                if(!equal(parts[0].Path.Points.at(-1),parts[1].Path.Points[0])||!equal(parts[1].Path.Points.at(-1),parts[2].Path.Points[0]))fail();                const crossing=parts[1].Path.Points,a=crossing[0],b=crossing.at(-1),span=item.Span;                if(crossing.length!==2||!near((a.X+b.X)/2,item.Center.X)||!near((a.Y+b.Y)/2,item.Center.Y)||                    !near((b.X-a.X)*(span.To.X-span.From.X)+(b.Y-a.Y)*(span.To.Y-span.From.Y),0))fail();                const joined=[...parts[0].Path.Points,...crossing.slice(1),...parts[2].Path.Points.slice(1)];                if(joined.length!==combined.Path?.Points.length||joined.some((p,i)=>!equal(p,combined.Path.Points[i])))fail();                for(const key of ['Distance','Terrain','Ascent','Descent','ReusedDistance','NewPassage'])                    if(!near(combined.Cost?.[key],parts.reduce((sum,p)=>sum+p.Cost?.[key],0)))fail();            }            result.push({connectionId:route.ConnectionId,width:rule.Profile.Radius*2,...item});        }        if(bindings.size)fail();        return result;    }    static generatedRegions(world) {
        const plan=world.RegionPlan;
        const fail=()=>{throw new Error('Invalid generated region plan. Regenerate with the engine.');};
        if(plan==null){
            for(const stage of [world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)])
                if(stage?.Request?.Demands?.some(d=>d.RegionId!=null))fail();
            return null;
        }
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y)&&Math.abs(p.X)<=1e7&&Math.abs(p.Y)<=1e7;
        const polygon=p=>p&&Array.isArray(p.Vertices)&&p.Vertices.length>=3&&p.Vertices.length<=512&&p.Vertices.every(point);
        const area=p=>{const o=p.Vertices[0];let sum=0;for(let i=1;i+1<p.Vertices.length;i++)sum+=(p.Vertices[i].X-o.X)*(p.Vertices[i+1].Y-o.Y)-(p.Vertices[i].Y-o.Y)*(p.Vertices[i+1].X-o.X);return sum/2;};
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=Math.max(1,Math.abs(a),Math.abs(b))*1e-9;
        const request=plan.Request;
        if(!request||!polygon(request.Boundary)||!world.EnvironmentPlan||!Array.isArray(request.Seeds)||request.Seeds.length<1||request.Seeds.length>32||
            !Array.isArray(plan.Regions)||plan.Regions.length!==request.Seeds.length||!Number.isInteger(request.MaximumCells)||request.MaximumCells<1||request.MaximumCells>4096||
            ![plan.BoundaryArea,plan.SuitableArea,plan.UnclaimedSuitableArea,plan.IneligibleArea].every(v=>Number.isFinite(v)&&v>=0)||
            !near(plan.BoundaryArea,Math.abs(area(request.Boundary)))||!near(plan.BoundaryArea,plan.SuitableArea+plan.IneligibleArea))fail();
        const seeds=new Map(request.Seeds.map(s=>[s?.Id,s]));if(seeds.size!==request.Seeds.length)fail();
        const ids=new Set();let total=0,cells=0;
        for(const region of plan.Regions){
            const seed=seeds.get(region?.Id);
            if(!seed||ids.has(region.Id)||!point(seed.Position)||!Number.isFinite(seed.MinimumArea)||seed.MinimumArea<=0||
                JSON.stringify(seed)!==JSON.stringify(region.Seed)||!Array.isArray(region.Cells)||!region.Cells.length||
                (cells+=region.Cells.length)>request.MaximumCells||region.Cells.some(p=>!polygon(p)||area(p)<=0)||
                !Number.isFinite(region.Area)||region.Area<seed.MinimumArea||!near(region.Area,region.Cells.reduce((n,p)=>n+area(p),0))||
                !Array.isArray(region.BoundaryEdges)||!region.BoundaryEdges.length||region.BoundaryEdges.length>32768||
                region.BoundaryEdges.some(e=>!point(e?.From)||!point(e?.To)||(e.From.X===e.To.X&&e.From.Y===e.To.Y)))fail();
            ids.add(region.Id);total+=region.Area;
        }
        if(!near(total+plan.UnclaimedSuitableArea,plan.SuitableArea))fail();
        for(const stage of [world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)])
            for(const demand of stage?.Request?.Demands||[])if(demand.RegionId!=null&&!ids.has(demand.RegionId))fail();
        return plan;
    }

    static localLayouts(world) {
        const plan=world.LocalLayoutPlan,fail=()=>{throw new Error('Invalid local layout plan. Regenerate with the engine.');};
        const demands=[world.PlacementPlan?.Placement,...(world.PlacementPlan?.NestedStages||[]).map(s=>s.Placement)].flatMap(s=>s?.Request?.Demands||[]);
        if(plan==null){if(demands.some(d=>d.AllocationId!=null))fail();return null;}
        const list=(a,max)=>Array.isArray(a)&&a.length<=max;
        const point=p=>p&&[p.X,p.Y].every(v=>Number.isFinite(v)&&Math.abs(v)<=1e7);
        const edge=e=>e&&point(e.From)&&point(e.To)&&Math.hypot(e.To.X-e.From.X,e.To.Y-e.From.Y)>1e-8;
        const area=p=>{const o=p.Vertices[0];let sum=0;for(let i=1;i+1<p.Vertices.length;i++)sum+=(p.Vertices[i].X-o.X)*(p.Vertices[i+1].Y-o.Y)-(p.Vertices[i].Y-o.Y)*(p.Vertices[i+1].X-o.X);return sum/2;};
        const near=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=Math.max(1,Math.abs(a),Math.abs(b))*1e-9;
        const def=plan.Definition;
        if(!world.RegionPlan||!def||!list(def.Layouts,32)||!def.Layouts.length||!list(def.Interfaces,128)||!list(plan.Allocations,128)||
            !list(plan.Areas,32)||plan.Areas.length!==def.Layouts.length||!list(plan.Interfaces,128)||plan.Interfaces.length!==def.Interfaces.length||
            !Number.isInteger(def.MaximumCells)||def.MaximumCells<1||def.MaximumCells>16384||!/^[a-f0-9]{64}$/.test(plan.RegionPlanSha256))fail();
        const regions=new Map(world.RegionPlan.Regions.map(r=>[r.Id,r])),layouts=new Map(),programs=new Map(),allocations=new Map();
        for(const layout of def.Layouts){
            if(!layout||layouts.has(layout.Id)||!regions.has(layout.RegionId)||!['OrientedGrid','PowerDiagram'].includes(layout.Strategy)||!list(layout.Programs,32)||!layout.Programs.length)fail();
            layouts.set(layout.Id,layout);
            for(const p of layout.Programs){if(!p||programs.has(p.Id)||!point(p.Anchor)||!Number.isFinite(p.MinimumArea)||p.MinimumArea<=0)fail();programs.set(p.Id,{program:p,layout});}
        }
        if(plan.Allocations.length!==programs.size)fail();let cells=0;
        for(const allocation of plan.Allocations){
            const entry=programs.get(allocation?.Id),shape=allocation?.Shape;
            if(!entry||allocations.has(allocation.Id)||allocation.LayoutId!==entry.layout.Id||allocation.RegionId!==entry.layout.RegionId||
                JSON.stringify(allocation.Program)!==JSON.stringify(entry.program)||!shape||shape.Id!==allocation.Id||shape.Seed?.Id!==allocation.Id||
                !point(shape.Seed.Position)||shape.Seed.Position.X!==entry.program.Anchor.X||shape.Seed.Position.Y!==entry.program.Anchor.Y||
                shape.Seed.MinimumArea!==entry.program.MinimumArea||!list(shape.Cells,def.MaximumCells)||!shape.Cells.length||
                (cells+=shape.Cells.length)>def.MaximumCells||shape.Cells.some(p=>!list(p?.Vertices,512)||p.Vertices.length<3||!p.Vertices.every(point)||area(p)<=0)||
                !near(shape.Area,shape.Cells.reduce((s,p)=>s+area(p),0))||shape.Area<entry.program.MinimumArea||
                !list(shape.BoundaryEdges,32768)||!shape.BoundaryEdges.length||!shape.BoundaryEdges.every(edge))fail();
            allocations.set(allocation.Id,allocation);
        }
        const areaIds=new Set(),scopeOwners=new Set();
        for(const layout of layouts.values()){
            const ownerKey=JSON.stringify([layout.RegionId,layout.ParentAllocationId??null]);
            if(scopeOwners.has(ownerKey))fail();scopeOwners.add(ownerKey);
            const visited=new Set();let current=layout;
            while(current.ParentAllocationId!=null){
                if(typeof current.ParentAllocationId!=='string'||visited.has(current.Id))fail();visited.add(current.Id);
                const parent=allocations.get(current.ParentAllocationId);
                if(!parent||parent.RegionId!==current.RegionId)fail();
                current=layouts.get(parent.LayoutId);if(!current)fail();
            }
        }
        for(const a of plan.Areas){
            const layout=layouts.get(a?.LayoutId),region=layout?.ParentAllocationId!=null?allocations.get(layout.ParentAllocationId)?.Shape:regions.get(a?.RegionId);
            if(!layout||!region||areaIds.has(a.LayoutId)||layout.RegionId!==a.RegionId||(a.ParentAllocationId??null)!==(layout.ParentAllocationId??null)||!near(a.RegionArea,region.Area)||
                ![a.AllocatedArea,a.ResidualArea].every(v=>Number.isFinite(v)&&v>=0)||!near(a.AllocatedArea+a.ResidualArea,a.RegionArea)||
                !near(a.AllocatedArea,plan.Allocations.filter(x=>x.LayoutId===a.LayoutId).reduce((s,x)=>s+x.Shape.Area,0))||
                (a.ResidualArea>Math.max(1,a.RegionArea)*1e-10&&typeof a.ResidualPurpose!=='string'))fail();
            areaIds.add(a.LayoutId);
        }
        const requirements=new Map(def.Interfaces.map(i=>[i.Id,i])),seen=new Set();if(requirements.size!==def.Interfaces.length)fail();
        for(const item of plan.Interfaces){
            const requirement=requirements.get(item?.Id),span=item?.SelectedSpan;
            if(!requirement||seen.has(item.Id)||!allocations.has(requirement.FromId)||!allocations.has(requirement.ToId)||requirement.FromId===requirement.ToId||
                JSON.stringify(requirement)!==JSON.stringify(item.Requirement)||!list(item.Segments,32768)||!item.Segments.length||!item.Segments.every(edge)||
                !edge(span)||!item.Segments.some(e=>JSON.stringify(e)===JSON.stringify(span))||
                !Number.isFinite(requirement.MinimumSpan)||requirement.MinimumSpan<=0||Math.hypot(span.To.X-span.From.X,span.To.Y-span.From.Y)<requirement.MinimumSpan||
                !point(item.Position)||!near(item.Position.X,(span.From.X+span.To.X)/2)||!near(item.Position.Y,(span.From.Y+span.To.Y)/2))fail();
            seen.add(item.Id);
        }
        for(const demand of demands)if(demand.AllocationId!=null&&allocations.get(demand.AllocationId)?.RegionId!==demand.RegionId)fail();
        return plan;
    }

    static physicalCorridors(world,routes) {
        const plan=world.CorridorPlan;if(plan==null)return [];
        const fail=()=>{throw new Error('Invalid physical corridor plan. Regenerate with the engine.');};
        const point=p=>p&&Number.isFinite(p.X)&&Number.isFinite(p.Y);
        const same=(a,b)=>a.X===b.X&&a.Y===b.Y;
        const list=(v,max)=>Array.isArray(v)&&v.length<=max;
        const strings=v=>list(v,128)&&v.every(s=>typeof s==='string')&&new Set(v).size===v.length;
        if(!list(plan.Groups,16)||!plan.Groups.length||!list(plan.Definition?.Groups,16)||plan.Definition.Groups.length!==plan.Groups.length)fail();
        const seen=new Set(),kinds=new Set(),routeMap=new Map(routes.map(r=>[r.id,r]));
        for(const group of plan.Groups){
            const d=group?.Definition,declared=plan.Definition.Groups.find(g=>g.Id===d?.Id);
            if(!declared||seen.has(d.Id)||kinds.has(d.Movement)||declared.ConnectionKind!==d.Movement||d.Level!==0||declared.Level!==0||
                d.JunctionPolicy!=='connect-at-grade'||declared.JunctionPolicy!==d.JunctionPolicy||group.GeometryModel!=='surface-circular-at-grade-v1'||
                group.IntersectionTolerance!==1e-8||!list(group.Segments,16384)||!group.Segments.length||!list(group.Junctions,2048)||
                !list(group.Journeys,128)||!group.Journeys.length||!list(group.SharedDependencies,16384))fail();
            seen.add(d.Id);kinds.add(d.Movement);
            const segments=new Map(group.Segments.map(s=>[s.Id,s])),junctions=new Map(group.Junctions.map(j=>[j.Id,j])),journeys=new Map(group.Journeys.map(j=>[j.Id,j]));
            if(segments.size!==group.Segments.length||junctions.size!==group.Junctions.length||journeys.size!==group.Journeys.length)fail();
            for(const s of segments.values())if(typeof s.Id!=='string'||!point(s.From)||!point(s.To)||same(s.From,s.To)||!Number.isFinite(s.Width)||
                s.Width<=0||s.Width!==2*group.Profile?.Radius||!strings(s.ForwardJourneys)||!strings(s.ReverseJourneys)||
                !s.ForwardJourneys.concat(s.ReverseJourneys).length||s.ForwardJourneys.concat(s.ReverseJourneys).some(id=>!journeys.has(id)))fail();
            const transitionKey=t=>JSON.stringify([t.FromSegmentId,t.FromForward,t.ToSegmentId,t.ToForward]);
            for(const j of junctions.values()){
                if(typeof j.Id!=='string'||!point(j.Position)||!list(j.Envelope?.Vertices,32)||j.Envelope.Vertices.length<3||
                    j.Envelope.Vertices.some(p=>!point(p))||!list(j.SegmentIds,2048)||!j.SegmentIds.length||new Set(j.SegmentIds).size!==j.SegmentIds.length||
                    !list(j.Transitions,4096))fail();
                const incoming=[],outgoing=[];
                for(const id of j.SegmentIds){const s=segments.get(id);if(!s)fail();
                    const from=s.FromJunctionId===j.Id,to=s.ToJunctionId===j.Id;
                    if(from===to||!same(j.Position,from?s.From:s.To))fail();
                    if(s.ForwardJourneys.length)(from?outgoing:incoming).push([id,true]);
                    if(s.ReverseJourneys.length)(from?incoming:outgoing).push([id,false]);
                }
                const expected=new Set(incoming.flatMap(a=>outgoing.map(b=>JSON.stringify([...a,...b]))));
                if(expected.size!==j.Transitions.length||new Set(j.Transitions.map(transitionKey)).size!==j.Transitions.length||j.Transitions.some(t=>!expected.has(transitionKey(t))))fail();
            }
            for(const s of segments.values())if(!junctions.get(s.FromJunctionId)?.SegmentIds.includes(s.Id)||!junctions.get(s.ToJunctionId)?.SegmentIds.includes(s.Id))fail();
            const expectedRoutes=routes.filter(r=>world.Connections.find(c=>c.Id===r.connectionId)?.Kind===d.Movement);
            if(expectedRoutes.length!==journeys.size||expectedRoutes.some(r=>!journeys.has(r.id)))fail();
            for(const j of journeys.values()){
                const route=routeMap.get(j.Id);
                if(!route||j.ConnectionId!==route.connectionId||!point(j.From)||!point(j.To)||!same(j.From,{X:route.points[0].x,Y:route.points[0].y})||
                    !same(j.To,{X:route.points.at(-1).x,Y:route.points.at(-1).y})||!list(j.Steps,16384)||!j.Steps.length)fail();
                let cursor=j.From;
                for(const step of j.Steps){const s=segments.get(step.SegmentId);if(!s||typeof step.Forward!=='boolean'||
                    !(step.Forward?s.ForwardJourneys:s.ReverseJourneys).includes(j.Id)||!same(cursor,step.Forward?s.From:s.To))fail();cursor=step.Forward?s.To:s.From;}
                if(!same(cursor,j.To))fail();
            }
            const dependencies=group.Segments.map(s=>({SegmentId:s.Id,ConnectionIds:[...new Set(s.ForwardJourneys.concat(s.ReverseJourneys).map(id=>journeys.get(id).ConnectionId))].sort()})).filter(d=>d.ConnectionIds.length>1);
            if(JSON.stringify(dependencies)!==JSON.stringify(group.SharedDependencies))fail();
        }
        return plan.Groups;
    }

    static capacityAllocations(world,nodes,children) {
        const plan=world.CapacityPlan;if(plan==null)return [];
        if(!plan.Definitions||!Array.isArray(plan.Allocations)||plan.Allocations.length<1||plan.Allocations.length>4096)
            throw new Error('Invalid capacity plan.');
        const groups=new Set();
        for(const allocation of plan.Allocations){
            const request=allocation?.Request,source=allocation?.Source,rule=request?.Rule;
            if(!request||!rule||!source||!nodes.has(request.ParentNodeId)||typeof request.PartId!=='string'||
                !Number.isSafeInteger(allocation.Count)||allocation.Count<0||allocation.Count>rule.MaximumCount||allocation.Count<request.MinimumCount||
                !Number.isFinite(request.RequiredQuantity)||request.RequiredQuantity<0||source.Unit!==rule.Unit||
                !Number.isFinite(source.Quantity)||source.Quantity<=0||!Number.isFinite(allocation.ProvidedQuantity)||
                allocation.ProvidedQuantity!==allocation.Count*source.Quantity||allocation.ProvidedQuantity+1e-8<request.RequiredQuantity||
                !Array.isArray(allocation.NodeIds)||allocation.NodeIds.length!==allocation.Count)
                throw new Error('Invalid capacity allocation.');
            const group=`${request.ParentNodeId}/${request.PartId}`;
            if(groups.has(group))throw new Error('Duplicate capacity group.');groups.add(group);
            const prefix=group+'-',members=children.get(request.ParentNodeId).filter(n=>n.Id.startsWith(prefix)&&/^\d+$/.test(n.Id.slice(prefix.length)));
            if(members.length!==allocation.Count||allocation.NodeIds.some((id,i)=>id!==prefix+i||!nodes.has(id)||
                nodes.get(id).ParentId!==request.ParentNodeId||nodes.get(id).ArchetypeId!==request.ArchetypeId||nodes.get(id).StructureKey!==request.StructureKey))
                throw new Error('Capacity membership mismatch.');
            const expected=rule.UseStructureVariant?world.StructureDefinitions?.[request.StructureKey]:
                plan.Definitions[request.ArchetypeId]?.find(c=>c.Id===rule.CapacityId);
            if(!expected||source.Quantity!==(rule.UseStructureVariant?expected.Variant.ServiceCapacity:expected.Quantity)||
                source.Unit!==(rule.UseStructureVariant?expected.Definition.CapacityUnit:expected.Unit))throw new Error('Capacity source mismatch.');
        }
        return plan.Allocations;
    }

    static serviceAllocations(world,nodes) {
        const plan=world.ServicePlan;if(plan==null)return;
        const fail=()=>{throw new Error('Invalid service delivery record. Regenerate with the engine.');};
        const positive=n=>Number.isFinite(n)&&n>0, close=(a,b)=>Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-8*Math.max(1,Math.abs(a),Math.abs(b));
        if(!Array.isArray(plan.Sources)||plan.Sources.length>4096||!Array.isArray(plan.Allocations)||plan.Allocations.length>4096||
            !Array.isArray(plan.Definition?.Bindings)||!Array.isArray(plan.Definition?.Links)||!plan.Profiles||!plan.Usage||
            !Number.isSafeInteger(plan.Work)||plan.Work<0)fail();
        const sources=new Map(),limits=new Map(),usage=new Map(),bound=new Set(),links=new Map();
        const graph=new Map(world.Connections.map(c=>[c.Id,c]));
        for(const source of plan.Sources){
            if(!nodes.has(source?.NodeId)||sources.has(source.NodeId)||!plan.Profiles[nodes.get(source.NodeId).ArchetypeId]||
                !Array.isArray(source.Supplies)||!Array.isArray(source.Requirements)||!Array.isArray(source.RemainingRequirements))fail();
            sources.set(source.NodeId,source);
            for(const [items,kind] of [[source.Supplies,'supply'],[source.Requirements,'requirement']]){
                const ids=new Set();
                for(const item of items){if(!item||typeof item.Id!=='string'||ids.has(item.Id)||!positive(item.Quantity)||typeof item.Kind!=='string'||typeof item.Unit!=='string')fail();ids.add(item.Id);
                    if(kind==='supply'){
                        if(item.Area!=null&&(world.Space.Unit!=='meters'||!Array.isArray(item.Area.Vertices)||item.Area.Vertices.length<3||item.Area.Vertices.length>256||
                            item.Area.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y))))fail();
                        limits.set(`source:${source.NodeId}/${item.Id}`,item.Quantity);
                    }}
            }
        }
        for(const link of plan.Definition.Links){
            if(!graph.has(link?.ConnectionId)||links.has(link.ConnectionId)||!positive(link.Capacity))fail();
            links.set(link.ConnectionId,link);limits.set(`link:${link.ConnectionId}`,link.Capacity);
        }
        const capacityDefinitions=plan.Definition.CorridorCapacities||[],segmentLimits=new Map(),capacityGroups=new Map(),routeIds=new Map();
        if(!Array.isArray(capacityDefinitions)||capacityDefinitions.length>16||!Array.isArray(plan.SegmentCapacities||[])||(plan.SegmentCapacities||[]).length>100000)fail();
        const resolvedSegments=new Map();
        for(const c of plan.SegmentCapacities||[]){if(!c||typeof c.SegmentId!=='string'||resolvedSegments.has(c.SegmentId))fail();resolvedSegments.set(c.SegmentId,c);}
        for(const r of world.RoutePlan?.Routes||[])routeIds.set(r.ConnectionId,{Forward:r.Forward.Id,Reverse:r.Reverse?.Id});
        for(const n of world.NetworkPlan?.Networks||[])for(const e of n.Plan.Edges)routeIds.set(e.Link.Id,{Forward:e.Forward.Id,Reverse:e.Reverse.Id});
        const seenGroups=new Set();
        for(const d of capacityDefinitions){
            const group=world.CorridorPlan?.Groups?.find(g=>g.Definition.Id===d?.GroupId);
            if(!group||seenGroups.has(d.GroupId)||!positive(d.CapacityPerSegment)||typeof d.Unit!=='string'||!Array.isArray(group.Segments)||
                group.Segments.length>16384||!Array.isArray(group.Journeys)||group.Journeys.length>128)fail();
            seenGroups.add(d.GroupId);
            for(const s of group.Segments){
                const c=resolvedSegments.get(s.Id);
                if(!c||segmentLimits.has(s.Id)||c.GroupId!==d.GroupId||c.Capacity!==d.CapacityPerSegment||c.Unit!==d.Unit||c.Purpose!==d.Purpose||c.SourceRuleId!==d.SourceRuleId)fail();
                segmentLimits.set(s.Id,c);limits.set(`segment:${s.Id}`,c.Capacity);
            }
            for(const id of new Set(group.Journeys.map(j=>j.ConnectionId))){if(capacityGroups.has(id))fail();capacityGroups.set(id,group);}
        }
        if(segmentLimits.size!==(plan.SegmentCapacities||[]).length)fail();
        for(const allocation of plan.Allocations){
            const requirement=sources.get(allocation?.ConsumerNodeId)?.Requirements.find(r=>r.Id===allocation.RequirementId);
            const supply=sources.get(allocation?.ProviderNodeId)?.Supplies.find(s=>s.Id===allocation.SupplyId);
            const key=JSON.stringify([allocation?.ConsumerNodeId,allocation?.RequirementId]);
            const bindings=plan.Definition.Bindings.filter(b=>b.ConsumerNodeId===allocation.ConsumerNodeId&&b.RequirementId===allocation.RequirementId);
            if(!requirement||!supply||bound.has(key)||bindings.length!==1||bindings[0].ProviderNodeId!==allocation.ProviderNodeId||bindings[0].SupplyId!==allocation.SupplyId||
                allocation.Quantity!==requirement.Quantity||allocation.Kind!==requirement.Kind||allocation.Kind!==supply.Kind||allocation.Unit!==requirement.Unit||allocation.Unit!==supply.Unit||
                allocation.Physical!==requirement.RequirePhysicalRoute||Boolean(allocation.AreaContained)!==Boolean(requirement.SourceContainsSite)||!Array.isArray(allocation.ConnectionIds)||
                (requirement.SourceContainsSite?(allocation.ConnectionIds.length!==0||allocation.Physical||!supply.Area):allocation.ConnectionIds.length===0)||
                JSON.stringify(allocation.ConnectionIds)!==JSON.stringify(bindings[0].ConnectionIds)||
                (allocation.Physical?(!Number.isFinite(allocation.Distance)||allocation.Distance<0||requirement.MaximumDistance!=null&&allocation.Distance>requirement.MaximumDistance+1e-8):allocation.Distance!==null))fail();
            bound.add(key);let current=allocation.ProviderNodeId;const visited=new Set([current]),expectedSegments=new Map();
            const reserve=id=>usage.set(id,(usage.get(id)||0)+allocation.Quantity);
            reserve(`source:${allocation.ProviderNodeId}/${allocation.SupplyId}`);
            for(const id of allocation.ConnectionIds){
                const edge=graph.get(id),link=links.get(id);
                if(!edge||!link||edge.Kind!==requirement.ConnectionKind||link.Unit!==allocation.Unit)fail();
                const forward=current===edge.FromId,group=capacityGroups.get(id);
                if(group){
                    if(!allocation.Physical)fail();
                    const journeyId=routeIds.get(id)?.[forward?'Forward':'Reverse'];
                    const journey=group.Journeys.find(j=>j.Id===journeyId&&j.ConnectionId===id);
                    if(!journey||!Array.isArray(journey.Steps)||journey.Steps.length>16384)fail();
                    for(const step of journey.Steps){const limit=segmentLimits.get(step.SegmentId);if(!limit||limit.GroupId!==group.Definition.Id||limit.Unit!==allocation.Unit)fail();
                        expectedSegments.set(step.SegmentId,(expectedSegments.get(step.SegmentId)||0)+1);}
                }
                if(current===edge.FromId)current=edge.ToId;else if(edge.Bidirectional&&current===edge.ToId)current=edge.FromId;else fail();
                if(visited.has(current))fail();visited.add(current);reserve(`link:${id}`);
            }
            if(!requirement.SourceContainsSite&&current!==allocation.ConsumerNodeId)fail();
            const uses=allocation.SegmentUses||[];
            if(!Array.isArray(uses)||uses.length!==expectedSegments.size||new Set(uses.map(u=>u.SegmentId)).size!==uses.length)fail();
            for(const use of uses){
                const count=expectedSegments.get(use.SegmentId),limit=segmentLimits.get(use.SegmentId);
                if(!limit||use.GroupId!==limit.GroupId||use.Traversals!==count||!Number.isSafeInteger(count)||count<1||!close(use.Quantity,allocation.Quantity*count))fail();
                const id=`segment:${use.SegmentId}`;usage.set(id,(usage.get(id)||0)+use.Quantity);
            }
        }
        if(bound.size!==plan.Definition.Bindings.length)fail();
        for(const source of sources.values())for(const r of source.Requirements)if(!bound.has(JSON.stringify([source.NodeId,r.Id])))fail();
        if(Object.keys(plan.Usage).length!==limits.size)fail();
        for(const [id,limit] of limits){const used=usage.get(id)||0;if(used>limit+1e-8||!close(plan.Usage[id],used))fail();}
        const expected=plan.Allocations.filter(a=>nodes.get(a.ConsumerNodeId)?.StructureKey!=null),evaluations=plan.PolicyEvaluations||[];
        if(!Array.isArray(evaluations)||evaluations.length!==expected.length)fail();
        for(const allocation of expected){
            const checks=evaluations.filter(p=>p.ConsumerNodeId===allocation.ConsumerNodeId&&p.RequirementId===allocation.RequirementId);
            const structure=world.StructureDefinitions?.[nodes.get(allocation.ConsumerNodeId).StructureKey];
            if(checks.length!==1||checks[0].PolicyId!==structure?.Catalog?.PolicyId||checks[0].SourceId!==`${allocation.ProviderNodeId}/${allocation.SupplyId}`)fail();
        }
    }

    static progression(world,nodes) {        const plan=world.ProgressionPlan;        if(plan==null)return;        const fail=()=>{throw new Error('Invalid progression witness. Regenerate with the engine.');};        const text=v=>typeof v==='string'&&v.trim().length>0;        const list=(v,min,max)=>Array.isArray(v)&&v.length>=min&&v.length<=max&&v.every(text)&&new Set(v).size===v.length;        if(plan.Version!==1||!Array.isArray(plan.Rules)||plan.Rules.length<1||plan.Rules.length>32||            !Array.isArray(plan.Results)||plan.Results.length!==plan.Rules.length||!Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>50000000)fail();        const links=new Map(world.Connections.map(l=>[l.Id,l])),seen=new Set();let total=0;        for(let i=0;i<plan.Rules.length;i++){            const rule=plan.Rules[i],result=plan.Results[i];            if(!rule||!text(rule.Id)||seen.has(rule.Id)||!text(rule.Purpose)||!text(rule.SourceRuleId)||!nodes.has(rule.ScopeNodeId)||!nodes.has(rule.StartNodeId)||                !list(rule.GoalNodeIds,1,64)||!list(rule.ConnectionKinds,1,32)||!list(rule.RequiredMilestones,0,32)||                new Set([...rule.GoalNodeIds,...rule.RequiredMilestones,rule.StartNodeId]).size!==rule.GoalNodeIds.length+rule.RequiredMilestones.length+1||                !Number.isSafeInteger(rule.MaximumWork)||rule.MaximumWork<1||rule.MaximumWork>10000000||                !result||result.RuleId!==rule.Id||!Array.isArray(result.Paths)||result.Paths.length!==rule.GoalNodeIds.length||                !Number.isSafeInteger(result.Work)||result.Work<1||result.Work>rule.MaximumWork)fail();            seen.add(rule.Id);total+=result.Work;            const inScope=id=>{for(let depth=0;depth<=32;depth++){if(id===rule.ScopeNodeId)return true;id=nodes.get(id)?.ParentId;if(id==null)return false;}return false;};            if(![rule.StartNodeId,...rule.GoalNodeIds,...rule.RequiredMilestones].every(id=>nodes.has(id)&&inScope(id)))fail();            for(let j=0;j<result.Paths.length;j++){                const path=result.Paths[j];                if(!path||path.GoalNodeId!==rule.GoalNodeIds[j]||!list(path.NodeIds,2,nodes.size)||!list(path.ConnectionIds,1,links.size)||                    path.ConnectionIds.length!==path.NodeIds.length-1||path.NodeIds[0]!==rule.StartNodeId||path.NodeIds.at(-1)!==path.GoalNodeId||                    !path.NodeIds.every(id=>nodes.has(id)&&inScope(id)))fail();                let previous=0;                for(const milestone of rule.RequiredMilestones){const index=path.NodeIds.indexOf(milestone);if(index<=previous)fail();previous=index;}                for(let k=0;k<path.ConnectionIds.length;k++){                    const link=links.get(path.ConnectionIds[k]),from=path.NodeIds[k],to=path.NodeIds[k+1];                    if(!link||!rule.ConnectionKinds.includes(link.Kind)||!(link.FromId===from&&link.ToId===to||link.Bidirectional&&link.FromId===to&&link.ToId===from))fail();                }            }        }        if(total!==plan.Work)fail();        // C# owns all-path dominance and reachability. This only checks the saved display witness.    }    static variation(world,nodes) {
        const plan=world.VariationPlan;
        const fail=()=>{throw new Error('Invalid catalog variation record. Regenerate with the engine.');};
        if(plan==null){if([...nodes.values()].some(n=>n.VariationKey!=null))fail();return;}
        if(plan.Version!==1||!Array.isArray(plan.Allocations)||!plan.Allocations.length||plan.Allocations.length>4096)fail();
        const keys=new Set(),members=new Set(),text=v=>typeof v==='string'&&v.trim().length>0;
        for(const allocation of plan.Allocations){
            const r=allocation?.Request,rule=r?.Rule;
            if(!r||!rule||!text(rule.Id)||!text(rule.Seed)||rule.Seed.length>128||!text(rule.Purpose)||!text(rule.SourceRuleId)||
                !Array.isArray(rule.ArchetypeIds)||!rule.ArchetypeIds.length||rule.ArchetypeIds.length>32||!rule.ArchetypeIds.every(text)||
                new Set(rule.ArchetypeIds).size!==rule.ArchetypeIds.length||!rule.ArchetypeIds.includes(r.BaseArchetypeId)||
                !nodes.has(r.ParentNodeId)||nodes.get(r.ParentNodeId).ArchetypeId!==r.ParentArchetypeId||!text(r.PartId)||!text(r.Kind)||
                !Number.isInteger(r.Count)||r.Count<0||r.Count>100000||allocation.Key!==`${r.ParentNodeId}/variation/${r.PartId}`||keys.has(allocation.Key)||
                !Array.isArray(allocation.Choices)||allocation.Choices.length!==r.Count)fail();
            keys.add(allocation.Key);
            const prefix=`${r.ParentNodeId}/${r.PartId}-`;
            const direct=[...nodes.values()].filter(n=>n.ParentId===r.ParentNodeId&&n.Id.startsWith(prefix)&&/^\d+$/.test(n.Id.slice(prefix.length)));
            if(direct.length!==r.Count)fail();
            for(let i=0;i<r.Count;i++){
                const choice=allocation.Choices[i],node=nodes.get(prefix+i);
                if(!choice||!node||choice.NodeId!==node.Id||node.ArchetypeId!==choice.ArchetypeId||!rule.ArchetypeIds.includes(choice.ArchetypeId)||
                    node.Kind!==r.Kind||node.VariationKey!==allocation.Key||members.has(node.Id)||!/^[a-f0-9]{64}$/.test(choice.ScoreSha256))fail();
                members.add(node.Id);
            }
        }
        if([...nodes.values()].some(n=>n.VariationKey!=null&&!members.has(n.Id)))fail();
        // C# owns deterministic hash replay; the browser checks the saved display receipt.
    }
    static protectedSpaces(world,nodes) {
        const plan=world.ContextPlan,defined=plan?.Definition?.Protections,saved=plan?.Protections;
        const fail=()=>{throw new Error('Invalid protected gameplay space. Regenerate with the engine.');};
        if(defined==null){if(saved!=null)fail();return [];}
        if(!Array.isArray(defined)||defined.length<1||defined.length>64||!Array.isArray(saved)||saved.length!==defined.length)fail();
        const finite=v=>Number.isFinite(v)&&Math.abs(v)<=1e7;
        const point=p=>p&&finite(p.X)&&finite(p.Y);
        const polygon=p=>Array.isArray(p?.Vertices)&&p.Vertices.length>=3&&p.Vertices.length<=256&&p.Vertices.every(point);
        const text=v=>typeof v==='string'&&v.trim().length>0;
        const origin=id=>{const p=nodes.get(id)?.Position;if(!Array.isArray(p)||p.length!==2||!p.every(finite))fail();return {X:p[0],Y:p[1]};};
        const anchor=a=>{if(!a||!point(a.Offset))fail();const p=origin(a.NodeId);return {X:p.X+a.Offset.X,Y:p.Y+a.Offset.Y};};
        const same=(a,b)=>JSON.stringify(a)===JSON.stringify(b);
        const ids=new Set((plan.Definition.Exclusions||[]).map(e=>e.Id));
        for(const rule of defined){
            if(!rule||!text(rule.Id)||ids.has(rule.Id)||!text(rule.Kind)||!text(rule.Purpose)||!text(rule.SourceRuleId)||
                !Array.isArray(rule.Tags)||!rule.Tags.length||rule.Tags.length>64||!rule.Tags.every(text)||new Set(rule.Tags).size!==rule.Tags.length||
                (rule.Area==null)===(rule.Sightline==null)||rule.Sightline!=null&&rule.RelativeToNodeId!=null)fail();
            ids.add(rule.Id);
            const matches=saved.filter(p=>p?.Definition?.Id===rule.Id);if(matches.length!==1)fail();
            const item=matches[0];if(!same(rule,item.Definition)||!polygon(item.Boundary))fail();
            if(rule.Area!=null){
                if(!polygon(rule.Area)||item.Centerline!=null)fail();
                const p=rule.RelativeToNodeId==null?{X:0,Y:0}:origin(rule.RelativeToNodeId);
                const expected=rule.Area.Vertices.map(v=>({X:v.X+p.X,Y:v.Y+p.Y}));
                if(!same(expected,item.Boundary.Vertices))fail();
            }else{
                const line=rule.Sightline;
                if(!Number.isFinite(line.Width)||line.Width<=0||line.Width>1e6)fail();
                const a=anchor(line.From),b=anchor(line.To);
                if(Math.hypot(b.X-a.X,b.Y-a.Y)<=1e-9||!same(item.Centerline?.Points,[a,b]))fail();
            }
        }
        // Display exact saved envelopes. C# owns collision checks and authoritative geometry replay.
        return saved;
    }
    static contextEvaluations(world,nodes) {
        const plan=world.ContextPlan,feedback=world.PlacementPlan?.Feedback?.Context;
        const fail=()=>{throw new Error('Invalid region context record. Regenerate with the engine.');};
        if(plan==null){if(feedback!=null)fail();return;}
        if(world.Space.Unit!=='meters'||world.Space.Axes.length!==2||!world.Surface||!world.PlacementPlan||
            JSON.stringify(plan.Definition)!==JSON.stringify(feedback)||!Array.isArray(plan.Sources)||!Array.isArray(plan.Evaluations)||
            !Number.isSafeInteger(plan.Work)||plan.Work<1||plan.Work>plan.Definition.MaximumWork)fail();
        const protections=WorldSchematic.protectedSpaces(world,nodes);
        const exclusions=[...(plan.Definition.Exclusions||[]),...protections.map(p=>({Id:p.Definition.Id,Tags:p.Definition.Tags,SourceRuleId:p.Definition.SourceRuleId,Boundary:p.Boundary}))];
        const regions=new Map(),sources=new Map(),expected=[];
        for(const r of plan.Definition.Regions){
            if(!r||typeof r.Id!=='string'||regions.has(r.Id)||typeof r.Use!=='string'||typeof r.SourceRuleId!=='string'||
                !Array.isArray(r.Boundary?.Vertices)||r.Boundary.Vertices.length<3||r.Boundary.Vertices.length>256||
                r.Boundary.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)))fail();
            regions.set(r.Id,r);
        }
        const placed=[...world.PlacementPlan.Definition.Bindings,...(world.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].map(b=>b.NodeId);
        if(plan.Sources.length!==placed.length||plan.Definition.Sites.length!==placed.length)fail();
        for(const source of plan.Sources){
            const node=nodes.get(source.NodeId),binding=plan.Definition.Sites.filter(b=>b.NodeId===source.NodeId),region=regions.get(source.RegionId);
            if(!node||sources.has(source.NodeId)||!placed.includes(source.NodeId)||binding.length!==1||binding[0].RegionId!==source.RegionId||!region||!Array.isArray(source.Tags))fail();
            const registered=node.StructureKey==null?null:world.StructureDefinitions?.[node.StructureKey];
            const tags=registered?.Definition?.SitingProfile?.Tags??binding[0].Tags;
            if(!Array.isArray(tags)||JSON.stringify([...tags].sort())!==JSON.stringify(source.Tags)||
                (registered?(binding[0].Tags!=null||source.PolicyId!==registered.Catalog.PolicyId||!registered.Definition.SitingProfile.AllowedDistricts.includes(region.Use)):source.PolicyId!=null))fail();
            sources.set(source.NodeId,source);
            const zoneIds=new Set();
            for(const zone of exclusions){
                if(!zone||typeof zone.Id!=='string'||zoneIds.has(zone.Id)||typeof zone.SourceRuleId!=='string'||!Array.isArray(zone.Tags)||zone.Tags.length===0||
                    !Array.isArray(zone.Boundary?.Vertices)||zone.Boundary.Vertices.length<3||zone.Boundary.Vertices.length>256||
                    zone.Boundary.Vertices.some(p=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)))fail();
                zoneIds.add(zone.Id);
                if(source.Tags.some(t=>zone.Tags.includes(t)))expected.push({SiteId:source.NodeId,RuleId:`exclusion:${zone.Id}`,RelatedId:zone.Id,Spatial:true,SourceRuleId:zone.SourceRuleId,Distance:null,Minimum:null});
            }
            expected.push({SiteId:source.NodeId,RuleId:'region-use',RelatedId:region.Id,Spatial:false,SourceRuleId:region.SourceRuleId,Distance:null,Minimum:null},
                {SiteId:source.NodeId,RuleId:'region-containment',RelatedId:region.Id,Spatial:true,SourceRuleId:region.SourceRuleId,Distance:null,Minimum:null});
        }
        const ordered=[...sources.values()].sort((a,b)=>a.NodeId<b.NodeId?-1:a.NodeId>b.NodeId?1:0);
        for(let i=0;i<ordered.length;i++)for(let j=0;j<i;j++){
            const a=ordered[i],b=ordered[j];
            for(const rule of plan.Definition.Separations){
                if(!Number.isFinite(rule.MinimumMeters)||rule.MinimumMeters<0)fail();
                if(a.Tags.includes(rule.FirstTag)&&b.Tags.includes(rule.SecondTag)||a.Tags.includes(rule.SecondTag)&&b.Tags.includes(rule.FirstTag))
                    expected.push({SiteId:a.NodeId,RuleId:`separation:${rule.Id}`,RelatedId:b.NodeId,Spatial:true,SourceRuleId:rule.SourceId,Minimum:rule.MinimumMeters});
            }
        }
        if(plan.Evaluations.length!==expected.length)fail();
        for(const e of expected){
            const records=plan.Evaluations.filter(r=>r.SiteId===e.SiteId&&r.RuleId===e.RuleId&&r.RelatedId===e.RelatedId);
            if(records.length!==1)fail();const actual=records[0];
            if(actual.Passed!==true||actual.Spatial!==e.Spatial||actual.SourceRuleId!==e.SourceRuleId||actual.Minimum!==e.Minimum||
                (e.Minimum===null?actual.Distance!==null:!Number.isFinite(actual.Distance)||actual.Distance+1e-8<e.Minimum))fail();
        }
    }

    static placementFeedback(world) {
        const plan=world.PlacementPlan;if(!plan)return null;
        const attempts=plan.FeasibilityAttempts,feedback=plan.Feedback;
        const required=world.AccessPlan!=null||world.RoutePlan!=null||world.NetworkPlan!=null||world.ServicePlan!=null||world.ContextPlan!=null;
        if(!Array.isArray(attempts)||!Number.isSafeInteger(plan.Work)||plan.Work<0||
            !Number.isSafeInteger(plan.Definition?.MaximumFeasibilityAttempts)||plan.Definition.MaximumFeasibilityAttempts<1||
            plan.Definition.MaximumFeasibilityAttempts>256||attempts.length>plan.Definition.MaximumFeasibilityAttempts||
            required!==(feedback!=null)||(!feedback&&attempts.length)||(feedback&&attempts.length===0))
            throw new Error('Invalid placement feasibility record. Regenerate with the engine.');
        let work=0;
        for(const [index,attempt] of attempts.entries()){
            if(!attempt||attempt.Ordinal!==index+1||typeof attempt.CandidateSha256!=='string'||! /^[a-f0-9]{64}$/.test(attempt.CandidateSha256)||
                attempt.Accepted!==(index===attempts.length-1)||!Number.isSafeInteger(attempt.Work)||attempt.Work<0||
                (attempt.Accepted?attempt.Rejection!==null:typeof attempt.Rejection!=='string'||!attempt.Rejection.startsWith('METADATA_')))
                throw new Error('Invalid placement feasibility attempt.');
            work+=attempt.Work;
        }
        if(!Number.isSafeInteger(work)||work>plan.Work)throw new Error('Invalid placement feasibility work.');
        return feedback?{attempts,work,totalWork:plan.Work}:null;
    }

    static physicalAccess(world,nodes) {
        const plan=world.AccessPlan,result=new Map();if(plan==null)return result;
        if(!plan.Definitions||!Array.isArray(plan.Sites)||plan.Sites.length>1024||!world.PlacementPlan)throw new Error('Invalid physical access plan.');
        const placed=new Map(),stored=world.PlacementPlan;
        for(const stage of [{Placement:stored.Placement,Bindings:stored.Definition.Bindings},...stored.NestedStages.map(s=>({...s,Bindings:stored.Definition.NestedStages.find(d=>d.Id===s.Id)?.Bindings}))]){
            if(!Array.isArray(stage.Bindings))throw new Error('Missing physical stage.');
            for(const b of stage.Bindings)placed.set(b.NodeId,{binding:b,site:stage.Placement.Sites.find(s=>s.DemandId===b.DemandId)});
        }
        if(placed.size!==plan.Sites.length)throw new Error('Incomplete occupancy classification.');
        for(const site of plan.Sites){
            const node=nodes.get(site?.NodeId),source=plan.Definitions[node?.ArchetypeId],location=placed.get(site?.NodeId);
            if(!node||!source||!location||result.has(site.NodeId)||!['Reservation','Solid'].includes(site.Occupancy)||site.Occupancy!==source.Occupancy||
                JSON.stringify(site.Footprint)!==JSON.stringify(location.site?.Footprint?.Footprint)||!Array.isArray(site.Ports)||!Array.isArray(source.Ports)||
                site.Ports.length>16||site.Ports.length!==source.Ports.length)throw new Error('Invalid physical occupancy.');
            const ids=new Set(),a=location.binding.RotationDegrees*Math.PI/180,c=Math.cos(a),s=Math.sin(a),center=location.site.Position;
            for(const port of site.Ports){
                const p=source.Ports.find(p=>p.Id===port?.Id);
                if(!p||ids.has(port.Id)||!Number.isFinite(port.Width)||port.Width<=0||port.Width!==p.Width)throw new Error('Invalid access port.');
                ids.add(port.Id);
                for(const key of ['BoundaryPosition','ApproachPosition']){
                    const point=port[key],local=p[key];
                    if(!point||!local||![point.X,point.Y,local.X,local.Y].every(Number.isFinite)||
                        Math.abs(point.X-(local.X*c-local.Y*s+center.X))>1e-8||Math.abs(point.Y-(local.X*s+local.Y*c+center.Y))>1e-8)throw new Error('Moved access port.');
                }
            }
            result.set(site.NodeId,site);
        }
        return result;
    }

    static placementObservations(world,nodes) {
        const stored=world.PlacementPlan;if(stored==null)return new Map();
        const declared=stored.Definition?.NestedStages||[],nested=stored.NestedStages;
        if(!Array.isArray(nested)||nested.length>128||!Array.isArray(declared)||declared.length!==nested.length||!world.EnvironmentPlan)
            throw new Error('Invalid placement stages.');
        const definitions=new Map(declared.map(s=>[s.Id,s]));
        if(definitions.size!==declared.length)throw new Error('Duplicate placement stages.');
        const stages=[{Id:stored.Definition.Request.Id,ParentNodeId:null,Placement:stored.Placement,Bindings:stored.Definition.Bindings,Footprints:stored.Footprints}];
        for(const stage of nested){
            const definition=definitions.get(stage.Id);
            if(!definition||stage.ParentNodeId!==definition.ParentNodeId)throw new Error('Invalid placement parent.');
            definitions.delete(stage.Id);stages.push({...stage,Bindings:definition.Bindings});
        }
        const byNode=new Map(),features=stored.SiteFeatures??[],featureIds=new Set();
        if(!Array.isArray(features)||features.length>68112)throw new Error('Invalid site surroundings.');
        for(const feature of features)if(!feature||typeof feature.Id!=='string'||featureIds.has(feature.Id))throw new Error('Invalid site feature identity.');else featureIds.add(feature.Id);
        for(const stage of stages){
            const plan=stage.Placement,bindings=stage.Bindings,demands=plan?.Request?.Demands,sources=stage.Footprints;
            if(plan?.Status!=='Found'||!Array.isArray(bindings)||bindings.length<1||bindings.length>16||
                !Array.isArray(demands)||demands.length!==bindings.length||!Array.isArray(plan.Sites)||plan.Sites.length!==bindings.length||
                !Array.isArray(plan.Diagnostics)||plan.Diagnostics.length!==bindings.length||!Array.isArray(sources)||sources.length!==bindings.length||
                (stage.ParentNodeId!==null&&!byNode.has(stage.ParentNodeId)))throw new Error('Invalid placement plan.');
            const rules=[],observations=[],reservationRules=[],reservationObservations=[],seen=new Set();
            for(const binding of bindings){
                const demand=demands.find(d=>d.Id===binding.DemandId),site=plan.Sites.find(s=>s.DemandId===binding.DemandId);
                const diagnostic=plan.Diagnostics.find(d=>d.DemandId===binding.DemandId),source=sources.find(s=>s.NodeId===binding.NodeId),node=nodes.get(binding.NodeId);
                if(!demand||!site||!node||byNode.has(node.Id)||seen.has(binding.DemandId)||!site.Position||
                    (stage.ParentNodeId!==null&&node.ParentId!==stage.ParentNodeId)||!source||source.Unit!==world.Space.Unit||
                    JSON.stringify(source.Polygon)!==JSON.stringify(demand.Footprint)||typeof source.SourceId!=='string'||
                    JSON.stringify(source.Reservation??null)!==JSON.stringify(demand.Reservation??null)||
                    (demand.Reservation!=null)!==(site.Reservation!=null)||
                    site.Position.X!==site.Point?.Observation?.Surface?.Position?.X||site.Position.Y!==site.Point?.Observation?.Surface?.Position?.Y||
                    typeof site.CandidateSource!=='string'||!diagnostic||!Number.isInteger(diagnostic.Candidates)||!Number.isInteger(diagnostic.Eligible)||
                    diagnostic.Eligible<1||diagnostic.Eligible>diagnostic.Candidates)throw new Error('Invalid placement binding.');
                seen.add(binding.DemandId);
                const sourceFeatures=source.SiteFeatures??[],siteFeatures=features.filter(f=>f.NodeId===binding.NodeId);
                if(!Array.isArray(sourceFeatures)||sourceFeatures.length>129||siteFeatures.length!==sourceFeatures.length||
                    (binding.SiteLayout!=null)!==(source.SiteFeatures!=null))throw new Error('Invalid site feature coverage.');
                for(const feature of sourceFeatures){
                    const placed=siteFeatures.find(f=>f.Id===feature.Id),vertices=feature.Boundary?.Vertices;
                    if(!placed||!featureIds.delete(placed.Id)||feature.NodeId!==binding.NodeId||!Array.isArray(vertices)||vertices.length!==4||
                        !Array.isArray(placed.Boundary?.Vertices)||placed.Boundary.Vertices.length!==4||
                        ['Kind','SourceRuleId','Purpose','EntrancePortId','SpeciesAssetId','PlantSpacingMeters','PlantMarginMeters'].some(k=>placed[k]!==feature[k])||
                        vertices.some((p,i)=>!Number.isFinite(p.X)||!Number.isFinite(p.Y)||placed.Boundary.Vertices[i].X!==p.X+site.Position.X||placed.Boundary.Vertices[i].Y!==p.Y+site.Position.Y))
                        throw new Error('Invalid site feature geometry or provenance.');
                }
                const id=`placement/${stage.Id}/${demand.Id}`;
                rules.push({Id:id,Profile:demand.Profile,Footprint:demand.Footprint,FootprintWorkBudget:plan.Request.MaximumWork,
                    RequireEligible:true,MinimumNodes:1,MaximumNodes:1});
                observations.push({RuleId:id,NodeId:binding.NodeId,Result:site.Point,Footprint:site.Footprint,
                    Placement:{Stage:stage.Id,ParentNodeId:stage.ParentNodeId,RegionId:demand.RegionId,AllocationId:demand.AllocationId,Purpose:demand.Purpose,SourceRuleId:demand.SourceRuleId,CandidateSource:site.CandidateSource,
                        FootprintSource:source,SiteFeatures:siteFeatures,Candidates:diagnostic.Candidates,EligibleCandidates:diagnostic.Eligible,Rejections:diagnostic.Rejections}});
                if(demand.Reservation){
                    const reservationId=`reservation/${stage.Id}/${demand.Id}`;
                    reservationRules.push({...rules.at(-1),Id:reservationId,Footprint:demand.Reservation});
                    reservationObservations.push({...observations.at(-1),RuleId:reservationId,Footprint:site.Reservation,
                        Placement:{...observations.at(-1).Placement,IsReservation:true}});
                }
            }
            for(const [nodeId,entries] of WorldSchematic.environmentObservations({...world,EnvironmentPlan:{Definition:world.EnvironmentPlan.Definition,Rules:rules,Observations:observations}},nodes))byNode.set(nodeId,entries);
            if(reservationRules.length)for(const [nodeId,entries] of WorldSchematic.environmentObservations({...world,EnvironmentPlan:{Definition:world.EnvironmentPlan.Definition,Rules:reservationRules,Observations:reservationObservations}},nodes))byNode.get(nodeId).push(...entries);
        }
        if(featureIds.size)throw new Error('Unbound site features.');
        return byNode;
    }

    static environmentObservations(world,nodes) {
        const byNode=new Map(),plan=world.EnvironmentPlan;if(plan==null)return byNode;
        const fields=plan.Definition?.Fields;
        if(plan.Definition?.SchemaVersion!==1||!Array.isArray(fields)||fields.length<1||fields.length>32||
            !Array.isArray(plan.Rules)||plan.Rules.length>16||!Array.isArray(plan.Observations)||plan.Observations.length>4096)
            throw new Error('Invalid environmental plan.');
        const fieldIds=new Set(fields.map(f=>f.Id)),rules=new Map(plan.Rules.map(r=>[r.Id,r])),seen=new Set(),counts=new Map();
        if(fieldIds.size!==fields.length||rules.size!==plan.Rules.length)throw new Error('Duplicate environment definitions.');
        for(const entry of plan.Observations){
            const rule=rules.get(entry.RuleId),node=nodes.get(entry.NodeId),result=entry.Result,observation=result?.Observation,position=observation?.Surface?.Position;
            const key=`${entry.RuleId}\n${entry.NodeId}`,values=observation?.Values;
            if(!rule||!node||seen.has(key)||result?.ProfileId!==rule.Profile?.Id||typeof result.Eligible!=='boolean'||
                !Array.isArray(node.Position)||!position||position.X!==node.Position[0]||position.Y!==node.Position[1]||
                !values||typeof values!=='object'||Array.isArray(values)||Object.keys(values).some(k=>!fieldIds.has(k)||!Number.isFinite(values[k]))||
                (result.Eligible?(!Number.isFinite(result.Score)||result.Score<0||result.Score>1):result.Score!==null)||
                !Array.isArray(result.Rejections)||result.Rejections.some(r=>typeof r!=='string')||
                (result.Eligible&&result.Rejections.length!==0)||(!result.Eligible&&result.Rejections.length===0)||
                !Array.isArray(result.Decisions)||result.Decisions.length>64||(rule.RequireEligible&&!result.Eligible))
                throw new Error('Invalid point suitability observation.');
            if(observation.Surface.Status==='Sampled'&&Object.keys(values).length!==fields.length)throw new Error('Incomplete environmental values.');
            const area=entry.Footprint;
            if(rule.Footprint!=null){
                const vertices=area?.Footprint?.Vertices,local=rule.Footprint.Vertices;
                if(!area||area.ProfileId!==result.ProfileId||typeof area.Eligible!=='boolean'||typeof area.CompleteCoverage!=='boolean'||
                    !Array.isArray(vertices)||vertices.length<3||vertices.length>64||!Array.isArray(local)||local.length!==vertices.length||
                    vertices.some((p,i)=>!p||!local[i]||!Number.isFinite(p.X)||!Number.isFinite(p.Y)||Math.abs(p.X)>10000000||Math.abs(p.Y)>10000000||p.X!==node.Position[0]+local[i].X||p.Y!==node.Position[1]+local[i].Y)||
                    !area.Ranges||typeof area.Ranges!=='object'||Array.isArray(area.Ranges)||Object.entries(area.Ranges).some(([id,r])=>!fieldIds.has(id)||!r||!Number.isFinite(r.Minimum)||!Number.isFinite(r.Maximum)||r.Minimum>r.Maximum)||
                    (area.CompleteCoverage&&Object.keys(area.Ranges).length!==fields.length)||
                    !Array.isArray(area.Rejections)||area.Rejections.some(r=>typeof r!=='string')||area.Eligible!==(area.Rejections.length===0)||
                    (area.Eligible&&!area.CompleteCoverage)||!Array.isArray(area.Decisions)||area.Decisions.length!==rule.Profile.Requirements.length||
                    !Number.isSafeInteger(area.Work)||area.Work<1||area.Work>rule.FootprintWorkBudget||
                    !Number.isSafeInteger(area.Regions)||area.Regions<1||!Number.isSafeInteger(area.Pieces)||area.Pieces<0||
                    (rule.RequireEligible&&!area.Eligible))throw new Error('Invalid whole-footprint observation.');
            }else if(area!=null)throw new Error('Undeclared whole-footprint observation.');
            seen.add(key);counts.set(rule.Id,(counts.get(rule.Id)||0)+1);
            if(!byNode.has(node.Id))byNode.set(node.Id,[]);byNode.get(node.Id).push(entry);
        }
        for(const rule of plan.Rules)if((counts.get(rule.Id)||0)<rule.MinimumNodes||(counts.get(rule.Id)||0)>rule.MaximumNodes)
            throw new Error('Point suitability count mismatch.');
        return byNode;
    }

    static heightField(f,unit) {
        if(!f||f.SchemaVersion!==1||f.Unit!==unit||typeof f.Id!=='string'||typeof f.Kind!=='string'||
            !Number.isInteger(f.Columns)||f.Columns<2||f.Columns>513||!Number.isInteger(f.Rows)||f.Rows<2||f.Rows>513||
            ![f.StepX,f.StepY].every(n=>Number.isFinite(n)&&n>=.001)||!f.Origin||
            ![f.Origin.X,f.Origin.Y,f.Origin.X+(f.Columns-1)*f.StepX,f.Origin.Y+(f.Rows-1)*f.StepY].every(n=>Number.isFinite(n)&&Math.abs(n)<=10000000)||
            !Array.isArray(f.Heights)||f.Heights.length!==f.Columns*f.Rows||f.Heights.some(h=>!Number.isFinite(h)||Math.abs(h)>10000000))
            throw new Error('Invalid height field.');
        let minimum=Infinity,maximum=-Infinity;
        for(const h of f.Heights){minimum=Math.min(minimum,h);maximum=Math.max(maximum,h);}
        return {source:f,minimum,maximum,width:(f.Columns-1)*f.StepX,height:(f.Rows-1)*f.StepY};
    }

    // Display triangulation of the engine's samples; never runs landform/noise generation.
    static *heightTriangles(field) {
        const f=field.source,point=(x,y)=>({x:f.Origin.X+x*f.StepX,y:f.Origin.Y+y*f.StepY,height:f.Heights[y*f.Columns+x]});
        for(let y=0;y<f.Rows-1;y++)for(let x=0;x<f.Columns-1;x++){
            const a=point(x,y),b=point(x+1,y),c=point(x,y+1),d=point(x+1,y+1);
            yield {points:[a,b,d],elevation:(a.height+b.height+d.height)/3,slope:Math.hypot((b.height-a.height)/f.StepX,(d.height-b.height)/f.StepY)};
            yield {points:[a,d,c],elevation:(a.height+d.height+c.height)/3,slope:Math.hypot((d.height-c.height)/f.StepX,(c.height-a.height)/f.StepY)};
        }
    }

    constructor(svg, world, details, status) {
        this.svg = svg; this.details = details; this.status = status; this.selected = null; this.events = [];
        this.load(world);
        svg.addEventListener('wheel', event => { event.preventDefault(); this.zoom(event.deltaY < 0 ? 1.15 : 1 / 1.15); }, { passive: false });
        let drag = null;
        svg.addEventListener('pointerdown', event => { if (event.button === 0) { this.dragged = false; drag = [event.clientX, event.clientY]; } });
        svg.addEventListener('pointermove', event => {
            if (!drag) return;
            if (!this.dragged && Math.hypot(event.clientX - drag[0], event.clientY - drag[1]) < 3) return;
            this.dragged = true; svg.setPointerCapture(event.pointerId);
            const bounds = svg.getBoundingClientRect();
            this.view[0] -= (event.clientX - drag[0]) * this.view[2] / bounds.width;
            this.view[1] -= (event.clientY - drag[1]) * this.view[3] / bounds.height;
            drag = [event.clientX, event.clientY]; this.applyView();
        });
        svg.addEventListener('pointerup', () => { drag = null; });
        svg.addEventListener('pointercancel', () => { drag = null; });
    }

    element(tag, attrs = {}, text = null, parent = this.svg) {
        const el = document.createElementNS('http://www.w3.org/2000/svg', tag);
        for (const [key, value] of Object.entries(attrs)) el.setAttribute(key, String(value));
        if (text !== null) el.textContent = text;
        parent.append(el); return el;
    }

    load(world) {
        const plan = WorldSchematic.layout(world); // Validate before replacing a working diagram.
        this.plan = plan; this.selected = null; this.selectors = new Map(); this.labelNodes = new Map(); this.focusRootId = null; this.svg.replaceChildren();
        const boxesById = new Map(plan.boxes.map(box=>[box.id,box]));
        this.boxesById=boxesById;
        const childrenById = new Map();
        for(const box of plan.boxes){const parent=box.node.ParentId;if(!childrenById.has(parent))childrenById.set(parent,[]);childrenById.get(parent).push(box);}
        this.details.replaceChildren();
        const prompt = document.createElement('p'); prompt.textContent = 'Select a region, site or constituent structure to inspect its purpose and metadata.';
        this.details.append(prompt);
        this.showPlacementFeedback();
        this.showCapacity();        this.showProgression();
        this.element('title', {}, `${world.Id}: world schematic`);
        this.element('desc', {}, `Use Tab and Enter to inspect locations, or the fit and zoom controls. ${plan.coordinateSpace?'Engine world coordinates.':'Layout uses diagram units.'}`);
        const defs = this.element('defs');
        const marker = this.element('marker', { id: 'arrow', markerWidth: 8, markerHeight: 8, refX: 7, refY: 4, orient: 'auto' }, null, defs);
        this.element('path', { d: 'M0 0 L8 4 L0 8 Z', fill: '#176891' }, null, marker);
        this.groups = {};
        for (const name of ['terrain', 'regions', 'roads', 'sites', 'links', 'labels']) this.groups[name] = this.element('g', { 'data-layer': name });
        const legend=document.querySelector('.key');
        if(legend)legend.replaceChildren();
        const key=(text,color)=>{if(legend){const label=document.createElement('span');label.textContent=text;label.style.setProperty('--color',color);legend.append(label);}};
        const fieldSelector=document.querySelector('#terrain-field-view');
        if(fieldSelector){fieldSelector.disabled=!plan.heightField;fieldSelector.value='height';fieldSelector.onchange=null;}
        if(plan.boundary){
            const points=p=>p.map(v=>`${v.x},${v.y}`).join(' '),clip=this.element('clipPath',{id:'surface-boundary'},null,defs);
            this.element('polygon',{points:points(plan.boundary)},null,clip);
            this.groups.terrain.setAttribute('clip-path','url(#surface-boundary)');
            this.element('polygon',{points:points(plan.boundary),fill:'#f2ece1',stroke:'#526b64','stroke-width':.4},null,this.groups.terrain);
            if(plan.heightField){
                const field=plan.heightField,f=field.source,images={},resolution=1024;
                let maxSlope=0;for(const triangle of WorldSchematic.heightTriangles(field))maxSlope=Math.max(maxSlope,triangle.slope);
                for(const mode of ['height','slope']){
                    const canvas=document.createElement('canvas');canvas.width=resolution;canvas.height=resolution;
                    const context=canvas.getContext('2d');
                    if(!context)throw new Error('Terrain canvas unavailable.');
                    for(const triangle of WorldSchematic.heightTriangles(field)){
                        const t=mode==='height'?(triangle.elevation-field.minimum)/(field.maximum-field.minimum||1):triangle.slope/(maxSlope||1);
                        context.fillStyle=mode==='height'?`hsl(${220-t*185} 48% ${35+t*35}%)`:`hsl(${280-t*250} 65% ${88-t*48}%)`;
                        context.beginPath();triangle.points.forEach((p,i)=>context[i?'lineTo':'moveTo']((p.x-f.Origin.X)/field.width*resolution,(p.y-f.Origin.Y)/field.height*resolution));
                        context.closePath();context.fill();
                    }
                    images[mode]=canvas.toDataURL('image/png');
                }
                const image=this.element('image',{x:f.Origin.X,y:f.Origin.Y,width:field.width,height:field.height,
                    href:images.height,preserveAspectRatio:'none','data-heightfield-id':f.Id},null,this.groups.terrain);
                this.element('title',{},`${f.Purpose}; ${f.Columns} × ${f.Rows} engine samples; elevation ${field.minimum.toFixed(2)}–${field.maximum.toFixed(2)} ${plan.unit}; slope 0–${maxSlope.toFixed(3)} rise/run. Display colors are triangle means.`,image);
                key(`Height ${field.minimum.toFixed(1)}–${field.maximum.toFixed(1)} ${plan.unit} · blue → gold`,'#668b9e');
                key(`Slope 0–${maxSlope.toFixed(2)} rise/run · pale → red`,'#c85f6e');
                if(fieldSelector)fieldSelector.onchange=()=>{image.setAttribute('href',images[fieldSelector.value]);this.notify('schematic.terrain.layer',`Showing ${fieldSelector.value==='height'?'elevation':'slope'} from engine terrain samples.`);};
            }
            const kinds=[...new Set(plan.terrain.map(p=>p.kind))].sort(),colors=new Map(kinds.map((k,i)=>[k,`hsl(${(i*137+95)%360} 30% 75%)`]));
            for(const patch of plan.terrain){
                const shape=this.element('polygon',{points:points(patch.points),fill:colors.get(patch.kind),'data-terrain-id':patch.id},null,this.groups.terrain);
                this.element('title',{},`${patch.kind}: ${patch.id}\n${patch.purpose}`,shape);
            }
            for(const exclusion of plan.exclusions){
                const shape=this.element('polygon',{points:points(exclusion.points),fill:'#434750',stroke:'#f8e8db','stroke-width':.3,'data-exclusion-id':exclusion.id},null,this.groups.terrain);
                this.element('title',{},`Excluded: ${exclusion.id}\n${exclusion.purpose}`,shape);
            }
            kinds.forEach(k=>key(k,colors.get(k)));key('Excluded','#434750');key('Passage envelope','#79634c');key('Destination marker','#e5b951');
        } else {key('Regions','#e6ede7');key('Sites','#b8ccd5');key('Diagram channels','#76878b');}
        if(plan.generatedRegions){
            const regionPlan=plan.generatedRegions;
            regionPlan.Regions.forEach((region,index)=>{
                const color=`hsl(${(index*137+195)%360} 55% 48%)`,group=this.element('g',{'data-generated-region':region.Id},null,this.groups.regions);
                // One fill path without strokes on the internal terrain decomposition.
                const fill=region.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');
                const shape=this.element('path',{d:fill,fill:color,'fill-opacity':.3,'data-region-area':region.Area},null,group);
                this.element('title',{},`${region.Id}: ${region.Seed.Purpose}\n${region.Area.toFixed(2)} ${plan.unit}²; minimum ${region.Seed.MinimumArea}\n${region.Seed.SourceRuleId}`,shape);
                const outline=region.BoundaryEdges.map(e=>`M${e.From.X},${e.From.Y} L${e.To.X},${e.To.Y}`).join(' ');
                this.element('path',{d:outline,fill:'none',stroke:color,'stroke-width':2,'vector-effect':'non-scaling-stroke','data-region-outline':region.Id},null,group);
                key(`${region.Id} · ${region.Area.toFixed(1)} ${plan.unit}²`,color);
            });
            const summary=document.createElement('p');summary.dataset.regionAccounting='true';
            summary.textContent=`Generated regions: ${(regionPlan.SuitableArea-regionPlan.UnclaimedSuitableArea).toFixed(1)} ${plan.unit}² assigned; ${regionPlan.UnclaimedSuitableArea.toFixed(1)} suitable but unclaimed; ${regionPlan.IneligibleArea.toFixed(1)} outside the region rules. Region colors do not imply roads or completed content.`;
            this.details.append(summary);
        }
        if(plan.localLayouts){
            plan.localLayouts.Allocations.forEach((allocation,index)=>{
                const color=`hsl(${(index*137+25)%360} 58% 45%)`,shape=allocation.Shape;
                const group=this.element('g',{'data-local-allocation':allocation.Id},null,this.groups.regions);
                const fill=shape.Cells.map(c=>c.Vertices.map((p,i)=>`${i?'L':'M'}${p.X},${p.Y}`).join(' ')+' Z').join(' ');
                const path=this.element('path',{d:fill,fill:color,'fill-opacity':.32,'data-allocation-area':shape.Area},null,group);
                this.element('title',{},`${allocation.Id}: ${allocation.Program.Purpose}\n${shape.Area.toFixed(2)} ${plan.unit}²; minimum ${allocation.Program.MinimumArea}\n${allocation.Program.SourceRuleId}`,path);
                this.element('path',{d:shape.BoundaryEdges.map(e=>`M${e.From.X},${e.From.Y} L${e.To.X},${e.To.Y}`).join(' '),fill:'none',stroke:color,'stroke-width':1.5,'vector-effect':'non-scaling-stroke','data-allocation-outline':allocation.Id},null,group);
                key(`${allocation.Id} · ${shape.Area.toFixed(1)} ${plan.unit}²`,color);
            });
            for(const item of plan.localLayouts.Interfaces){
                const edge=item.SelectedSpan;
                const line=this.element('line',{x1:edge.From.X,y1:edge.From.Y,x2:edge.To.X,y2:edge.To.Y,stroke:'#713b81','stroke-width':3,'stroke-dasharray':'5 3','vector-effect':'non-scaling-stroke','data-local-interface':item.Id},null,this.groups.regions);
                const physical=plan.interfaceRoutes.filter(r=>r.InterfaceId===item.Id);                this.element('title',{},`${item.Requirement.Purpose}\nDeclared shared boundary. ${physical.length?'Realized crossings are shown in magenta.':'No physical passage is declared.'}`,line);
            }
            key('Required shared interface','#713b81');            if(plan.interfaceRoutes.length)key('Physical interface crossing','#c23d84');
            const summary=document.createElement('p');summary.dataset.localLayoutAccounting='true';
            summary.textContent=`Local programs: ${plan.localLayouts.Allocations.length}. ${plan.localLayouts.Allocations.filter(a=>!plan.localLayouts.Definition.Layouts.some(l=>l.ParentAllocationId===a.Id)).reduce((n,a)=>n+a.Shape.Area,0).toFixed(1)} ${plan.unit}² in terminal allocations; ${plan.localLayouts.Areas.reduce((n,a)=>n+a.ResidualArea,0).toFixed(1)} residual. Colored allocations reserve space for programs; their boundaries are not roads.`;
            this.details.append(summary);
        }
        if(plan.suitability?.size){key('Declared checks passed','#3c9669');key('Declared check rejected','#ca5964');}
        if(world.AccessPlan){key('Solid footprint','#596b86');key('Declared entrance approach','#b48029');}
        if(world.ServicePlan?.SegmentCapacities?.length){key('Passage capacity available','#237e7b');key('Passage at capacity','#9d376d');}
        for(const source of world.ServicePlan?.Sources||[])for(const supply of source.Supplies)if(supply.Area){
            const area=this.element('polygon',{points:supply.Area.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),
                fill:'#399870','fill-opacity':.08,stroke:'#399870','stroke-width':1.5,'stroke-dasharray':'3 3',
                'vector-effect':'non-scaling-stroke','pointer-events':'none','data-resource-area':`${source.NodeId}/${supply.Id}`},null,this.groups.regions);
            this.element('title',{},`Resource area: ${supply.Kind} (${supply.SourceRuleId})`,area);
        }
        if(world.ServicePlan?.Sources.some(s=>s.Supplies.some(p=>p.Area)))key('Resource area','#399870');
        if(world.ContextPlan){
            key('Context region','#8064a2');
            for(const protection of world.ContextPlan.Protections||[]){
                const rule=protection.Definition;
                const area=this.element('polygon',{points:protection.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'#e68a24','fill-opacity':.22,stroke:'#b66510','stroke-width':1.5,'stroke-dasharray':'5 3',
                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-protection-id':rule.Id},null,this.groups.regions);
                this.element('title',{},`${rule.Kind}: ${rule.Purpose}; blockers: ${rule.Tags.join(', ')} (${rule.SourceRuleId})`,area);
                if(protection.Centerline)this.element('polyline',{points:protection.Centerline.Points.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'none',stroke:'#b66510','stroke-width':1,'vector-effect':'non-scaling-stroke','pointer-events':'none',
                    'data-protection-centerline':rule.Id},null,this.groups.regions);
            }
            if(world.ContextPlan.Protections?.length)key('Protected gameplay space','#e68a24');
            for(const zone of world.ContextPlan.Definition.Exclusions||[]){
                const area=this.element('polygon',{points:zone.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'#c05b69','fill-opacity':.18,stroke:'#c05b69','stroke-width':1.5,'stroke-dasharray':'4 2',
                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-context-exclusion':zone.Id},null,this.groups.regions);
                this.element('title',{},`Excluded for ${zone.Tags.join(', ')}: ${zone.Id} (${zone.SourceRuleId})`,area);
            }
            if(world.ContextPlan.Definition.Exclusions?.length)key('Tagged exclusion','#c05b69');
            for(const region of world.ContextPlan.Definition.Regions){
                const polygon=this.element('polygon',{points:region.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),
                    fill:'#8064a2','fill-opacity':.06,stroke:'#8064a2','stroke-width':1.5,'stroke-dasharray':'7 4',
                    'vector-effect':'non-scaling-stroke','pointer-events':'none','data-context-region':region.Id},null,this.groups.regions);
                this.element('title',{},`${region.Id}: ${region.Use} (${region.SourceRuleId})`,polygon);
            }
        }
        if(plan.suitability&&[...plan.suitability.values()].some(entries=>entries.some(e=>e.Placement?.IsReservation)))key('Placement reservation','#a77b3b');
        const featureColor=kind=>['Garden','GreenBuffer','Lawn'].includes(kind)?'#6e9b69':'#b7a276';
        for(const kind of new Set((world.PlacementPlan?.SiteFeatures??[]).map(f=>f.Kind)))key(`Site: ${kind}`,featureColor(kind));
        const safeColor = (value, fallback) => typeof value === 'string' && /^#[a-f0-9]{6}$/i.test(value) ? value : fallback;
        for (const box of plan.boxes) {
            const pointChecks=plan.suitability?.get(box.id)||[];
            const h = box.node.Properties?.schematic ?? {}, label = h.label || (box.marker ? `${box.node.Kind} ${box.id.split('/').at(-1)}` : box.node.Kind);
            const group = this.element('g', { tabindex: 0, role: 'button', 'aria-label': `${label}: ${box.id}`, 'data-node-id': box.id, ...(box.node.VariationKey?{'data-variation-archetype':box.node.ArchetypeId}:{}) }, null,
                box.container ? this.groups.regions : this.groups.sites);
            for(const feature of pointChecks[0]?.Placement?.SiteFeatures??[]){
                const color=featureColor(feature.Kind);
                const polygon=this.element('polygon',{points:feature.Boundary.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:color,'fill-opacity':.6,
                    stroke:color,'stroke-width':1,'vector-effect':'non-scaling-stroke','data-site-feature':feature.Id},null,group);
                this.element('title',{},`${feature.Kind}: ${feature.Purpose} (${feature.SourceRuleId})`,polygon);
            }
            for(const entry of pointChecks)if(entry.Footprint){
                const area=entry.Footprint,reserved=entry.Placement?.IsReservation===true,color=!area.Eligible?'#ca5964':reserved?'#a77b3b':box.physicalAccess?.Occupancy==='Solid'?'#596b86':'#3c9669';
                const polygon=this.element('polygon',{points:area.Footprint.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:reserved?'none':color,'fill-opacity':.16,
                    'stroke-dasharray':reserved?'5 3':'none',
                    stroke:color,'stroke-width':1.5,'vector-effect':'non-scaling-stroke','data-footprint-rule':entry.RuleId},null,group);
                this.element('title',{},`${reserved?'Placement reservation':'Whole-footprint'} ${entry.RuleId}: ${area.Eligible?'eligible':area.Rejections.join(', ')}`,polygon);
            }
            const shape = box.marker ? {cx:box.x+box.width/2,cy:box.y+box.height/2,r:box.catalogFootprint?Math.min(box.width/2,Math.min(box.focusBounds.width,box.focusBounds.height)*.15):box.width/2} : box.polygon ? { points:box.polygon.map(p=>`${p.x},${p.y}`).join(' ') } :
                {x:box.x,y:box.y,width:box.width,height:box.height,rx:plan.coordinateSpace?0:box.container?10:4};
            const rect = this.element(box.marker?'circle':box.polygon?'polygon':'rect', { ...shape,
                fill: box.marker&&pointChecks.length?(pointChecks.every(o=>o.Result.Eligible&&o.Footprint?.Eligible!==false)?'#3c9669':'#ca5964'):h.boundsOnly?'none':safeColor(h.color, box.marker?'#e5b951':box.container ? '#e6ede7' : '#b8ccd5'), stroke: h.boundsOnly?'none':'#637d82', 'stroke-width': box.depth === 0 ? 2 : 1, 'vector-effect':'non-scaling-stroke' }, null, group);
            this.element('title', {}, `${label}\n${box.node.Purpose}`, group);
            if (!box.marker && !box.container && !box.polygon && h.footprint !== false) {
                this.element('rect', { x: box.x + box.width * 0.12, y: box.y + box.height * 0.12, width: box.width * 0.76,
                    height: box.height * 0.60, rx: 2, fill: '#ffffff', 'fill-opacity': 0.36, stroke: '#637d82' }, null, group);
            }
            const anchor=(box.polygon||h.boundsOnly)&&Array.isArray(box.node.Position)&&box.node.Position.length===2&&box.node.Position.every(Number.isFinite)?box.node.Position:null;
            const labelBounds=box.catalogFootprint?box.focusBounds:null;
            const labelSize=labelBounds?Math.min(plan.marker*1.8,labelBounds.height*.16,labelBounds.width/Math.max(2,String(label).length)*1.1):null;
            const labelText = this.element('text', { x: labelBounds?labelBounds.x+labelBounds.width/2:anchor?anchor[0]:box.container ? box.x + 14 : box.x + box.width / 2,
                y: labelBounds?labelBounds.y+labelSize:box.marker?box.y-box.height*.5:anchor?anchor[1]:box.container ? box.y + 23 : plan.coordinateSpace ? box.y + box.height * 0.65 : box.y + box.height - 9, 'text-anchor': labelBounds||anchor?'middle':box.container ? 'start' : 'middle',
                'font-size': labelSize??(box.marker?plan.marker*1.8:plan.coordinateSpace ? Math.min(box.container?14:6,box.width/Math.max(2,String(label).length)*1.2) : box.depth === 0 ? 22 : box.container ? 16 : 12), fill: '#18383e', 'font-weight': box.container ? 650 : 500,
                'pointer-events': 'none' }, String(label).slice(0, 44), this.groups.labels);
            const select = () => {
                if (this.dragged) return;
                if (this.selected) { this.selected.rect.setAttribute('stroke-width', this.selected.width); this.selected.rect.setAttribute('stroke', '#637d82'); }
                this.selected = { rect, width: box.depth === 0 ? '2' : '1' }; rect.setAttribute('stroke', '#135d90'); rect.setAttribute('stroke-width', '4');
                this.details.replaceChildren();
                const heading = document.createElement('h2'); heading.textContent = String(label); this.details.append(heading);
                const purpose = document.createElement('p'); purpose.textContent = box.node.Purpose; this.details.append(purpose);
                this.showPlacementFeedback();
                this.showCapacity(box.id);                this.showProgression(box.id);
                this.showServices(box.id);
                this.showContext(box.id);
                this.showVariation(box.id);
                const allocationId=pointChecks.find(e=>e.Placement?.AllocationId)?.Placement.AllocationId;
                const allocation=plan.localLayouts?.Allocations.find(a=>a.Id===allocationId);
                if(allocation){const detail=document.createElement('p');detail.textContent=`Local program ${allocation.Id}: ${allocation.Program.Purpose}. Region ${allocation.RegionId}; ${allocation.Shape.Area.toFixed(1)} ${plan.unit}² allocated, minimum ${allocation.Program.MinimumArea}. Source footprint and reservation retained.`;this.details.append(detail);}
                if(box.physicalAccess){const access=document.createElement('p');access.textContent=`Occupancy: ${box.physicalAccess.Occupancy}. ${box.physicalAccess.Ports.length} declared boundary entrances. External routes avoid solid footprints; approach legs connect selected entrances.`;this.details.append(access);}
                for(const port of box.physicalAccess?.Ports||[]){const entry=document.createElement('p');entry.textContent=`${port.Id}: ${port.Purpose}. Width ${port.Width} ${world.Space.Unit}; boundary (${port.BoundaryPosition.X}, ${port.BoundaryPosition.Y}) → approach (${port.ApproachPosition.X}, ${port.ApproachPosition.Y}).`;this.details.append(entry);}
                for(const entry of pointChecks){
                    const check=document.createElement('p'),result=entry.Result;
                    check.textContent=`Point check ${entry.RuleId}: ${result.Eligible?`eligible, preference score ${result.Score.toFixed(3)}`:`rejected (${result.Rejections.join(', ')})`}. ${entry.Footprint?`Whole footprint: ${entry.Footprint.Eligible?'eligible':`rejected (${entry.Footprint.Rejections.join(', ')})`}.`:'Whole footprint not evaluated.'}`;
                    this.details.append(check);
                    const values=document.createElement('pre');values.textContent=JSON.stringify({Fields:result.Observation.Values,Decisions:result.Decisions,
                        ...(entry.Footprint?{WholeFootprint:{CompleteCoverage:entry.Footprint.CompleteCoverage,Ranges:entry.Footprint.Ranges,Decisions:entry.Footprint.Decisions}}:{}),
                        ...(entry.Placement?{Placement:entry.Placement}:{})},null,2);this.details.append(values);
                }
                const button=(text,action)=>{const item=document.createElement('button');item.type='button';item.textContent=text;item.addEventListener('click',action);this.details.append(item);return item;};
                const navigate=target=>{this.dragged=false;this.selectors.get(target.id)?.();this.focusBox(target);};
                button('Focus selection',()=>this.focusBox(box));
                const parent=boxesById.get(box.node.ParentId);
                if(parent)button(`Parent: ${parent.node.Properties?.schematic?.label||parent.node.Kind}`,()=>navigate(parent));
                const children=childrenById.get(box.id)||[];
                if(children.length){
                    const title=document.createElement('h3');title.textContent=`Contained items (${children.length})`;this.details.append(title);
                    const list=document.createElement('select');list.setAttribute('aria-label','Contained items');list.style.width='100%';
                    for(const child of children){const option=document.createElement('option');option.value=child.id;option.textContent=`${child.node.Properties?.schematic?.label||child.node.Kind} — ${child.id.split('/').pop()}`;list.append(option);}
                    this.details.append(list);button('Open and focus item',()=>navigate(boxesById.get(list.value)));
                }
                const metadata=document.createElement('details');const summary=document.createElement('summary');summary.textContent='Metadata';metadata.append(summary);this.details.append(metadata);
                const info = document.createElement('pre'); info.textContent = JSON.stringify({ Id: box.id, Kind: box.node.Kind, Parent: box.node.ParentId,
                    Diagram: { x: box.x, y: box.y, width: box.width, height: box.height }, WorldPosition: box.node.Position,
                    Polygon: box.polygon ?? null, Properties: box.node.Properties, StructureKey: box.node.StructureKey,
                    Object: this.plan.world.ObjectDefinitions?.[box.node.Properties?.object_key] ?? null,
                    Area: this.plan.world.AreaDefinitions?.[box.node.Properties?.area_key] ?? null }, null, 2); metadata.append(info);
                heading.tabIndex=-1;heading.focus();
                this.notify('schematic.node.selected', `Selected ${label}.`);
            };
            this.selectors.set(box.id,select);
            group.addEventListener('click', select);
            group.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); this.dragged = false; select(); } });
            labelText.setAttribute('aria-hidden', 'true');
            labelText.setAttribute('data-source-label',h.hideLabel===true?'hidden':'visible');
            labelText.setAttribute('data-node-id',box.id);this.labelNodes.set(box.id,labelText);
            if (h.hideLabel === true) labelText.style.display = 'none';
        }
        for (const road of plan.corridors) {
            this.element('line', { ...road, stroke: '#76878b', 'stroke-width': road.width, 'stroke-linecap': plan.coordinateSpace === 'engine-world-meters' ? 'butt' : 'square' }, null, this.groups.roads);
            this.element('line', { x1: road.x1, y1: road.y1, x2: road.x2, y2: road.y2, stroke: '#f5f1d8',
                'stroke-width': 1, 'stroke-dasharray': '6 6' }, null, this.groups.roads);
        }
        const sharedJourneys=new Set((plan.physicalCorridors||[]).flatMap(g=>g.Journeys.map(j=>j.Id)));
        const segmentCapacity=new Map((world.ServicePlan?.SegmentCapacities||[]).map(c=>[c.SegmentId,c]));
        for(const group of plan.physicalCorridors||[]){
            for(const s of group.Segments){
                const capacity=segmentCapacity.get(s.Id),used=capacity?world.ServicePlan.Usage[`segment:${s.Id}`]:null;
                const shape=this.element('line',{x1:s.From.X,y1:s.From.Y,x2:s.To.X,y2:s.To.Y,stroke:capacity?(used>=capacity.Capacity?'#9d376d':'#237e7b'):'#79634c','stroke-width':s.Width,
                    'stroke-linecap':'round','data-corridor-segment':s.Id,...(capacity?{'data-segment-usage':used,'data-segment-capacity':capacity.Capacity,'data-capacity-unit':capacity.Unit}:{})},null,this.groups.roads);
                this.element('title',{},`Shared passage: ${s.ForwardJourneys.length} forward / ${s.ReverseJourneys.length} reverse journeys; ${s.Width} ${plan.unit}. ${capacity?`Allocated ${used} / ${capacity.Capacity} ${capacity.Unit}, combined directions. `:''}${s.SourceRuleIds.join(', ')}`,shape);
            }
            for(const j of group.Junctions){
                const shape=this.element('polygon',{points:j.Envelope.Vertices.map(p=>`${p.X},${p.Y}`).join(' '),fill:'#d69843','fill-opacity':.7,
                    stroke:'#714819','stroke-width':.12,'data-corridor-junction':j.Id},null,this.groups.roads);
                this.element('title',{},`Surface junction: ${j.SegmentIds.length} incident segments, ${j.Transitions.length} permitted directed transitions. Circular-traveler clearance; no vehicle-turn qualification.`,shape);
            }
        }
        for(const route of plan.routes||[]){
            if(sharedJourneys.has(route.id))continue;
            const shape=this.element('polyline',{points:route.points.map(p=>`${p.x},${p.y}`).join(' '),fill:'none',stroke:'#79634c',
                'stroke-width':route.width,'stroke-linecap':'round','stroke-linejoin':'round','data-route-id':route.id},null,this.groups.roads);
            this.element('title',{},`${route.purpose}\n${route.sourceRuleId}\n${route.id}`,shape);
        }
        for(const item of plan.interfaceRoutes||[]){            const shape=this.element('polyline',{points:item.Forward.Crossing.Path.Points.map(p=>`${p.X},${p.Y}`).join(' '),fill:'none',                stroke:'#c23d84','stroke-width':item.width,'stroke-linecap':'round','data-physical-interface':item.InterfaceId},null,this.groups.roads);            this.element('title',{},`Physical interface ${item.InterfaceId}: ${item.FromAllocationId} to ${item.ToAllocationId}; width ${item.width} ${plan.unit}. ${item.Attempts.length} crossing attempts. ${item.Attempts.filter(a=>!a.Accepted).map(a=>a.Rejection).join('; ')}`,shape);        }        for(const box of plan.boxes)for(const port of box.physicalAccess?.Ports||[]){
            const a=port.BoundaryPosition,b=port.ApproachPosition;
            const shape=this.element('line',{x1:a.X,y1:a.Y,x2:b.X,y2:b.Y,stroke:'#b48029','stroke-width':port.Width,
                'stroke-dasharray':`${port.Width} ${port.Width*.5}`,'data-access-port':`${box.id}/${port.Id}`},null,this.groups.roads);
            this.element('title',{},`Declared entrance ${port.Id}: ${port.Purpose}; width ${port.Width} ${world.Space.Unit}. Terrain traversal is qualified for selected route profiles.`,shape);
        }
        const byId = new Map(plan.boxes.map(box => [box.id, box]));
        for (const link of world.Connections) {
            const a = byId.get(link.FromId), b = byId.get(link.ToId);
            if(!a||!b)continue;
            const ax = a.x + a.width / 2, ay = a.y + a.height / 2, bx = b.x + b.width / 2, by = b.y + b.height / 2;
            const path = this.element('path', { d: plan.boundary?`M${ax} ${ay} L${bx} ${by}`:`M${ax} ${ay} H${(ax + bx) / 2} V${by} H${bx}`, fill: 'none', stroke: '#176891',
                'stroke-width': 2, 'stroke-dasharray': '5 4', 'marker-end': link.Bidirectional ? '' : 'url(#arrow)' }, null, this.groups.links);
            this.element('title', {}, `${link.Kind}: ${link.FromId} → ${link.ToId}${link.Bidirectional ? ' (both ways)' : ''}`, path);
        }
        this.groups.links.style.display = 'none';
        this.fit(); this.notify('schematic.loaded', `${world.Nodes.length} nodes · ${world.Connections.length} relationships · ${world.Coverage}.`);
        const title = document.querySelector('#world-title'); if (title) title.textContent = world.Id.replaceAll('-', ' ');
        const scope = document.querySelector('#scope'); if (scope) scope.textContent = world.Coverage === 'PartialScope'
            ? `Partial world scope · remaining: ${(world.RemainingScopes || []).join(', ')}` : 'Complete declared recipe scope';
        const note = document.querySelector('#coordinate-note'); if (note) note.textContent = plan.boundary
            ? `Exact surface and route coordinates in ${plan.unit}. ${plan.heightField?'Height/slope colors display the sampled terrain triangles; polygon overlays remain above them. ':''}Circles mark destinations; outlined suitability polygons show declared footprints. ${plan.physicalCorridors?.length?'Shared passages are drawn once; amber polygons show qualified surface junctions. ':''}Check colors report saved engine decisions only. Dashed connections are logical intent. Artwork and gameplay remain separate.` : plan.coordinateSpace
            ? 'Exact engine-generated meter coordinates. JavaScript displays streets, parcels and footprints without relocating them. Artwork and gameplay integration are separate.'
            : 'Schematic diagram units. Boxes and road channels express the declared design; they are not measured geometry or a playable map.';
    }

    fit() { this.focusRootId=null;this.updateLabelVisibility();this.view = [this.plan.minX||0, this.plan.minY||0, this.plan.width, this.plan.height]; this.applyView();this.notify('schematic.view.fit','Showing the full map.'); }
    focusBox(box) {
        const frame=box.focusBounds||box;
        const bounds=this.svg.getBoundingClientRect(),ratio=bounds.width>0&&bounds.height>0?bounds.width/bounds.height:1;
        const engine=this.plan.coordinateSpace?.startsWith('engine-'),margin=engine?Math.max(1,Math.min(frame.width,frame.height)*.15):10;
        const width=Math.max(frame.width+2*margin,(frame.height+2*margin)*ratio,engine?1:this.plan.width/12),height=width/ratio;
        this.focusRootId=box.id;this.updateLabelVisibility();
        this.view=[frame.x+frame.width/2-width/2,frame.y+frame.height/2-height/2,width,height];this.applyView();
        this.notify('schematic.view.focused',`Focused ${box.node.Properties?.schematic?.label||box.node.Kind}.`);
    }
    updateLabelVisibility() {
        for(const [id,label] of this.labelNodes){
            let visible=label.getAttribute('data-source-label')!=='hidden';
            if(visible&&this.focusRootId){
                visible=false;let current=id;const visited=new Set();
                while(current&&!visited.has(current)){if(current===this.focusRootId){visible=true;break;}visited.add(current);current=this.boxesById.get(current)?.node.ParentId;}
            }
            label.style.display=visible?'':'none';
        }
    }
    showVariation(nodeId) {
        const node=this.plan.world.Nodes.find(n=>n.Id===nodeId);if(!node?.VariationKey)return;
        const allocation=this.plan.world.VariationPlan.Allocations.find(a=>a.Key===node.VariationKey),rule=allocation.Request.Rule;
        const heading=document.createElement('h3');heading.textContent='Catalog variation';this.details.append(heading);
        for(const text of [`Selected form: ${node.ArchetypeId}.`,rule.Purpose,`Permitted forms: ${rule.ArchetypeIds.join(', ')}.`,
            `Rule: ${rule.Id}; seed: ${rule.Seed}; source: ${rule.SourceRuleId}.`,
            'The selected catalog program retains its source dimensions and required constituents.']){
            const p=document.createElement('p');p.textContent=text;this.details.append(p);
        }
    }
    showContext(nodeId) {
        const plan=this.plan.world.ContextPlan,source=plan?.Sources.find(s=>s.NodeId===nodeId);if(!source)return;
        const heading=document.createElement('h3');heading.textContent='Region and neighbors';this.details.append(heading);
        const paragraph=text=>{const p=document.createElement('p');p.textContent=text;this.details.append(p);};
        const region=plan.Definition.Regions.find(r=>r.Id===source.RegionId);
        paragraph(`Region: ${region.Id} - ${region.Use}. Tags: ${source.Tags.join(', ')||'none'}. Source: ${source.PolicyId||'game-authored context'}.`);
        for(const e of plan.Evaluations.filter(e=>e.SiteId===nodeId||e.RuleId.startsWith('separation:')&&e.RelatedId===nodeId))
            paragraph(`${e.RuleId}: passed - ${e.RelatedId}${e.Distance==null?'':` - edge clearance ${e.Distance.toFixed(2)} / minimum ${e.Minimum} meters`}.`);
        paragraph('Engine checks cover declared region use, complete reserved geometry and authored neighbor clearance. Other source requirements remain separately qualified.');
    }

    showServices(nodeId) {
        const plan=this.plan.world.ServicePlan;if(!plan)return;
        const source=plan.Sources.find(s=>s.NodeId===nodeId);if(!source)return;
        const title=document.createElement('h3');title.textContent='Service delivery';this.details.append(title);
        const paragraph=text=>{const p=document.createElement('p');p.textContent=text;p.style.overflowWrap='anywhere';this.details.append(p);};
        for(const supply of source.Supplies)paragraph(`Supply ${supply.Kind}: ${plan.Usage[`source:${nodeId}/${supply.Id}`]} / ${supply.Quantity} ${supply.Unit} allocated.`);
        for(const allocation of plan.Allocations.filter(a=>a.ConsumerNodeId===nodeId)){
            paragraph(`${allocation.Kind}: ${allocation.Quantity} ${allocation.Unit} from ${allocation.ProviderNodeId}. ${allocation.AreaContained?'Contained resource: full allocated site lies within the source area.':allocation.Physical?`Physical delivery: ${allocation.Distance.toFixed(2)} meters, including entrance approaches.`:'Logical delivery only; physical access is unqualified.'}`);
            paragraph(allocation.AreaContained?'No transport links consumed.':`Connection chain: ${allocation.ConnectionIds.join(' → ')}.`);
            for(const use of allocation.SegmentUses||[])paragraph(`Physical passage ${use.SegmentId}: ${use.Traversals} traversal(s), ${use.Quantity} ${allocation.Unit} reserved in the shared pool.`);
        }
        paragraph(`Source: ${source.SourceId}.`);
        for(const check of plan.PolicyEvaluations?.filter(p=>p.ConsumerNodeId===nodeId)||[])
            paragraph(`Catalog supply check: ${check.RequirementId} — ${check.PolicyId}. Engine-evaluated source: ${check.SourceId}.`);
        if(source.RemainingRequirements.length)paragraph(`Unqualified requirements: ${source.RemainingRequirements.join(', ')}.`);
    }
    showProgression(nodeId=null) {        const plan=this.plan.world.ProgressionPlan;        if(!plan)return;        const results=plan.Results.filter(r=>!nodeId||r.Paths.some(p=>p.NodeIds.includes(nodeId))||plan.Rules.find(rule=>rule.Id===r.RuleId)?.ScopeNodeId===nodeId);        if(!results.length)return;        const panel=document.createElement('details'),summary=document.createElement('summary');        summary.textContent=`Gameplay progression: ${results.length} rule${results.length===1?'':'s'}`;panel.append(summary);        const paragraph=text=>{const p=document.createElement('p');p.style.overflowWrap='anywhere';p.textContent=text;panel.append(p);};        for(const result of results){            const rule=plan.Rules.find(r=>r.Id===result.RuleId);            paragraph(`${rule.Purpose}. Required order: ${rule.RequiredMilestones.join(' → ')||'no mandatory milestones'}.`);            for(const path of result.Paths.slice(0,8))paragraph(`Saved journey to ${path.GoalNodeId}: ${path.NodeIds.join(' → ')}`);            if(result.Paths.length>8)paragraph('First 8 goals shown; all paths are in the saved metadata.');        }        paragraph('Engine-validated logical reachability and mandatory milestones. Physical movement, inventory and encounter behavior are separate checks.');        this.details.append(panel);    }    showCapacity(nodeId=null) {
        const all=this.plan.world.CapacityPlan?.Allocations||[];
        const allocations=nodeId?all.filter(a=>a.Request.ParentNodeId===nodeId||a.NodeIds.includes(nodeId)):all;
        if(!allocations.length)return;
        const panel=document.createElement('details'),summary=document.createElement('summary');
        summary.textContent=`Content capacity: ${allocations.length} demand group${allocations.length===1?'':'s'}`;panel.append(summary);
        for(const allocation of allocations.slice(0,16)){
            const item=document.createElement('p');item.style.overflowWrap='anywhere';
            item.textContent=`${allocation.Request.Rule.Purpose}: ${allocation.Request.RequiredQuantity} ${allocation.Source.Unit} required; ${allocation.Count} instances × ${allocation.Source.Quantity} = ${allocation.ProvidedQuantity}. Source: ${allocation.Source.SourceId}.`;
            panel.append(item);
        }
        const scope=document.createElement('p');scope.textContent='Declared content quantity only. Service delivery and complete siting remain separate checks.';
        if(allocations.length>16)scope.textContent+=' First 16 groups shown; all groups are in the metadata.';
        panel.append(scope);this.details.append(panel);
    }
    showPlacementFeedback() {        const adaptation=this.plan.layoutAdaptation;        if(adaptation){            const panel=document.createElement('details'),summary=document.createElement('summary');            summary.textContent=`Local repartition: ${adaptation.Attempts.length-1} rejected layouts, 1 accepted`;            panel.append(summary);const description=document.createElement('p');            description.textContent=`${adaptation.Policy.Purpose}. Only ${adaptation.Policy.LayoutId} may change. Source sizes and protected anchors are retained. Work: ${adaptation.Work} / ${adaptation.Policy.MaximumWork}.`;            panel.append(description);const list=document.createElement('ol');            for(const attempt of adaptation.Attempts){                const item=document.createElement('li');item.style.overflowWrap='anywhere';                item.textContent=`${Object.entries(attempt.Steps).map(([key,value])=>`${key}: ${value}`).join(', ')}: ${attempt.Accepted?'Accepted':attempt.Rejection}`;                list.append(item);            }            panel.append(list);this.details.append(panel);        }
        const feasibility=this.plan.feasibility;if(!feasibility)return;
        const panel=document.createElement('details'),summary=document.createElement('summary');
        summary.textContent=`Placement feasibility: ${feasibility.attempts.length-1} rejected candidates, 1 accepted`;
        panel.append(summary);
        const explanation=document.createElement('p');
        explanation.textContent=`The engine checked declared entrances and required connections before accepting placement. Feasibility work: ${feasibility.work}; total placement work: ${feasibility.totalWork}.`;
        panel.append(explanation);
        const list=document.createElement('ol');
        for(const attempt of feasibility.attempts){
            const item=document.createElement('li');item.textContent=attempt.Accepted?'Accepted':attempt.Rejection;
            item.style.overflowWrap='anywhere';list.append(item);
        }
        panel.append(list);this.details.append(panel);
    }
    applyView() { this.svg.setAttribute('viewBox', this.view.join(' ')); }
    zoom(factor) {
        const width = this.view[2] / factor, height = this.view[3] / factor;
        if (width < (this.plan.coordinateSpace === 'engine-world-meters' ? 1 : this.plan.width / 12) || width > this.plan.width * 3) return;
        this.view = [this.view[0] + (this.view[2] - width) / 2, this.view[1] + (this.view[3] - height) / 2, width, height]; this.applyView();
    }
    layer(name, visible) { if (this.groups[name]) this.groups[name].style.display = visible ? '' : 'none'; }
    notify(event, message, outcome = 'succeeded') {
        this.status.textContent = message;
        const entry = { event, occurredAtUtc: new Date().toISOString(), target: this.plan?.world.Id ?? null, outcome };
        this.events.push(entry); if (this.events.length > 200) this.events.shift();
        this.svg.dispatchEvent(new CustomEvent('worldcreator.operation', { bubbles: true, detail: entry }));
    }
    downloadSvg() {
        try {
        this.notify('schematic.svg.prepared', 'SVG export prepared.');
        const copy = this.svg.cloneNode(true); copy.setAttribute('viewBox', `${this.plan.minX||0} ${this.plan.minY||0} ${this.plan.width} ${this.plan.height}`);
        for(const label of copy.querySelectorAll('text[data-source-label]'))label.style.display=label.getAttribute('data-source-label')==='hidden'?'none':'';
        copy.setAttribute('width', this.plan.width); copy.setAttribute('height', this.plan.height); copy.setAttribute('font-family', 'system-ui, sans-serif');
        this.element('metadata', {}, JSON.stringify({ RecipeSha256: this.plan.world.RecipeSha256, CatalogSha256: this.plan.world.CatalogSha256,
            CoordinateSpace: this.plan.coordinateSpace || 'schematic-diagram-units', LocalSessionEvents: this.events }), copy);
        const url = URL.createObjectURL(new Blob([new XMLSerializer().serializeToString(copy)], { type: 'image/svg+xml' }));
        const a = document.createElement('a'); a.href = url; a.download = `${this.plan.world.Id}-schematic.svg`; a.click();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
        } catch (error) { this.notify('schematic.svg.failed', 'Could not prepare the SVG export.', 'failed'); console.error('schematic.svg.failed', error); }
    }

    static start() {
        const status = document.querySelector('#status');
        try {
            const viewer = new WorldSchematic(document.querySelector('#map'), JSON.parse(document.querySelector('#world-data').textContent), document.querySelector('#details'), status);
            document.querySelector('#fit').addEventListener('click', () => viewer.fit());
            document.querySelector('#zoom-in').addEventListener('click', () => viewer.zoom(1.3));
            document.querySelector('#zoom-out').addEventListener('click', () => viewer.zoom(1 / 1.3));
            document.querySelector('#export').addEventListener('click', () => viewer.downloadSvg());
            for (const input of document.querySelectorAll('[data-toggle]')) input.addEventListener('change', () => viewer.layer(input.dataset.toggle, input.checked));
            document.querySelector('#load').addEventListener('change', async event => {
                try {
                    const file = event.target.files[0]; if (!file) return;
                    if (file.size > 16777216) throw new Error('Metadata file is too large (maximum 16 MiB).');
                    viewer.load(JSON.parse(await file.text()));
                    for (const input of document.querySelectorAll('[data-toggle]')) viewer.layer(input.dataset.toggle, input.checked);
                } catch (error) { viewer.notify('schematic.load.failed', `Cannot load schematic: ${error.message}`, 'failed'); console.error('schematic.load.failed', error); }
                finally { event.target.value = ''; }
            });
            globalThis.worldSchematic = viewer;
        } catch (error) { status.textContent = `Cannot display schematic: ${error.message}`; console.error('schematic.start.failed', error); }
    }
}
if (typeof module !== 'undefined') module.exports = WorldSchematic;
if (typeof document !== 'undefined') document.addEventListener('DOMContentLoaded', () => WorldSchematic.start());
