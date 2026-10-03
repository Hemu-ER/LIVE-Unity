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
assert sum(bool(e["Implemented"]) for e in entries.values()) == 20

for character, mapping in {
    "jenny": {"persona": ("active",0,"SkillRatioByStar"),"revive": ("passive",0,"SourceMaxHealthRatioByStar"),"reviveAs": ("passive",2,"BuffAmountByStar")},
    "ian": {"revive": ("active",0,"SourceMaxHealthRatioByStar"),"reviveAs": ("active",2,"BuffAmountByStar"),"lifesteal": ("active",3,"BuffAmountByStar")},
    "kenneth": {"shield": ("active", 0, "AttackRatioByStar"), "rage": ("active", 1, "BuffAmountByStar"), "lifesteal": ("passive", 0, "BuffAmountByStar")},
    "abigail": {"spin": ("active", 0, "SkillRatioByStar"), "shred": ("passive", 0, "BuffAmountByStar")},
    "sua": {"odyssey": ("active", 0, "SkillRatioByStar"), "lifesteal": ("active", 0, "HealCasterRatioByStar"), "mind": ("passive", 0, "SkillRatioByStar"), "heal": ("passive", 1, "SourceMaxHealthRatioByStar")},
    "marcus": {"quake": ("active", 0, "AttackRatioByStar"), "shock": ("passive", 0, "AttackRatioByStar")},
    "bianca": {"dominion": ("active", 0, "SkillRatioByStar"), "maxHp": ("active", 0, "TargetMaxHealthRatioByStar")},
    "garnet": {"chain": ("active", 2, "TargetMaxHealthRatioByStar"), "execute": ("active", 3, "ExecuteThresholdByStar"), "basicReduce": ("passive", 0, "BuffAmountByStar")},
    "charlotte": {"heal": ("passive", 0, "SkillRatioByStar"), "buff": ("passive", 1, "BuffAmountByStar")},
}.items():
    block = re.search(character + r":\{([^}]+)\}", engine).group(1)
    for coefficient, (kind, index, field) in mapping.items():
        values = [float(x) for x in re.search(coefficient + r":\[([^]]+)\]", block).group(1).split(",")]
        if coefficient == "shred": values = [-value for value in values]
        assert entries["web." + character + "." + kind]["Definition"]["Effects"][index][field] == values
assert "nextBianca:4" in engine and "u.skill.nextBianca+=8" in engine
assert "dst.hp>0&&dst.hp<=dst.maxHp*.5" in engine and "dst.skill.biancaRestUntil=time+3" in engine
assert "applyCC(t,1);t.def*=.90;damage" in engine
assert "u.basicCount%3===0" in engine and "a.skill.invulnUntil=time+1" in engine
assert "QA.charlotteBuffDur" in engine and "charlotteBuffDur:3" in engine
assert "dealt*(u.coefficients.lifesteal||.1)" in engine
assert "if(u.name==='아비게일'&&!t.dead)" in engine
assert "u.name==='마커스'&&t.shock&&!t.dead" in engine and "t.shock=false" in engine
assert "u.nextQuake&&u.ccUntil<=time" in engine
assert "u.skill.nextSua+=4" in engine and "total+=damage" in engine
assert "dst.as*=1+(dst.coefficients.reviveAs||0)" in engine
assert "dst.skill.invulnUntil=time+1.5" in engine
assert "u.skill.ianRevived?1.2:.8" in engine
assert "if(u.skill.personaReady){u.skill.personaReady=false" in engine
assert "u.basicCount>=2){u.basicCount=0;u.skill.personaReady=true}" in engine
assert entries["web.jenny.passive"]["Definition"]["Effects"][2]["Permanent"]
assert entries["web.jenny.active"]["Definition"]["ReserveNextBasic"]
assert entries["web.ian.passive"]["Definition"]["Effects"][0]["Key"] == entries["web.ian.active"]["Definition"]["Effects"][1]["Key"]
print("WEB_SKILL_SOURCE_CHECK_PASSED: 10 executable character coefficient sets; Jenny/Ian permanent state and reservation rules")
