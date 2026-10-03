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
assert sum(bool(e["Implemented"]) for e in entries.values()) == 8

for character, mapping in {
    "bianca": {"dominion": ("active", 0, "SkillRatioByStar"), "maxHp": ("active", 0, "TargetMaxHealthRatioByStar")},
    "garnet": {"chain": ("active", 2, "TargetMaxHealthRatioByStar"), "execute": ("active", 3, "ExecuteThresholdByStar"), "basicReduce": ("passive", 0, "BuffAmountByStar")},
    "charlotte": {"heal": ("passive", 0, "SkillRatioByStar"), "buff": ("passive", 1, "BuffAmountByStar")},
}.items():
    block = re.search(character + r":\{([^}]+)\}", engine).group(1)
    for coefficient, (kind, index, field) in mapping.items():
        values = [float(x) for x in re.search(coefficient + r":\[([^]]+)\]", block).group(1).split(",")]
        assert entries["web." + character + "." + kind]["Definition"]["Effects"][index][field] == values
assert "nextBianca:4" in engine and "u.skill.nextBianca+=8" in engine
assert "dst.hp>0&&dst.hp<=dst.maxHp*.5" in engine and "dst.skill.biancaRestUntil=time+3" in engine
assert "applyCC(t,1);t.def*=.90;damage" in engine
assert "u.basicCount%3===0" in engine and "a.skill.invulnUntil=time+1" in engine
assert "QA.charlotteBuffDur" in engine and "charlotteBuffDur:3" in engine
print("WEB_SKILL_SOURCE_CHECK_PASSED: 32 bindings/64 names, Isol and three new character coefficients/trigger rules")
