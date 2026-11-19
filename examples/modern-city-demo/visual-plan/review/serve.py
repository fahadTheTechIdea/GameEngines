"""Task-local read-only HTTP review server, bound only to loopback."""
from datetime import datetime, timezone
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import json
import os

HERE = Path(__file__).resolve().parent
EXAMPLE = HERE.parents[1]
server = ThreadingHTTPServer(('127.0.0.1', 0), partial(SimpleHTTPRequestHandler, directory=str(EXAMPLE)))
receipt = {'Pid': os.getpid(), 'Port': server.server_port, 'Root': str(EXAMPLE),
           'StartedAtUtc': datetime.now(timezone.utc).isoformat(),
           'Url': f'http://127.0.0.1:{server.server_port}/visual-plan/review/index.html'}
(HERE / 'server.json').write_text(json.dumps(receipt, indent=2) + '\n', encoding='utf-8')
print(json.dumps(receipt), flush=True)
server.serve_forever()
