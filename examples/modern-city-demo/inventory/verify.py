"""Focused integrity check for this example inventory, not a completed-city certificate."""
import collections
import hashlib
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent


def read(name):
    return json.loads((HERE / name).read_text(encoding='utf-8-sig'))


def preserves_authored(authored, expanded):
    # The C# serializer writes optional/default contract fields absent in authored JSON.
    if isinstance(authored, dict):
        return isinstance(expanded, dict) and all(k in expanded and preserves_authored(v, expanded[k]) for k, v in authored.items())
    if isinstance(authored, list):
        return isinstance(expanded, list) and len(authored) == len(expanded) and all(preserves_authored(a, b) for a, b in zip(authored, expanded))
    return authored == expanded


def verify():
    handoff = read('art-handoff-v003.json')
    world = read('expanded-v003/world-metadata.json')
    assert handoff['Status'] == 'DraftInventoryOnly'
    assert handoff['World'] == world
    assert handoff['Recipe'] == read('expanded-v003/recipe.json')
    assert handoff['Catalog'] == read('expanded-v003/catalog.json')
    assert preserves_authored(read('recipe.json'), handoff['Recipe'])
    assert preserves_authored(read('catalog.json'), handoff['Catalog'])
    nodes = {n['Id']: n for n in world['Nodes']}
    assert len(nodes) == len(world['Nodes']) == 862
    assert {m['NodeId'] for m in handoff['Members']} == set(nodes)
    assert len(handoff['Members']) == len(nodes)
    counts = collections.Counter(n['Kind'] for n in nodes.values())
    assert counts == {'settlement': 1, 'district': 6, 'site': 158, 'building': 369, 'ground-area': 328}
    assert all(n['Position'] is None for n in nodes.values())
    assert world['MapGeometry'] is None and len(world['Connections']) == 8
    districts = {n['Id'] for n in nodes.values() if n['Kind'] == 'district'}
    neighbors = {n: set() for n in districts}
    for link in world['Connections']:
        assert link['FromId'] in districts and link['ToId'] in districts and link['Bidirectional']
        assert link['Properties']['geometryStatus'] == 'UnrealizedIntentOnly'
        neighbors[link['FromId']].add(link['ToId'])
        neighbors[link['ToId']].add(link['FromId'])
    reached = set()
    pending = [next(iter(districts))]
    while pending:
        node = pending.pop()
        if node not in reached:
            reached.add(node)
            pending.extend(neighbors[node] - reached)
    assert reached == districts
    assert world['Coverage'] == 'PartialScope' and world['RemainingScopes']
    # Exact site constituent coverage: a hall cannot silently stand in for a factory.
    factories = [n for n in nodes.values() if n['ArchetypeId'] == 'factory-site']
    assert len(factories) == 4
    expected = {'production-hall': 2, 'warehouse': 2, 'office': 1, 'workshop': 1,
                'guardhouse': 1, 'yard': 1, 'utility-yard': 1, 'entry-walk': 1}
    for factory in factories:
        assert collections.Counter(n['ArchetypeId'] for n in nodes.values() if n['ParentId'] == factory['Id']) == expected
    jobs = handoff['Jobs']
    assert collections.Counter(j['Kind'] for j in jobs) == {'ReferenceConcept': 23, 'DetailMockup': 22, 'OverviewMockup': 7}
    seen = set()
    for job in jobs:
        assert job['Id'] not in seen and set(job['DependsOn']) <= seen
        assert set(job['NodeIds']) <= set(nodes)
        seen.add(job['Id'])
    assert set(jobs[-1]['NodeIds']) == set(nodes)
    member_by_id = {m['NodeId']: m for m in handoff['Members']}
    role_by_type = {b['ArchetypeId']: b['Role'] for b in handoff['Specification']['Bindings']}
    for n in nodes.values():
        if n['Kind'] not in ('building', 'ground-area'):
            continue
        assert role_by_type[n['ArchetypeId']] == ('Structure' if n['Kind'] == 'building' else 'Ground')
        member = member_by_id[n['Id']]
        assert member['ScopeId'] and member['ReferenceJobId']
        job = next(j for j in jobs if j['Id'] == member['ReferenceJobId'])
        assert n['Id'] in job['NodeIds']
    previous = read('art-handoff-v002.json')
    assert previous['World']['Nodes'] == world['Nodes']
    assert [j for j in previous['Jobs'] if j['Kind'] == 'ReferenceConcept'] == [j for j in jobs if j['Kind'] == 'ReferenceConcept']
    visual = {n['Id'] for n in nodes.values() if n['Kind'] in ('building', 'ground-area')}
    detail_jobs = [j for j in jobs if j['Kind'] == 'DetailMockup']
    owners = collections.Counter(i for j in detail_jobs for i in j['NodeIds'] if i in visual)
    assert set(owners) == visual and all(count == 1 for count in owners.values())
    policy = read('visual-view-policy.json')
    assert all(sum(nodes[i]['Kind'] == 'building' for i in job['NodeIds']) <= policy['MaximumBuildingBodiesPerDetailView'] for job in detail_jobs)
    # A complete site, including every factory constituent, belongs to one detail view.
    for site in (n for n in nodes.values() if n['Kind'] == 'site'):
        subtree = {i for i in nodes if i == site['Id'] or i.startswith(site['Id'] + '/')}
        assert sum(subtree <= set(j['NodeIds']) for j in detail_jobs) == 1
    example = HERE.parent
    views = read('../visual-plan/detail-views.json')['Views']
    assert {v['JobId'] for v in views} == {j['Id'] for j in detail_jobs}
    job_by_id = {j['Id']: j for j in jobs}
    for view in views:
        job = job_by_id[view['JobId']]
        assert view['SourceNodeIds'] == job['NodeIds']
        assert view['RequiredReferenceJobs'] == job['DependsOn']
        assert view['Status'] == 'DraftPromptOnly' and view['GeneratedImage'] is None
        assert (example / view['Prompt']).is_file()
    city = read('../visual-plan/city-brief.json')
    assert set(city['RequiredMemberNodeIds']) == set(nodes)
    assert city['GeneratedImage'] is None and city['ReadyForGeneration'] is False
    assert city['LogicalInterfaces'] == world['Connections']
    assert len(city['Districts']) == 6
    assert sum(d['Buildings'] for d in city['Districts']) == 369
    assert sum(d['GroundAreas'] for d in city['Districts']) == 328
    assert sum(d['DetailViews'] for d in city['Districts']) == 22
    for district in city['Districts']:
        brief = read('../visual-plan/districts/' + district['District'] + '.json')
        context = brief['ContextNodeId']
        assert set(brief['RequiredMemberNodeIds']) == {i for i in nodes if i == context or i.startswith(context + '/')}
        assert brief['GeneratedOverviewImage'] is None
        assert (example / brief['OverviewPrompt']).is_file()
    files = ['recipe.json', 'catalog.json', 'scopes-v003.json', 'art-handoff-v003.json', 'expanded-v003/world-metadata.json',
             'visual-view-policy.json', 'detail-view-inventory.json']
    prompt_hashes = {str(p.relative_to(example)): hashlib.sha256(p.read_bytes()).hexdigest()
                     for p in sorted((example / 'visual-plan').rglob('*')) if p.is_file()}
    return {'Status': 'DraftInventoryIntegrityVerified', 'Nodes': counts, 'ReferenceJobs': 23,
            'LogicalDistrictInterfaces': 8, 'PhysicalPathsCertified': 0,
            'DetailJobs': 22, 'DistrictOverviewJobs': 6, 'CityOverviewJobs': 1,
            'Hashes': {f: hashlib.sha256((HERE / f).read_bytes()).hexdigest() for f in files},
            'VisualBriefHashes': prompt_hashes,
            'Scope': 'Authored reference-test inventory, logical connection intent and draft visual briefs only. No artwork approval, physical map, roads, runtime, image legibility or whole-city acceptance certified.'}


if __name__ == '__main__':
    print(json.dumps(verify(), indent=2))
