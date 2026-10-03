"""Usage: python Tools/verify_web_skills.py /path/to/pinned/web/text/files"""
import json, pathlib, re, sys
root = pathlib.Path(__file__).resolve().parents[1]
source = pathlib.Path(sys.argv[1])
engine = (source / "combat-engine.js").read_text(encoding="utf-8-sig")
game = (source / "game.js").read_text(encoding="utf-8-sig")
roster = (source / "roster.js").read_text(encoding="utf-8-sig")
catalog = json.loads((root / "Assets/Prototype/Resources/CharacterSkills.json").read_text(encoding="utf-8-sig"))
entries = {entry["Id"]: entry for entry in catalog["Definitions"]}
assert len(entries) == 64 and len(catalog["Characters"]) == 32
for binding in catalog["Characters"]:
    assert binding["CharacterId"] in roster
    for field in ("ActiveId", "PassiveId"):
        assert entries[binding[field]]["DisplayName"] in game
line = re.search(r"isol:\{bomb:\[([^]]+)\],start:\[([^]]+)\]", engine)
assert line
bomb, start = [[float(x) for x in group.split(",")] for group in line.groups()]
active = entries["web.isol.active"]["Definition"]
passive = entries["web.isol.passive"]["Definition"]
assert active["Effects"][0]["AttackRatioByStar"] == bomb
assert all(effect["BuffAmountByStar"] == start for effect in passive["Effects"])
assert active["FirstTriggerSeconds"] == active["IntervalSeconds"] == 10
assert "isolNext:10" in engine and "u.skill.isolNext+=10" in engine
assert "currentAtk(u)*(u.coefficients.bomb||1)" in engine
assert "u.atk*=1+(u.coefficients.start||.15);u.as*=1+(u.coefficients.start||.15)" in engine
assert sum(bool(e["Implemented"]) for e in entries.values()) == 2
print("WEB_SKILL_SOURCE_CHECK_PASSED: 32 bindings, 64 names/IDs, Isol coefficients and engine trigger/start rules")
