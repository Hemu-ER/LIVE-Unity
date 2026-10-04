using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeJustinaSmokeCheck
    {
        private static int assertions,timeoutDraws;
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=timeoutDraws=0;
            for(int star=1;star<=3;star++){Rules(star);ShopMerge(board,"justina",star);}
            LethalBasic();Simulations();
            Debug.Log($"JUSTINA_SMOKE_CHECK_PASSED: {assertions} assertions; 1-3 stars, row/boost ordering, lethal-basic retention/retarget, CC/reset, real shop merges and24 deterministic simulations ({timeoutDraws} timeout draws).");
        }
        private static void Equip(PrototypeUnit u,string id,int star,string kind=null)
        {
            var abilities=JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(PrototypeWebRoster.Load().CreateGameData().Definition(id).Abilities));
            if(kind!=null)abilities.Skills=abilities.Skills.Where(s=>s.Id.EndsWith("."+kind)).ToArray();
            u.ConfigureAbilities(abilities,star);u.Stats.MaxHealth=10000;u.Stats.AttackPower=100;u.Stats.Defense=20;u.Stats.AttackSpeed=1;u.Stats.AttackRange=6;u.Stats.SkillAmplification=200;
        }
        private static void Hold(PrototypeUnit u)=>u.ApplyStatus("test.hold",1,1,100,PrototypeStatusKind.CrowdControl);
        private static void Release(PrototypeUnit u)=>u.ApplyStatus("test.hold",-1,1,1);
        private static void Ticks(Fixture f,int n){for(int i=0;i<n;i++)f.Step();}
        private static PrototypeSkillRuntime Skill(PrototypeUnit u,string id)=>u.Skills.Single(s=>s.Definition.Id==id);
        private static void Attacks(Fixture f,PrototypeUnit u,int n){int ticks=0;while(u.Statistics.BasicAttackCount<n&&ticks++<2000)f.Step();Check(u.Statistics.BasicAttackCount==n,"Requested attack count");}
        private static void Rules(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"justina",star);var same=f.Add("B",1,5);var other=f.Add("B",0,4);var dead=f.Add("B",1,3);
                foreach(var u in f.Units.Skip(1))u.Stats.MaxHealth=100000;
                f.Start();dead.SetHealth(0);foreach(var u in f.Units.Skip(1))if(u.IsAlive)Hold(u);
                var active=Skill(f.A,"web.justina.active");var passive=Skill(f.A,"web.justina.passive");
                Attacks(f,f.A,1);Check(active.CastCount==0&&!passive.NextBasicReserved,"One hit does not trigger");
                int start=f.Events.Count;Attacks(f,f.A,2);
                int bomb=PrototypeDamageCalculator.RoundAmount(200*new[]{.85,1.1,1.9}[star-1]);
                int boost=PrototypeDamageCalculator.RoundAmount(200*new[]{.45,.6,1}[star-1]);
                Check(active.CastCount==1&&passive.CastCount==0&&passive.NextBasicReserved,"Second hit AoE then reserve only");
                Check(f.B.CurrentHealth==100000-200-bomb&&same.CurrentHealth==100000-bomb&&other.CurrentHealth==100000&&dead.CurrentHealth==0,"Living target row only and AMP coefficients");
                var events=f.Events.Skip(start).ToList();int basic=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&string.IsNullOrEmpty(e.SkillId));int row=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.justina.active");int reserve=events.FindIndex(e=>e.Type==PrototypeCombatEventType.NextAttackReserved);
                Check(basic>=0&&row>basic&&reserve>row&&!events.Any(e=>e.Type==PrototypeCombatEventType.NextAttackConsumed),"Basic -> row -> reserve, no same-hit consume");
                Check(events.Count(e=>e.Type==PrototypeCombatEventType.BasicAttackStarted)==1&&events.Count(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.justina.active")==2,"AoE is not another basic hit");
                Hold(f.A);Ticks(f,75);Check(passive.NextBasicReserved&&passive.CastCount==0&&f.A.Statistics.BasicAttackCount==2,"CC preserves reservation");Release(f.A);
                Attacks(f,f.A,3);Check(passive.CastCount==1&&!passive.NextBasicReserved&&active.CastCount==1&&f.B.CurrentHealth==100000-300-bomb-boost,"Third consumes once; no added count");
                Attacks(f,f.A,4);Check(active.CastCount==2&&passive.NextBasicReserved,"Fourth begins next cycle");
                Attacks(f,f.A,5);Check(passive.CastCount==2&&!passive.NextBasicReserved,"Fifth consumes next reservation");
                Attacks(f,f.A,6);f.Combat.ResetBattle();Check(!Skill(f.A,"web.justina.passive").NextBasicReserved,"Reset clears runtime reservation");
                f.Combat.StartBattle();Check(!Skill(f.A,"web.justina.passive").NextBasicReserved,"No next-battle leak");
            }
        }
        private static void LethalBasic()
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"justina",1);var same=f.Add("B",1,5);var other=f.Add("B",0,4);
                foreach(var u in f.Units.Skip(1))u.Stats.MaxHealth=100000;
                f.Start();foreach(var u in f.Units.Skip(1))Hold(u);
                Attacks(f,f.A,2);var passive=Skill(f.A,"web.justina.passive");f.B.SetHealth(1);
                Attacks(f,f.A,3);Check(!f.B.IsAlive&&passive.NextBasicReserved&&passive.CastCount==0,"Lethal basic preserves boost");
                int start=f.Events.Count;Attacks(f,f.A,4);var events=f.Events.Skip(start).ToList();
                int consume=events.FindIndex(e=>e.Type==PrototypeCombatEventType.NextAttackConsumed);
                int boost=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.justina.passive");
                int row=events.FindIndex(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.justina.active");
                int reserve=events.FindIndex(e=>e.Type==PrototypeCombatEventType.NextAttackReserved);
                Check(consume>=0&&boost>consume&&row>boost&&reserve>row&&passive.NextBasicReserved&&passive.CastCount==1,"Retarget: old boost -> immediate row -> new boost");
                Attacks(f,f.A,5);Check(passive.CastCount==2&&!passive.NextBasicReserved,"New boost waits exactly one attack");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"justina",1);var same=f.Add("B",1,5);var other=f.Add("B",0,4);
                f.Start();foreach(var u in f.Units.Skip(1))Hold(u);Attacks(f,f.A,1);f.B.SetHealth(1);
                int hp=same.CurrentHealth;Attacks(f,f.A,2);
                Check(!f.B.IsAlive&&same.CurrentHealth==hp-170&&other.CurrentHealth==1000,"Killing basic keeps original row anchor");
            }
        }
        private static void ShopMerge(BattlefieldPrototype board,string id,int star)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=10000;c.ResetRun();foreach(var owned in c.Run.Player.OwnedUnits.ToArray())c.Sell(owned.InstanceId);
            int upgrades=0;while(c.Data.Odds(c.Run.Player.Mastery.Level)[c.Data.Definition(id).Cost-1]==0&&upgrades++<200)Check(c.Invest(),"Unlock real cost odds");
            int required=new[]{1,3,9}[star-1],bought=0;
            for(int roll=0;roll<2000&&bought<required;roll++)
            {for(int slot=0;slot<c.Run.Shop.Count&&bought<required;slot++)if(c.Run.Shop.Slot(slot)==id){Check(c.Buy(slot),"Buy real merge copy");bought++;}if(bought<required)Check(c.Reroll(),"Paid real reroll");}
            Check(bought==required&&c.Run.Player.OwnedUnits.Count==1,"Actual three-copy recursive merge");var ownedUnit=c.Run.Player.OwnedUnits.Single();Check(ownedUnit.Stars==star&&ownedUnit.Location==PrototypeUnitLocation.Bench,"Merged star and bench");
            Check(c.Move(ownedUnit.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Deploy actual merged character and Ready");
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int n=0;n<900;n++)c.Step(.02f);var a=board.Units.Single(u=>u.Faction=="A");var active=Skill(a,"web."+id+".active");var passive=Skill(a,"web."+id+".passive");
            Check(active.CastCount>0&&passive.CastCount>0,"Both real skills execute after actual merge");
            Check(Math.Abs(active.Definition.Effects[0].SkillRatio-new[]{.85,1.1,1.9}[star-1])<.0001,"Merged active coefficient");
            Check(Math.Abs(passive.Definition.Effects[0].SkillRatio-new[]{.45,.6,1}[star-1])<.0001,"Merged passive coefficient");
            c.SetDataMode(PrototypeDataMode.TestFixtures);c.SetDataMode(PrototypeDataMode.WebRoster);
        }
        private static void Simulations()
        {
            for(int seed=0;seed<12;seed++)
            {
                string previous=null;
                for(int repeat=0;repeat<2;repeat++)using(var f=new Fixture(false))
                {
                    var data=PrototypeWebRoster.Load().CreateGameData();
                    for(int row=0;row<3;row++)for(int team=0;team<2;team++)
                    {string id=(row==0&&team==0?"justina":new[]{"hyunwoo","yuki","sua","jenny"}[(row+team+seed)%4]);var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks<3000)
                    {ticks++;f.Step();var cells=new HashSet<Vector2Int>();foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&!double.IsNaN(unit.CombatAttackPower)&&!double.IsInfinity(unit.CombatAttackPower),"Mixed team occupancy/finite stats");}
                    if(f.Combat.State==PrototypeCombatState.Fighting)
                    {
                        Check(ticks==3000&&f.Combat.StallCount==0&&f.Units.Sum(u=>u.Statistics.DamageDealt)>0,"60s active combat, not a silent stall");
                        // Core fixture uses the same deadline operation as PrototypeRunController.
                        f.Combat.FinishAsDraw();timeoutDraws++;
                        Check(f.Combat.Winner==null,"Existing 60s Draw rule");
                    }
                    Check(f.Combat.State==PrototypeCombatState.Finished,"Mixed battle resolves by elimination or existing deadline");string result=f.Combat.Winner+"|"+ticks+"|"+f.Combat.StatisticsSummary();Check(previous==null||previous==result,"Mixed same seed deterministic");previous=result;
                }
            }
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
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Charge smoke: "+message);}
    }
}
