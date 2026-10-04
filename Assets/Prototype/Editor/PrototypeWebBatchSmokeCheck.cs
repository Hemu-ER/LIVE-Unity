using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace LIVE.Prototype.Editor
{
    public static class PrototypeWebBatchSmokeCheck
    {
        private static int assertions;
        private static readonly string[] Ids = { "bianca", "garnet", "charlotte" };
        public static void Validate(BattlefieldPrototype board)
        {
            assertions=0;
            var catalog=PrototypeCharacterSkills.Load();
            Check(catalog.Definitions.Count(e=>e.Implemented)==24,"Exactly twelve implemented pairs");
            foreach(var id in Ids)for(int star=1;star<=3;star++)
            {
                var data=PrototypeWebRoster.Load().CreateGameData();var def=data.Definition(id);
                Check(def.Abilities.Skills.Length==2 && catalog.Definition(def.ActiveDefinitionReference).Implemented && catalog.Definition(def.PassiveDefinitionReference).Implemented,"Real definition linked");
                if(id=="bianca")Bianca(star);else if(id=="garnet")Garnet(star);else Charlotte(star);
            }
            foreach(var id in Ids)Shop(board,id);
            Determinism();
            Debug.Log($"WEB_BATCH_SMOKE_CHECK_PASSED: {assertions} assertions; Bianca/Garnet/Charlotte 1-3 stars, timing/threshold boundaries, immediate reactions, true damage, CC, execute/death/occupancy, reduction, area/heal/buff refresh, events, real shop flow, 16 seeded mixed 3v3 simulations.");
        }
        private static void Equip(PrototypeUnit unit,string id,int star)
        {
            unit.ConfigureAbilities(PrototypeWebRoster.Load().CreateGameData().Definition(id).Abilities,star);
            unit.Stats.MaxHealth=10000;unit.Stats.Defense=0;unit.Stats.SkillAmplification=100;unit.Stats.AttackPower=100;unit.Stats.AttackRange=6;
        }
        private static void Hold(PrototypeUnit unit,float seconds=100) => unit.ApplyStatus("test.hold",1,1,seconds,PrototypeStatusKind.CrowdControl);
        private static int Casts(PrototypeUnit unit,string id)=>unit.Skills.Single(s=>s.Definition.Id==id).CastCount;
        private static void Ticks(Fixture f,int count){for(int i=0;i<count;i++)f.Step();}
        private static void Bianca(int star)
        {
            using(var f=new Fixture())
            {
                Equip(f.A,"bianca",star);f.B.Stats.MaxHealth=10000;f.B.Stats.Defense=100;
                f.Start();Hold(f.A);Hold(f.B);Ticks(f,199);Check(Casts(f.A,"web.bianca.active")==0,"Bianca before4");
                f.Step();int expected=PrototypeDamageCalculator.RoundAmount((100*new[]{3.3,4.5,7.5}[star-1]+10000*new[]{.12,.17,.27}[star-1])/2);
                Check(Casts(f.A,"web.bianca.active")==1&&f.B.CurrentHealth==10000-expected,"Bianca4 AMP+maxHP star damage");
                Ticks(f,399);Check(Casts(f.A,"web.bianca.active")==1,"Bianca before12");f.Step();Check(Casts(f.A,"web.bianca.active")==2,"Bianca12 second cast");
                Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.bianca.active")==2,"Bianca skill damage events");
            }
            using(var f=new Fixture())
            {
                Equip(f.A,"bianca",star);f.Start();Hold(f.A);Hold(f.B);
                f.A.ReceiveDamage(4999,f.B);Check(f.A.CurrentHealth==5001&&Casts(f.A,"web.bianca.passive")==0,"Bianca strictly above50");
                f.A.ReceiveDamage(1,f.B);Check(f.A.CurrentHealth==10000&&Casts(f.A,"web.bianca.passive")==1,"Bianca at50 immediately heals");
                f.A.ReceiveDamage(1000,f.B);Check(f.A.CurrentHealth==9900&&f.A.Statistics.DamagePrevented==900,"Bianca immediate90 percent reduction");
                Ticks(f,149);f.A.ReceiveDamage(1000,f.B);Check(f.A.CurrentHealth==9800,"Bianca reduction before3");
                f.Step();f.A.ReceiveDamage(5000,f.B);Check(f.A.CurrentHealth==4800&&Casts(f.A,"web.bianca.passive")==1,"Bianca expires3 and once-only");
                Check(f.Events.Any(e=>e.Type==PrototypeCombatEventType.HealApplied&&e.SkillId=="web.bianca.passive"&&e.Amount==5000),"Bianca heal attribution");
                f.Combat.ResetBattle();f.Combat.StartBattle();f.A.ReceiveDamage(10000,f.B);Check(!f.A.IsAlive&&Casts(f.A,"web.bianca.passive")==0,"Lethal damage does not revive Bianca");
            }
        }
        private static void Garnet(int star)
        {
            double ratio=new[]{.1,.15,.25}[star-1];
            foreach(int above in new[]{0,1})using(var f=new Fixture(false))
            {
                var a=f.Add("A",1,1);var near=f.Add("B",1,2);var far=f.Add("B",0,5);
                Equip(a,"garnet",star);near.Stats.MaxHealth=10000;near.Stats.Defense=100;
                f.Start();Hold(a);Hold(far);near.SetHealth((int)(20000*ratio)+above);
                Check(Casts(a,"web.garnet.active")==0,"Garnet not during StartBattle");
                f.Step();Check(Casts(a,"web.garnet.active")==1,"Garnet first periodic tick");
                var hit=f.Events.Single(e=>e.Type==PrototypeCombatEventType.DamageDealt&&e.SkillId=="web.garnet.active");
                Check(hit.Target==near&&hit.Amount==(int)(10000*ratio),"Nearest target and defense-independent fixed damage");
                if(above==0)
                {
                    Check(!near.IsAlive&&f.Combat.Grid.IsFree(near.Cell)&&a.Statistics.Kills==1&&a.Statistics.Executions==1,"At execute threshold death releases cell");
                    Check(f.Events.Count(e=>e.Type==PrototypeCombatEventType.UnitExecuted)==1&&f.Events.Count(e=>e.Type==PrototypeCombatEventType.UnitDied)==1,"Execution/death exactly once");
                }
                else
                {
                    Check(near.IsAlive&&near.CurrentHealth==(int)(10000*ratio)+1,"One HP above execute survives");
                    Check(Math.Abs(near.CombatDefense-90)<.001&&near.IsControlled,"Defense times.9 and CC");
                    Ticks(f,49);Check(near.IsControlled,"CC before1 second");f.Step();Check(!near.IsControlled,"CC expires exactly1 second");
                    Check(Math.Abs(near.CombatDefense-90)<.001,"Defense reduction remains");
                }
                Hold(near);Ticks(f,5);Check(Casts(a,"web.garnet.active")==1,"Garnet only once");
                int hp=a.CurrentHealth;int reduced=PrototypeDamageCalculator.RoundAmount(100*(1-new[]{.15,.25,.4}[star-1]));
                a.ReceiveDamage(100,far,isBasic:true);Check(a.CurrentHealth==hp-reduced,"Garnet basic reduction star coefficient");
                a.ReceiveDamage(100,far);Check(a.CurrentHealth==hp-reduced-100,"Garnet does not reduce skill damage");
            }
        }
        private static void Charlotte(int star)
        {
            using(var f=new Fixture(false))
            {
                var a=f.Add("A",0,0);var near=f.Add("A",2,2);var far=f.Add("A",2,3);var dead=f.Add("A",1,0);var enemy=f.Add("B",0,5);
                Equip(a,"charlotte",star);a.Stats.AttackSpeed=2;
                foreach(var unit in f.Units)unit.Stats.MaxHealth=10000;
                f.Start();dead.SetHealth(0);Hold(near);Hold(far);Hold(enemy);
                a.SetHealth(5000);near.SetHealth(5000);far.SetHealth(5000);
                int limit=0;while(a.Statistics.BasicAttackCount<2&&limit++<100)f.Step();
                Check(Casts(a,"web.charlotte.passive")==0&&a.CurrentHealth==5000,"Charlotte no passive before third basic");
                while(a.Statistics.BasicAttackCount<3&&limit++<200)f.Step();
                double heal=new[]{.6,.9,1.5}[star-1],buff=new[]{.1,.15,.3}[star-1];
                Check(Casts(a,"web.charlotte.passive")==1,"Third basic triggers immediately");
                Check(a.CurrentHealth==5000+PrototypeDamageCalculator.RoundAmount(100*heal),"Charlotte self heal before own buff");
                Check(near.CurrentHealth==5000+PrototypeDamageCalculator.RoundAmount(100*(1+buff)*heal),"Diagonal distance2 included; live AMP after caster buff");
                Check(far.CurrentHealth==5000&&!dead.IsAlive,"Outside radius and dead allies excluded");
                Check(Math.Abs(a.CombatAttackPower-100*(1+buff))<.001&&Math.Abs(a.EffectiveStats.SkillAmplification-100*(1+buff))<.001,"AP/AMP ratio star coefficient");
                while(a.Statistics.BasicAttackCount<5&&limit++<300)f.Step();Check(Casts(a,"web.charlotte.passive")==1,"No fourth/fifth basic proc");
                while(a.Statistics.BasicAttackCount<6&&limit++<400)f.Step();Check(Casts(a,"web.charlotte.passive")==2,"Sixth basic refresh");
                Check(Math.Abs(a.CombatAttackPower-100*(1+buff))<.001,"Refresh does not stack");
                Hold(a);Ticks(f,149);Check(a.CombatAttackPower>100,"Buff active before3 after refresh");f.Step();Check(Math.Abs(a.CombatAttackPower-100)<.001,"Buff expires3 after refresh");
                while(f.Combat.ElapsedSeconds<9.97)f.Step();Check(Casts(a,"web.charlotte.active")==0,"Charlotte before10");
                while(f.Combat.ElapsedSeconds<9.999)f.Step();Check(Casts(a,"web.charlotte.active")==1,"Charlotte at10");
                foreach(var unit in new[]{a,near,far})
                {int hp=unit.CurrentHealth;unit.ReceiveDamage(123,enemy);Check(unit.CurrentHealth==hp&&unit.HasStatus(PrototypeStatusKind.Invulnerable),"All living allies invulnerable including outside heal radius");}
                Check(!enemy.HasStatus(PrototypeStatusKind.Invulnerable)&&!dead.HasStatus(PrototypeStatusKind.Invulnerable),"Enemy and dead excluded");
                Ticks(f,49);Check(a.HasStatus(PrototypeStatusKind.Invulnerable),"Invulnerability before1");f.Step();Check(!a.HasStatus(PrototypeStatusKind.Invulnerable),"Invulnerability expires1");
                while(f.Combat.ElapsedSeconds<19.999)f.Step();Check(Casts(a,"web.charlotte.active")==2,"Charlotte repeats20");
                Check(f.Events.Any(e=>e.Type==PrototypeCombatEventType.BuffApplied&&e.SkillId=="web.charlotte.passive")&&f.Events.Any(e=>e.Type==PrototypeCombatEventType.StatusApplied&&e.SkillId=="web.charlotte.active"),"Buff/status attribution");
            }
        }
        private static void Shop(BattlefieldPrototype board,string id)
        {
            var c=board.RunController;c.SetDataMode(PrototypeDataMode.WebRoster);c.Data.Rules.StartingCredits=10000;c.ResetRun();
            foreach(var owned in c.Run.Player.OwnedUnits.ToArray())c.Sell(owned.InstanceId);
            bool bought=false;
            for(int roll=0;roll<1500&&!bought;roll++)
            {for(int slot=0;slot<c.Run.Shop.Count;slot++)if(c.Run.Shop.Slot(slot)==id){bought=c.Buy(slot);break;}if(!bought)c.Reroll();}
            Check(bought,"Real shop purchase "+id);var ownedUnit=c.Run.Player.OwnedUnits.Single();Check(ownedUnit.Location==PrototypeUnitLocation.Bench,"Purchased real unit enters bench");
            Check(c.Move(ownedUnit.InstanceId,PrototypeUnitLocation.Board,1,1)&&c.Ready(),"Deploy and Ready "+id);
            foreach(var unit in board.Units){unit.Stats.MaxHealth=100000;unit.SetHealth(100000);}
            for(int tick=0;tick<600;tick++)
            {c.Step(.02f);if(tick==60&&id=="bianca"){var b=board.Units.Single(u=>u.Faction=="A");b.ReceiveDamage(50001,null);}}
            var a=board.Units.Single(u=>u.Faction=="A");Check(Casts(a,"web."+id+".active")>0&&Casts(a,"web."+id+".passive")>0,"Actual purchased character uses both skills "+id);
            c.SetDataMode(PrototypeDataMode.TestFixtures);c.SetDataMode(PrototypeDataMode.WebRoster);
        }
        private static void Determinism()
        {
            for(int seed=0;seed<8;seed++)
            {
                string previous=null;
                for(int repeat=0;repeat<2;repeat++)using(var f=new Fixture(false))
                {
                    var data=PrototypeWebRoster.Load().CreateGameData();
                    for(int row=0;row<3;row++)for(int team=0;team<2;team++)
                    {string id=Ids[(row+team+seed)%3];var unit=f.Add(team==0?"A":"B",row,team==0?1:4,data.CombatStats(id,1+seed%3));unit.ConfigureAbilities(data.Definition(id).Abilities,1+seed%3);}
                    f.Combat.Initialize(3,6,f.Units,BattlefieldPrototype.Position,seed);f.Combat.StartBattle();int ticks=0;
                    while(f.Combat.State==PrototypeCombatState.Fighting&&ticks++<3000)
                    {f.Step();var cells=new HashSet<Vector2Int>();foreach(var unit in f.Units)if(unit.IsAlive)Check(cells.Add(unit.Cell)&&!double.IsNaN(unit.CombatAttackPower)&&!double.IsInfinity(unit.CombatAttackPower),"Mixed team occupancy/finite stats");}
                    Check(f.Combat.State==PrototypeCombatState.Finished,"Mixed battle finishes");string result=f.Combat.Winner+"|"+ticks+"|"+f.Combat.StatisticsSummary();Check(previous==null||previous==result,"Mixed same seed deterministic");previous=result;
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
        private static void Check(bool value,string message){assertions++;if(!value)throw new Exception("Web batch smoke: "+message);}
    }
}
