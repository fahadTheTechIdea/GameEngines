"""Read-only canonical option validation; never records answers or confirmation."""
from pathlib import Path
import hashlib
import json
import sys

framework=Path(sys.argv[1]).resolve()
output=Path(sys.argv[2]).resolve()
if output.exists():raise RuntimeError('Proposal output exists')
sys.path.insert(0,str(framework/'scripts'))
import gamearts_setup as setup
choices=[('project_name','name','framework-city-reference'),('world_kind','free_world',None),('look','pixel_mid',None),
         ('viewpoint','top_down',None),('scale','32',None),('directions','per_object',None),('animation','hybrid',None),
         ('image_generator','built_in_image_gen',None),('engine','godot',None),('palette','none',None)]
proposed={}
for name,value,text in choices:
    setup.check_answer(proposed,name,value,text)
    proposed[name]=dict(value=value,**({'text':text} if text else {}))
missing=setup.unanswered(proposed)
if missing:raise RuntimeError('Unspecified proposal choices: '+str(missing))
body=dict(Version=1,Status='ProposedOnly',Scope='Framework reference art test, not a commissioned game',OwnerConfirmed=False,
          Choices=proposed,CanonicalOptionValidation='Passed; this is not owner selection/confirmation',
          CanonicalSetupSha256=hashlib.sha256((framework/'scripts/gamearts_setup.py').read_bytes()).hexdigest(),
          TestOnlyPresentationCellMeters=.5,TestOnlyMaximumPoseIntervalSeconds=.05)
output.write_text(json.dumps(body,indent=2)+'\n')
print('PASS: all ten proposed art-test choices are valid canonical options; no answers, settings or owner confirmation written.')
