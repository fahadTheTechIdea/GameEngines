"""Synthetic canonical records for CLI integration, never real owner approval."""
from pathlib import Path
import json
import shutil
import sys
from PIL import Image

framework, target = (Path(p).resolve() for p in sys.argv[1:3])
if target.exists():
    raise RuntimeError('Test output exists')
target.mkdir(parents=True)
sys.path[:0] = [str(framework / 'scripts'), str(framework / 'tests')]
import gamearts_testkit as kit
import gamearts_asset_lifecycle as life
import build_component_bundle as bundles

project = target / 'consumer-fixture'
project.mkdir()
kit.write_settings(project, confirmed_by='SYNTHETIC TEST ONLY; not a human decision')
fixtures = Path(__file__).resolve().parent
image, metadata = kit.make_mockup(project, 'fixture-world', 'v001', 'world',
                                  picture_image=Image.open(fixtures/'mockup-capture-source.png'))
life.register_mockup(project, metadata, image)
life.approve_mockup(project, 'fixture-world', 'v001', 'SYNTHETIC TEST ONLY; not actual art approval')
inputs=project/'.gamearts/inputs'
inputs.mkdir()
shutil.copyfile(fixtures/'mockup-atlas/closed.png', inputs/'frame.png')
for kind, ppm in [('body',1),('tiles',32)]:
    config=dict(name=f'fixture_native_{kind}_v001', directions=['S'], sample_times=[0], pixels_per_meter=ppm,
                source_mockups=[dict(mockup_id='fixture-world',revision='v001')],
                components=[dict(id='base',parent=None,views=dict(S=dict(padding=0,frames=[
                    dict(file='frame.png',name=kind,direction='S',pivot=[8,8],
                         anchors={'g0':[5,6],'g1':[11,6],'g2':[11,10],'g3':[5,10]})])))])
    relative=f'.gamearts/outputs/assets/generated/fixture_{kind}_v001'
    bundles.build(config,project/relative,inputs)
    life.register_asset(project,f'stable-{kind}','v001',relative,[('fixture-world','v001')])
    life.approve_asset(project,f'stable-{kind}','SYNTHETIC TEST ONLY; not actual art approval')
settings=dict(Version=1,FrameworkRoot=str(framework),PythonExecutable=sys.executable,ProjectRoot=str(project),
              Revisions={'stable-body':'v001','stable-tiles':'v001'})
(target/'consumer.json').write_text(json.dumps(settings,indent=2))
(target/'STATUS.md').write_text('Synthetic canonical lifecycle fixture only. No real human approval or production artwork. Repeated pixels test framework integration, not city fidelity.\n')
print(json.dumps(dict(status='CANONICAL_WORLD_FIXTURE_READY',real_owner_approval=False,project=str(project))))
