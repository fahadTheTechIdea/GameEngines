"""Canonical-tool integration fixture only. No real consumer or owner approval.
All simulated settings and lifecycle transitions remain under the new test folder.
"""
from pathlib import Path
import json
import shutil
import sys

framework = Path(sys.argv[1]).resolve()
target = Path(sys.argv[2]).resolve()
if target.exists():
    raise RuntimeError('Test output already exists')
target.mkdir(parents=True)
sys.path[:0] = [str(framework / 'scripts'), str(framework / 'tests')]
import gamearts_testkit as kit
import gamearts_asset_lifecycle as life
import build_component_bundle as bundles

project = target / 'consumer-fixture'
project.mkdir()
kit.write_settings(project, confirmed_by='SYNTHETIC TEST FIXTURE — not an owner decision')
image, metadata = kit.make_mockup(project, 'fixture-scene', 'v001', 'world')
life.register_mockup(project, metadata, image)
life.approve_mockup(project, 'fixture-scene', 'v001', 'TEST FIXTURE ONLY; no real human/artwork approval')
source = project / '.gamearts' / 'inputs'
source.mkdir()
shutil.copyfile(Path(__file__).resolve().parent / 'mockup-atlas/closed.png', source / 'frame.png')
config = dict(name='fixture_native_v001', directions=['S'], sample_times=[0], pixels_per_meter=1,
              source_mockups=[dict(mockup_id='fixture-scene', revision='v001')],
              components=[dict(id='base', parent=None, views=dict(S=dict(padding=0, frames=[
                  dict(file='frame.png', name='idle', direction='S', pivot=[8,8],
                       anchors={'g0':[5,6], 'g1':[11,6], 'g2':[11,10], 'g3':[5,10]})])))])
relative = '.gamearts/outputs/assets/generated/fixture_v001'
bundles.build(config, project / relative, source)
life.register_asset(project, 'stable-fixture', 'v001', relative, [('fixture-scene','v001')])
life.approve_asset(project, 'stable-fixture', 'TEST FIXTURE ONLY; no real human/artwork approval')
(target / 'STATUS.md').write_text('Synthetic integration fixture. Canonical tools create test lifecycle records; these are never real owner approvals or production art.\n')
print(json.dumps(dict(status='TEST_FIXTURE_READY', project=str(project), asset_id='stable-fixture',
                      native_bundle_asset_id='fixture_native_v001', revision='v001', real_owner_approval=False)))
