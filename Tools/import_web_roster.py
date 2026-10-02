"""Extract the JSON roster literal without executing downloaded JavaScript.
Usage: python Tools/import_web_roster.py path/to/roster.js --commit SHA [--check]
Only data is imported; no web combat code or images are copied.
"""
import argparse
import hashlib
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('source', type=Path)
p.add_argument('--commit', required=True)
p.add_argument('--check', action='store_true')
args = p.parse_args()
raw = args.source.read_bytes()
text = raw.decode('utf-8-sig')
start = text.index('const roster=') + len('const roster=')
rows, end = json.JSONDecoder().raw_decode(text[start:].lstrip())
assert isinstance(rows, list) and rows
assert len({r['id'] for r in rows}) == len(rows), 'Duplicate IDs'
for r in rows:
    assert set(r) <= {'id', 'name', 'cost', 'role', 'affiliations', 'baseStats', 'implemented', 'main', 'asset', 'pveOnly'}
    assert set(r['baseStats']) == {'hp', 'atk', 'amp', 'range', 'def', 'as'}
    assert all(type(r['baseStats'][k]) is int for k in ('hp', 'atk', 'amp', 'range', 'def')), 'Integer stat conversion requires explicit review'
    assert isinstance(r['baseStats']['as'], (int, float)) and r['baseStats']['as'] > 0
    assert r['pveOnly'] if r['cost'] == 0 else 1 <= r['cost'] <= 3
blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
payload = dict(SourceRepository='Hemu-ER/ER-AutoChess', SourceCommit=args.commit,
               SourceRosterBlob=blob, Entries=rows)
output = Path(__file__).resolve().parents[1] / 'Assets/Prototype/Resources/WebRoster.json'
generated = json.dumps(payload, ensure_ascii=False, indent=2) + '\n'
if args.check:
    assert json.loads(output.read_text(encoding='utf-8-sig')) == payload, 'Imported data differs from source'
else:
    output.write_text(generated, encoding='utf-8')
playable = sum(not r.get('pveOnly', False) and 1 <= r['cost'] <= 3 for r in rows)
print(f'WEB_ROSTER_SOURCE_CHECK_PASSED: {len(rows)} rows, {playable} playable, {len(rows)-playable} PvE; blob {blob}')
