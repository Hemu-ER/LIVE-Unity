using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeMechanismSmokeCheck
    {
        private static int assertions;
        public static void Validate(BattlefieldPrototype board)
        {
            var catalog = PrototypeCharacterSkills.Load();
            Check(catalog.Definitions.Length == 64 && catalog.Characters.Length == 32, "32 pairs of references");
            Check(catalog.Definitions.Count(e => e.Implemented) == 2, "Exactly one character ported");
            var copy = JsonUtility.FromJson<PrototypeCharacterSkills>(JsonUtility.ToJson(catalog)); copy.Validate();
            copy.Definitions[1].Id = copy.Definitions[0].Id;
            bool rejected = false; try { copy.Validate(); } catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Duplicate definition rejected");
            foreach (PrototypeSkillTrigger trigger in Enum.GetValues(typeof(PrototypeSkillTrigger)))
            foreach (PrototypeSkillEffectType type in Enum.GetValues(typeof(PrototypeSkillEffectType)))
            {
                var original = Make(trigger, new PrototypeSkillEffect { Type = type, Area = PrototypeEffectArea.TargetColumn, Key = "stack", StackLimit = 3, DelaySeconds = .2f });
                var cloned = JsonUtility.FromJson<PrototypeSkillDefinition>(JsonUtility.ToJson(original));
                Check(cloned.Trigger == trigger && cloned.Effects[0].Type == type && cloned.Effects[0].Area == PrototypeEffectArea.TargetColumn && cloned.Effects[0].StackLimit == 3 && cloned.Effects[0].DelaySeconds == .2f, "Trigger/effect serialization");
            }
            using (var f = new Fixture())
            {
                var skill = Make(PrototypeSkillTrigger.Periodic, new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Shield, BaseDamage = 7 });
                skill.FirstTriggerSeconds = .2f; skill.IntervalSeconds = .2f; skill.CooldownSeconds = .3f;
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { Skills = new[] { skill } }); f.Start();
                for (int i=0;i<9;i++) f.Step(); Check(f.A.Statistics.SkillCastCount == 0, "No early timer");
                f.Step(); Check(f.A.Statistics.SkillCastCount == 1, "First timed trigger");
                for (int i=0;i<14;i++) f.Step(); Check(f.A.Statistics.SkillCastCount == 1, "Cooldown gates periodic trigger");
                f.Step(); Check(f.A.Statistics.SkillCastCount == 2, "Cooldown expires");
            }
            foreach (var trigger in new[] { PrototypeSkillTrigger.AfterNAttacks, PrototypeSkillTrigger.HealthBelowPercent, PrototypeSkillTrigger.OnBasicHit, PrototypeSkillTrigger.StatusAtLeast })
            using (var f = new Fixture())
            {
                var skill = Make(trigger, new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Shield, BaseDamage = 11 });
                skill.AttacksRequired = 1; skill.OncePerCombat = true; skill.RequiredStatus = "test";
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { Skills = new[] { skill } }); f.Start();
                f.A.SetHealth(100); f.A.ApplyStatus("test", 1, 3, 1);
                for(int i=0;i<15;i++) f.Step(); Check(f.A.Statistics.SkillCastCount == 1, "Reactive/health/attack/stack trigger " + trigger);
            }
            using (var f = new Fixture())
            {
                f.Start(); f.A.ApplyStatus("stack", 5, 3, .2f); Check(f.A.StatusStacks("stack") == 3, "Stack cap");
                f.A.ApplyStatus("stack", -1, 3, .2f); Check(f.A.StatusStacks("stack") == 2, "Stack consume");
                f.A.ApplyBuff(new PrototypeSkillEffect { Stat = PrototypeBuffStat.AttackPower, BuffAmount = .5f, Multiplicative = true, Duration = .2f, Key = "buff" });
                Check(Math.Abs(f.A.CombatAttackPower - 30) < .001, "Multiplicative buff");
                f.A.ApplyStatus("cc", 1, 1, .2f, PrototypeStatusKind.CrowdControl);
                for(int i=0;i<9;i++) f.Step(); Check(f.A.Statistics.BasicAttackCount == 0 && !f.A.IsMoving, "CC suppresses actions");
                for(int i=0;i<3;i++) f.Step(); Check(f.A.StatusStacks("stack") == 0 && !f.A.IsControlled && Math.Abs(f.A.CombatAttackPower-20)<.001, "Status/buff expire");
                f.A.ApplyStatus("immune",1,1,1,PrototypeStatusKind.CrowdControlImmune); f.A.ApplyStatus("cc",1,1,1,PrototypeStatusKind.CrowdControl); Check(!f.A.IsControlled,"CC immunity");
                f.A.ApplyStatus("inv",1,1,1,PrototypeStatusKind.Invulnerable); int hp=f.A.CurrentHealth; f.A.TakeDamage(100); Check(f.A.CurrentHealth==hp,"Invulnerability");
                f.Combat.ResetBattle(); Check(!f.A.HasStatus(PrototypeStatusKind.Invulnerable), "Reset clears statuses");
            }
            using(var f=new Fixture())
            {
                var revive=Make(PrototypeSkillTrigger.OnLethalDamage,new PrototypeSkillEffect { Type=PrototypeSkillEffectType.Heal, SourceMaxHealthRatio=.3f }); revive.OncePerCombat=true;
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { Skills=new[]{revive} }); f.Start(); f.A.ReceiveDamage(5000,f.B);
                Check(f.A.IsAlive && f.A.CurrentHealth==300 && f.B.Statistics.Kills==0 && f.Combat.Grid.Occupant(f.A.Cell)==f.A,"Lethal revive retains occupancy");
                f.A.ReceiveDamage(5000,f.B); Check(!f.A.IsAlive && f.B.Statistics.Kills==1,"Revive only once");
            }
            foreach(var area in new[]{PrototypeEffectArea.TargetRow,PrototypeEffectArea.TargetColumn,PrototypeEffectArea.AdjacentEnemies})
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1); var b=f.Add("B",1,3); var c=f.Add("B",1,4); var d=f.Add("B",0,3);
                var skill=Make(PrototypeSkillTrigger.OnCombatStart,new PrototypeSkillEffect { Type=PrototypeSkillEffectType.Damage, BaseDamage=30,Area=area });
                skill.Target=PrototypeSkillTarget.CurrentTarget; skill.Range=6;
                a.ConfigureAbilities(new PrototypeAbilitySettings{Skills=new[]{skill}}); f.Start();
                Check(b.CurrentHealth==970,"AoE anchor");
                Check(c.CurrentHealth==(area==PrototypeEffectArea.TargetColumn?1000:970),"AoE row/adjacent");
                Check(d.CurrentHealth==(area==PrototypeEffectArea.TargetRow?1000:970),"AoE column/adjacent");
            }
            for(int star=1;star<=3;star++)
            using(var f=new Fixture())
            {
                var def=PrototypeWebRoster.Load().CreateGameData().Definition("isol");
                f.A.ConfigureAbilities(def.Abilities,star); f.A.Stats.AttackPower=72;
                f.A.Stats.MaxHealth=f.B.Stats.MaxHealth=100000; f.A.Stats.AttackSpeed=1.02f;
                f.Start(); double ratio=new[]{.15,.25,.5}[star-1];
                Check(Math.Abs(f.A.CombatAttackPower-72*(1+ratio))<.0001 && Math.Abs(f.A.EffectiveStats.AttackSpeed-1.02*(1+ratio))<.0001,"Isol exact start coefficients");
                for(int i=0;i<499;i++) f.Step(); Check(f.A.Skills.Single(s=>s.Definition.Id=="web.isol.active").CastCount==0,"Isol before 10s");
                f.Step(); Check(f.A.Skills.Single(s=>s.Definition.Id=="web.isol.active").CastCount==1,"Isol at 10s");
                var hit=f.Events.Last(e=>e.Type==PrototypeCombatEventType.DamageDealt && e.SkillId=="web.isol.active");
                Check(hit.Amount==PrototypeDamageCalculator.RoundAmount(72*(1+ratio)*new[]{1,1.5,2.5}[star-1]),"Isol exact star damage");
                for(int i=0;i<500;i++) f.Step(); Check(f.A.Skills.Single(s=>s.Definition.Id=="web.isol.active").CastCount==2,"Isol repeats at 20s");
            }
            using(var f=new Fixture())
            {
                f.Start(); f.A.ApplyBuff(new PrototypeSkillEffect { Stat=PrototypeBuffStat.BasicDamageReduction,BuffAmount=.5f,Permanent=true });
                int hp=f.A.CurrentHealth; f.A.ReceiveDamage(100,f.B,isBasic:true); Check(f.A.CurrentHealth==hp-50,"Basic damage reduction");
                f.A.ReceiveDamage(100,f.B); Check(f.A.CurrentHealth==hp-150,"Skill not reduced by basic reduction");
                f.A.ApplyStatus("immortal",1,1,.1f,PrototypeStatusKind.Immortal);f.A.ReceiveDamage(5000,f.B);Check(f.A.CurrentHealth==1&&f.A.IsAlive,"Immortal HP floor");
                f.B.ApplyStatus("hold",1,1,1,PrototypeStatusKind.CrowdControl);
                for(int n=0;n<6;n++)f.Step(); f.A.ReceiveDamage(5000,f.B);Check(!f.A.IsAlive,"Immortality expires");
            }
            using(var f=new Fixture())
            {
                var skill=Make(PrototypeSkillTrigger.OnCombatStart,new PrototypeSkillEffect { Type=PrototypeSkillEffectType.Shield,BaseDamage=17,DelaySeconds=.2f });
                f.A.ConfigureAbilities(new PrototypeAbilitySettings {Skills=new[]{skill}});f.Start();
                f.B.ApplyStatus("hold",1,1,1,PrototypeStatusKind.CrowdControl);
                for(int n=0;n<9;n++)f.Step();Check(f.A.Shield==0,"Delay does not release early");
                f.Step();Check(f.A.Shield==17,"Delayed effect releases");
                f.Combat.ResetBattle();for(int n=0;n<12;n++)f.Step();Check(f.A.Shield==0,"Reset discards delayed effect");
            }
            using(var f=new Fixture())
            {
                f.Start();f.A.SetHealth(500);f.A.ApplyBuff(new PrototypeSkillEffect {Stat=PrototypeBuffStat.Lifesteal,BuffAmount=.5f,Permanent=true});
                f.B.ApplyStatus("hold",1,1,3,PrototypeStatusKind.CrowdControl);f.Step();Check(f.A.CurrentHealth==510,"Lifesteal uses actual HP damage");
                f.B.ApplyShield(100);for(int n=0;n<51;n++)f.Step();Check(f.A.CurrentHealth==510,"Shield damage does not lifesteal");
            }
            for(int seed=0;seed<8;seed++)
            {
                string prior=null;
                for(int repeat=0;repeat<2;repeat++)using(var f=new Fixture(false))
                {
                    var data=PrototypeWebRoster.Load().CreateGameData();
                    for(int row=0;row<3;row++)for(int team=0;team<2;team++)
                    {
                        var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats("isol",1+seed%3));unit.ConfigureAbilities(data.Definition("isol").Abilities,1+seed%3);
                    }
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks++<3000)
                    {
                        f.Step();var cells=new HashSet<Vector2Int>();
                        foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&!double.IsNaN(unit.CombatAttackPower),"Seeded skill occupancy/finite stats");
                    }
                    Check(f.Combat.State==PrototypeCombatState.Finished,"Seeded Isol teams finish");
                    string result=f.Combat.Winner+"|"+ticks+"|"+f.Combat.StatisticsSummary();Check(prior==null||prior==result,"Same seed/state reproducible");prior=result;
                }
            }
            ShopIntegration(board);
            Debug.Log($"MECHANISM_SMOKE_CHECK_PASSED: {assertions} assertions; catalog, serialization, timers, cooldowns, hit/health/stack triggers, expiration, CC, immunity, revive, AoE, Isol 1/2/3-star active/passive and real shop deployment.");
        }
        private static PrototypeSkillDefinition Make(PrototypeSkillTrigger trigger, PrototypeSkillEffect effect) => new PrototypeSkillDefinition { Id="mechanism", Trigger=trigger, Target=PrototypeSkillTarget.Self,Execution=PrototypeSkillExecution.Instant,Effects=new[]{effect} };
        private static void ShopIntegration(BattlefieldPrototype board)
        {
            var c=board.RunController; c.SetDataMode(PrototypeDataMode.WebRoster); c.Data.Rules.StartingCredits=10000;c.ResetRun();
            foreach(var owned in c.Run.Player.OwnedUnits.ToArray()) c.Sell(owned.InstanceId);
            bool bought=false;
            for(int i=0;i<1000&&!bought;i++)
            {
                for(int slot=0;slot<c.Run.Shop.Count;slot++) if(c.Run.Shop.Slot(slot)=="isol") { bought=c.Buy(slot);break; }
                if(!bought)c.Reroll();
            }
            Check(bought,"Isol available in actual shop"); var isol=c.Run.Player.OwnedUnits.Single();
            Check(c.Move(isol.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Buy/deploy/start Isol");
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int i=0;i<560;i++)c.Step(.02f);
            var live=board.Units.Single(u=>u.Faction=="A");
            Check(live.Skills.Single(s=>s.Definition.Id=="web.isol.active").CastCount==1 && live.Skills.Single(s=>s.Definition.Id=="web.isol.passive").CastCount==1,"Real roster combat both skills");
            c.SetDataMode(PrototypeDataMode.TestFixtures);c.SetDataMode(PrototypeDataMode.WebRoster);
        }
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Ability smoke fixture");
            public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
            public readonly List<PrototypeCombatEvent> Events = new List<PrototypeCombatEvent>();
            public PrototypeUnit A => Units[0];
            public PrototypeUnit B => Units[1];
            public readonly PrototypeCombatController Combat;
            public Fixture(bool defaultPair = true)
            {
                Combat = root.AddComponent<PrototypeCombatController>(); Combat.enabled = false;
                Combat.EventRaised += Events.Add;
                if (defaultPair) { Add("A", 1, 1); Add("B", 1, 4); }
            }
            public PrototypeUnit Add(string faction, int row, int column, PrototypeCombatStats stats = null)
            {
                var obj = new GameObject("Ability test " + faction); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<PrototypeUnit>();
                unit.Initialize(faction, row, column, stats ?? new PrototypeCombatStats { MaxHealth = 1000, Defense = 0, AttackRange = 3 }, null, null);
                Units.Add(unit); return unit;
            }
            public void Start() { Events.Clear(); Combat.Initialize(3, 6, Units, BattlefieldPrototype.Position, 2468); Combat.StartBattle(); }
            public void Step() => Combat.Step(0.02f);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Mechanism smoke: "+message);}
    }
}
