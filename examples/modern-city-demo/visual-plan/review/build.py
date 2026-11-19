"""Build a read-only review page from current canonical records. Never approves art."""
import hashlib
import html
import json
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import quote

HERE = Path(__file__).resolve().parent
EXAMPLE = HERE.parents[1]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def build():
    refs = read(EXAMPLE / '.gamearts/inputs/reference-execution.json')
    scenes = read(EXAMPLE / '.gamearts/inputs/district-execution.json')
    manifest = read(EXAMPLE / '.gamearts/outputs/manifests/mockups.json')['mockups']
    proposed, failed = [], []

    def artifact(job):
        matches = [m for m in manifest if m['mockup_id'] == job['MockupId'] and m['revision'] == job['Revision']]
        if len(matches) != 1:
            raise ValueError('Ambiguous or missing canonical revision: ' + job['MockupId'])
        item = matches[0]
        path = (EXAMPLE / item['file']).resolve()
        if not path.is_relative_to(EXAMPLE.resolve()):
            raise ValueError('Image outside example root')
        if hashlib.sha256(path.read_bytes()).hexdigest() != item['sha256']:
            raise ValueError('Canonical image hash mismatch: ' + item['mockup_id'])
        return item

    for job in refs['Jobs']:
        item = artifact(job)
        if item['status'] == 'approved':
            continue
        if item['status'] != 'generated':
            raise ValueError('Reference is not eligible for review: ' + item['mockup_id'])
        proposed.append({'Kind': 'Building reference' if job['Role'] == 'Structure' else 'Ground-area concept',
                         'Id': item['mockup_id'], 'Revision': item['revision'], 'File': item['file'],
                         'Sha256': item['sha256'], 'Status': 'Awaiting owner review',
                         'Notes': job.get('ObservedDesignNotes', [])})
    for job in scenes['Jobs']:
        item = artifact(job)
        if item['status'] == 'approved':
            continue
        if item['status'] != 'generated':
            raise ValueError('Scene is not eligible for review: ' + item['mockup_id'])
        review = job['SelfReview']
        good = review['ExpectedBuildingBodies'] == review['ObservedDistinctBuildingBodies']
        card = {'Kind': 'District detail', 'Id': item['mockup_id'], 'Revision': item['revision'],
                'File': item['file'], 'Sha256': item['sha256'],
                'Status': 'Awaiting owner review' if good else 'Not proposed for approval',
                'Notes': [f"Codex visual self-review: {review['ObservedDistinctBuildingBodies']} of {review['ExpectedBuildingBodies']} building bodies.",
                          'Map calibration, occluded ground/doors and native acceptance are not established.'],
                'ExpectedBuildings': review['ExpectedBuildingBodies'],
                'ObservedBuildingsSelfReview': review['ObservedDistinctBuildingBodies']}
        (proposed if good else failed).append(card)
    proposed.sort(key=lambda p: (0 if p['Kind'] == 'District detail' else 1, p['Kind'], p['Id']))
    snapshot = {'Version': 1, 'RecordedAtUtc': datetime.now(timezone.utc).isoformat(),
                'Status': 'ReadOnlyReviewSnapshot', 'ProposedCandidates': proposed,
                'FailedCandidatesNotProposed': failed,
                'ApprovalAuthority': 'Canonical GameArts lifecycle; this page has no approval/write controls.',
                'MapOrWorldAccepted': False}
    HERE.mkdir(parents=True, exist_ok=True)
    (HERE / 'candidates.json').write_text(json.dumps(snapshot, indent=2) + '\n', encoding='utf-8')

    def card(item, bad=False):
        url = '../../' + quote(item['File'], safe='/')
        title = html.escape(item['Id'])
        notes = ''.join('<li>' + html.escape(n) + '</li>' for n in item['Notes'])
        return f'''<article class="card{' failed' if bad else ''}">
<a class="preview" href="{url}" target="_blank" rel="noopener"><img loading="lazy" src="{url}" alt="{title} {html.escape(item['Revision'])}"></a>
<div class="body"><span class="kind">{html.escape(item['Kind'])}</span><h3>{title}</h3>
<p><strong>{html.escape(item['Revision'])}</strong> · {html.escape(item['Status'])}</p>
<ul>{notes}</ul><details><summary>Exact artifact identity</summary><code>{html.escape(item['Sha256'])}</code></details>
<a class="full" href="{url}" target="_blank" rel="noopener">Open full-size image ↗</a></div></article>'''

    cards = '\n'.join(card(p) for p in proposed)
    failures = '\n'.join(card(p, True) for p in failed)
    page = '''<!doctype html><html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>Framework art review</title>
<style>
:root{color-scheme:light dark;font-family:system-ui,sans-serif;background:#f4f2ed;color:#252622}
*{box-sizing:border-box}body{margin:0}main{max-width:1320px;margin:auto;padding:32px 24px 64px}
header{max-width:920px;margin-bottom:32px}.eyebrow{font-size:12px;text-transform:uppercase;letter-spacing:.14em;color:#586d63}
h1{font-size:clamp(30px,5vw,46px);line-height:1.1;margin:12px 0}p{line-height:1.55}.intro{font-size:18px;color:#5b6058}
.notice{background:#fff8e8;border-left:4px solid #ae7933;padding:12px 16px;border-radius:4px}
.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(300px,1fr));gap:20px;align-items:start}
.card{background:#fff;border:1px solid #d9ddd3;border-radius:12px;overflow:hidden}.preview{display:block;background:#dedfd8}
img{display:block;width:100%;height:245px;object-fit:contain}.body{padding:18px}.kind{font-size:12px;color:#546a60}
h2{font-size:25px;margin:36px 0 16px}h3{font-size:16px;overflow-wrap:anywhere;margin:8px 0}.body p{font-size:14px}
ul{padding-left:20px;font-size:13px;line-height:1.5;color:#586054}details{font-size:12px;margin:14px 0}summary{cursor:pointer}
code{display:block;overflow-wrap:anywhere;margin-top:8px}.full{color:#215b49;font-size:14px}.failed{border:2px solid #ad5b48}
footer{margin-top:32px;font-size:12px;color:#697064}a:focus-visible,summary:focus-visible{outline:3px solid #b7752e;outline-offset:4px}
@media(prefers-color-scheme:dark){:root{background:#191c19;color:#e8ece5}.intro,ul{color:#b8c1b3}.card{background:#222722;border-color:#3b453b}.preview{background:#30382f}.kind,.eyebrow{color:#a7c1ae}.notice{background:#322c1d}.full{color:#aad8c3}footer{color:#a2afa0}}
</style></head><body><main><header><div class="eyebrow">GameFrameWork · Reference-city test</div>
<h1>Review the current artwork</h1><p class="intro">Exact candidate images in one place. Open any image at full size, then use the pending chat review questions to approve it or request changes.</p>
<div class="notice">This page is read-only. It does not approve artwork, change canonical records, or certify map calibration. The failed-count detail is separate and is not proposed for approval.</div></header>
'''
    page += f'<h2>{len(proposed)} candidates awaiting review</h2><section class="grid" aria-label="Candidates awaiting owner review">{cards}</section>'
    if failed:
        page += '<h2>Needs revision direction</h2><p>The required inventory stays unchanged. Choose the next approach in chat; this image does not satisfy its count.</p>'
        page += f'<section class="grid" aria-label="Failed-count candidates">{failures}</section>'
    page += '<footer>Snapshot ' + html.escape(snapshot['RecordedAtUtc']) + ' · Canonical exact revisions and hashes were checked when this page was built. No automatic approval controls.</footer></main></body></html>'
    (HERE / 'index.html').write_text(page, encoding='utf-8')
    print(json.dumps({'Status': snapshot['Status'], 'Candidates': len(proposed), 'FailedSeparate': len(failed), 'Page': str(HERE / 'index.html')}))


if __name__ == '__main__':
    build()
