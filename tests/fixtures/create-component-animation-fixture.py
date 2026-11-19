"""Canonical builder output for technical tests; no real approvals or production art."""
from pathlib import Path
import hashlib
import json
import shutil
import sys

framework,target=(Path(p).resolve() for p in sys.argv[1:3])
if target.exists():raise RuntimeError('Test output exists')
target.mkdir(parents=True)
sys.path[:0]=[str(framework/'scripts'),str(framework/'tests')]
from test_component_bundle import ComponentTests
import build_component_bundle as builder

inputs=target/'inputs';inputs.mkdir()
config=ComponentTests().fixture(inputs)
config['name']='fixture_native_components_v001'
config['pixels_per_meter']=1
profile_sampling=len(sys.argv)>3 and sys.argv[3]=='profile-20hz'
config['sample_times']=[i/20 for i in range(21)] if profile_sampling else [0,.25,.5,.75,1]
for component in config['components']:
    for direction,view in component['views'].items():
        view['padding']=0
        frame=view['frames'][0]
        frame['name']=component['id']+'-'+direction+'-a'
        frame['duration_ms']=100
        if component['id']=='base':
            frame['anchors'].update(g0=[7,8],g1=[13,8],g2=[13,12],g3=[7,12])
        if component['id']=='joint':
            copied=inputs/('joint_'+direction.lower()+'_b.png')
            shutil.copyfile(inputs/frame['file'],copied)
            view['frames'].append(dict(file=copied.name,name='joint-'+direction+'-b',duration_ms=100,
                                       pivot=[9,11] if direction=='S' else [10,10],anchors={'tip_mount':[11,14] if direction=='S' else [10,13]}))
    component['draw_order']={'S':{'base':0,'joint':2,'tip':3}[component['id']],
                             'E':{'base':0,'joint':-2,'tip':-1}[component['id']]}
config['components'][1]['mount_tracks']['deploy']={d:[dict(time=0,rotation_degrees=0),dict(time=1,rotation_degrees=angle)] for d,angle in [('S',360),('E',-360)]}
config['components'][2]['motion_tracks']={'default':{'*':[dict(time=0,position=[0,0],interpolation='cubic',easing='ease_in_out'),
                                                       dict(time=.5,position=[2,0]),dict(time=1,position=[0,0])]}}
builder.build(config,target/'bundle',inputs)
bundle=json.loads((target/'bundle'/f"{config['name']}.bundle.json").read_text())
def sha(file):return hashlib.sha256(Path(file).read_bytes()).hexdigest()
sheets=[]
for component in bundle['components']:
    for direction,name in component['atlases'].items():
        atlas=json.loads((target/'bundle'/name).read_text())
        sheets.append(dict(Id=component['id']+':'+direction,ImagePath='bundle/'+atlas['image'],AtlasPath='bundle/'+name,
                           ImageSha256=sha(target/'bundle'/atlas['image']),AtlasSha256=sha(target/'bundle'/name),ComponentId=component['id'],Directions=[direction]))
manifest=dict(SchemaVersion=2,AssetId='stable-components',BundleAssetId=config['name'],GameArtsRevision='v001',
              StandardVersion='SYNTHETIC canonical builder fixture; no approval',ApprovalState='Approved',Sheets=sheets,Attachments=[])
(target/'manifest.json').write_text(json.dumps(manifest,indent=2))
for direction in config['directions']:
    sources=[]
    for component in config['components']:
        for frame in component['views'][direction]['frames']:
            sources.append(dict(ComponentId=component['id'],FrameName=frame['name'],Master=dict(File='inputs/'+frame['file'],Sha256=sha(inputs/frame['file']))))
    request=dict(Version=1,AssetId='stable-components',Revision='v001',BundlePath='bundle/'+config['name']+'.bundle.json',Facing=direction,
                 DurationSeconds=1,Loop=direction=='E',MaximumPoseIntervalSeconds=.05 if profile_sampling else .25,Sources=sources)
    (target/f'animation-{direction}.json').write_text(json.dumps(request,indent=2))
(target/'STATUS.md').write_text('Synthetic unit/integration inputs from the canonical component builder. Manifest approval is a test authority fixture, not real lifecycle or user approval. Parent-cleanup/art review remains pending; never use this as approved production art.\n')
print('COMPONENT_FIXTURE_READY: two facings, three nested components, timed frame/pivot/anchor changes, sampled rotation and eased/cubic source motion. No real approvals.')
