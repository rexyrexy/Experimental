using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using EloBuddy.SDK.Rendering;
using SharpDX;

namespace ItsRengar
{
    internal class Program
    {
        private static Spell.Active _q, _w;
        private static Spell.Skillshot _e;
        private static AIHeroClient _selectedEnemy;
        public static Menu MainM, ComboM, ClearM, MiscM;
        private static AIHeroClient Rengar => Player.Instance;
        private static bool RengarR => Rengar.HasBuff("RengarR");
        private static int ComboMode => ComboM["mode"].Cast<ComboBox>().CurrentValue;
        private static bool OneShot => ComboMode == 0;
        private static bool Snare => ComboMode == 1;
        private static bool SwitchComboMode => ComboM["keymode"].Cast<KeyBind>().CurrentValue;
        private static int Ferocity => (int) Rengar.Mana;
        private static bool ForceE => ComboM["forcee"].Cast<CheckBox>().CurrentValue;
        private static bool SwitchE => ComboM["switche"].Cast<CheckBox>().CurrentValue;
        private static bool DrawUltiTime => MiscM["ultitimer"].Cast<CheckBox>().CurrentValue;
        private static bool DrawERange => MiscM["drawe"].Cast<CheckBox>().CurrentValue;
        //private static bool Anti => MiscM["anti"].Cast<CheckBox>().CurrentValue;
        private static int AutoHp => MiscM["autohp"].Cast<Slider>().CurrentValue;
        private static bool DrawSelectedMode => MiscM["drawmode"].Cast<CheckBox>().CurrentValue;
        private static bool DrawSelectedTarget => MiscM["drawselectedtarget"].Cast<CheckBox>().CurrentValue;
        private static int Skin => MiscM["skin"].Cast<ComboBox>().CurrentValue;
        private static bool DoingCombo => Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo);
        private static bool DoingLaneC => Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear);
        private static bool DoingJungleC => Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.JungleClear);

        private static void Main()
        {
            Loading.OnLoadingComplete += OnLoadingComplete;
        }

        private static void OnLoadingComplete(EventArgs args)
        {
            if (Rengar.Hero != Champion.Rengar)
                return;

            _q = new Spell.Active(SpellSlot.Q, (uint) Rengar.GetAutoAttackRange());
            _w = new Spell.Active(SpellSlot.W, 500);
            _e = new Spell.Skillshot(SpellSlot.E, (uint) Rengar.Spellbook.GetSpell(SpellSlot.E).SData.CastRange,
                SkillShotType.Linear, (int) 0.25f, 1500, 70)
            {AllowedCollisionCount = 0};


            MainM = MainMenu.AddMenu("| ItsRengar |", "ItsRengarMainM");
            ComboM = MainM.AddSubMenu("| Combo |");
            ClearM = MainM.AddSubMenu("| Clear |");
            MiscM = MainM.AddSubMenu("| Misc |");

            MainM.AddGroupLabel("ItsRengar");
            MainM.AddLabel("Its Rengar | Coded by Rexy");

            ComboM.AddLabel("Combo Settings");
            ComboM.AddSeparator();

            ComboM.Add("mode", new ComboBox("Combo Mode :", 0, "OneShot", "Snare"));
            ComboM.Add("keymode", new KeyBind("Switch Combo Mode", false, KeyBind.BindTypes.HoldActive, 'G'));
            ComboM.AddSeparator();
            ComboM.Add("forcee", new CheckBox("Force E if enemy out of Q range"));
            ComboM.Add("switche", new CheckBox("Switch OneShot mode after E cast when Snare Mode"));

            ClearM.AddLabel("Clear Settings are Usage Q-W-E and Saves Ferocity");

            MiscM.AddLabel("Misc Settings");
            //MiscM.Add("anti", new CheckBox("Anti Gapcloser - Interrupter"));
            MiscM.Add("ultitimer", new CheckBox("Draw Ulti Buff Time"));
            MiscM.Add("drawe", new CheckBox("Draw E range"));
            MiscM.AddLabel("If you don't want to AutoHp set it value to 0");
            MiscM.Add("autohp", new Slider("Auto HP Value", 25));
            MiscM.Add("drawselectedtarget", new CheckBox("Draw Selected Enemy"));
            MiscM.Add("drawmode", new CheckBox("drawmode"));
            MiscM.AddSeparator();

            MiscM.Add("skin",
                new ComboBox("Selected Skin :", 1, "Classic", "Head Hunter", "Night Hunter", "SSW"));

            Game.OnUpdate += OnUpdate;
            Orbwalker.OnPreAttack += OnPreAttack;
            Orbwalker.OnPostAttack += OnPostAttack;
            Dash.OnDash += OnDash;
            Obj_AI_Base.OnSpellCast += OnSpellCast;
            /*Gapcloser.OnGapcloser += OnGapcloser;
            Interrupter.OnInterruptableSpell += OnInterruptableSpell;*/
            Game.OnWndProc += OnWndProc;
            Drawing.OnDraw += OnDraw;
            Game.OnTick += OnTick;

            Chat.Print("ItsRengar | Loaded", Color.White);
        }

        /* private static void OnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs e)
        {
            if (!Anti)
                return;
            if (e.Sender.IsEnemy && e.Sender.Distance(Rengar) < _e.Range)
            CastE(e.Sender);
        }

        private static void OnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs e)
        {
            if (!Anti)
                return;
            if (e.Sender.IsEnemy && e.Sender.Distance(Rengar) < _e.Range)
                CastE(e.Sender);
        }*/

        private static void OnSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
                return;

            if (args.Slot == SpellSlot.Q)
                Orbwalker.ResetAutoAttack();

            if (SwitchE && Snare && args.Slot == SpellSlot.E)
            {
                ComboM["mode"].Cast<ComboBox>().CurrentValue = 0;
            }
        }

        private static void OnTick(EventArgs args)
        {
            if (Rengar.HealthPercent <= AutoHp && _w.IsReady() && Ferocity == 5 && !RengarR)
                _w.Cast();

            if (SwitchComboMode)
            {
                if (OneShot)
                    Core.DelayAction(() => ComboM["mode"].Cast<ComboBox>().CurrentValue = 1, 120);
                else if (Snare)
                    Core.DelayAction(() => ComboM["mode"].Cast<ComboBox>().CurrentValue = 0, 120);
            }

            if (Rengar.SkinId != Skin)
                Rengar.SetSkinId(Skin);
        }

        private static void OnWndProc(WndEventArgs args)
        {
            //
            //Thanks to Nathan / jQuery
            //

            if (args.Msg != (uint) WindowMessages.LeftButtonDown)
            {
                return;
            }

            var unit2 =
                ObjectManager.Get<Obj_AI_Base>()
                    .FirstOrDefault(
                        a =>
                            a.IsValid && a.IsEnemy && a.Distance(Game.CursorPos) < Rengar.BoundingRadius + 1000 &&
                            a.IsValidTarget() && !a.IsMonster && !a.IsMinion && !(a is Obj_AI_Turret));

            if (unit2 != null)
            {
                _selectedEnemy = unit2 as AIHeroClient;
            }
        }

        private static void OnDraw(EventArgs args)
        {
            if (Rengar.IsDead)
                return;

            if (DrawERange)
                Circle.Draw(_e.IsReady() ? Color.Cyan : Color.White, _e.Range, Rengar);

            if (DrawSelectedTarget)
                if (_selectedEnemy.IsValidTarget() && _selectedEnemy.IsVisible)
                    Drawing.DrawText(
                        Drawing.WorldToScreen(_selectedEnemy.Position).X - 40,
                        Drawing.WorldToScreen(_selectedEnemy.Position).Y + 10,
                        System.Drawing.Color.White,
                        "Selected Target");
            var ultibuff = Rengar.GetBuff("RengarR");

            if (ultibuff != null && DrawUltiTime)
            {
                Drawing.DrawText(Drawing.Width*0.70f, Drawing.Height*0.115f, System.Drawing.Color.Aqua,
                    $"{ultibuff.EndTime - Game.Time}", 56);
            }
            if (!DrawSelectedMode) return;
            if (OneShot)
            {
                Drawing.DrawText(
                    Drawing.Width*0.70f,
                    Drawing.Height*0.95f,
                    System.Drawing.Color.White,
                    "OneShot Mode");
            }
            else if (Snare)
            {
                Drawing.DrawText(
                    Drawing.Width*0.70f,
                    Drawing.Height*0.95f,
                    System.Drawing.Color.DarkGoldenrod,
                    "Snare Mode");
            }


            //Thank to jQuery / Nathan for X and Y coordiantes
        }

        private static void OnDash(Obj_AI_Base sender, Dash.DashEventArgs e)
        {
            if (!sender.IsMe)
                return;

            var target = GetTarget(_w.Range);
            if (target != null && (!target.IsMinion || !target.IsMonster))
            {
                var youmuu = new Item(ItemId.Youmuus_Ghostblade);

                if (youmuu.IsReady())
                    youmuu.Cast();
            }

            var targetItem = GetTarget(400f);

            CastItems(targetItem);

            var targetx = GetTarget(_e.Range);

            if (OneShot && Ferocity == 5)
                return;
            if (targetx.IsValidTarget())
                CastE(targetx);
        }

        private static void CastItems(AIHeroClient target)
        {
            if (RengarR)
                return;
            var hydra = new Item(ItemId.Tiamat_Melee_Only, 400f);
            var tiamat = new Item(ItemId.Ravenous_Hydra_Melee_Only, 400f);

            if (target == null)
                return;

            if (hydra.IsReady() && target.Distance(Rengar) < 400)
            {
                hydra.Cast();
            }

            if (tiamat.IsReady() && target.Distance(Rengar) < 400)
            {
                tiamat.Cast();
            }
        }

        private static void OnPostAttack(AttackableUnit target, EventArgs args)
        {
            if (Ferocity == 5 && Snare)
                return;

            if (!(target is AIHeroClient))
                return;

            var youmuu = new Item(ItemId.Youmuus_Ghostblade);

            if (youmuu.IsReady())
                youmuu.Cast();

            var targetQ = GetTarget(_q.Range);

            if (IsValidTarget(targetQ, (int) _q.Range) && _q.IsReady() && Ferocity == 5 && OneShot ||
                Ferocity < 5 && OneShot || Snare)
                _q.Cast();

            CastItems(targetQ);
        }

        private static void OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            if (Ferocity == 5 && Snare)
                return;

            if (!(target is AIHeroClient))
                return;

            var aatarget = GetTarget();

            if (aatarget.NetworkId != target.NetworkId && aatarget.IsValidTarget())
                args.Process = false;

            var targetQ = GetTarget(_q.Range);


            if (IsValidTarget(targetQ, (int) _q.Range) && _q.IsReady())
                _q.Cast();
        }

        private static void OnUpdate(EventArgs args)
        {
            if (Rengar.IsDead)
                return;
            var betaQTarget = GetTarget(1900);
            if (IsValidTarget(betaQTarget, 1000))
            {
                if (RengarR && _q.IsReady() && OneShot)
                    _q.Cast();
            }

            if (DoingCombo)
                Combo();
            if (DoingJungleC && Ferocity < 5)
                JungleClear();
            if (DoingLaneC && Ferocity < 5)
                LaneClear();
        }

        private static void LaneClear()
        {
            var allMinions = EntityManager.MinionsAndMonsters.Get(
                EntityManager.MinionsAndMonsters.EntityType.Minion,
                EntityManager.UnitTeam.Enemy,
                ObjectManager.Player.Position,
                _w.Range,
                false);

            if (Ferocity == 5)
                return;
            if (allMinions == null)
            {
                return;
            }


            var objAiMinions = allMinions as IList<Obj_AI_Minion> ?? allMinions.ToList();
            foreach (var minion in objAiMinions)
            {
                var any = objAiMinions.Any();
                {
                    if (_q.IsReady() && minion.IsValidTarget())
                        _q.Cast();
                    if (_w.IsReady() && minion.IsValidTarget())
                        _w.Cast();
                    if (_e.IsReady() && minion.IsValidTarget())
                        _e.Cast(minion);
                }
            }

            //Thanks Kappa
        }

        private static void JungleClear()
        {
            var jmobs =
                ObjectManager.Get<Obj_AI_Minion>()
                    .OrderBy(m => m.CampNumber)
                    .Where(m => m.IsMonster && m.IsEnemy && !m.IsDead);
            var objAiMinions = jmobs as IList<Obj_AI_Minion> ?? jmobs.ToList();
            if (Ferocity == 5)
                return;
            foreach (var jmob in objAiMinions)
            {
                if (_q.IsReady() && jmob.IsValidTarget(_q.Range))
                    _q.Cast();
                if (_w.IsReady() && jmob.IsValidTarget(_w.Range))
                    _w.Cast();
                if (_e.IsReady() && jmob.IsValidTarget(_e.Range))
                    _e.Cast(jmob);
            }
        }

        private static void Combo()
        {
            if (RengarR)
                return;

            var target = GetTarget();

            if (!IsValidTarget(target, 1250) || target == null)
                return;

            if (OneShot)
            {
                if (Ferocity == 5)
                {
                    CastQ(target);
                }
                else
                {
                    CastE(target);
                    CastW(target);
                    CastItems(target);
                    CastQ(target);
                }
                if (!ForceE) return;
                if (target.Distance(Rengar) > _q.Range && target.Distance(Rengar) < _e.Range && !Rengar.IsDashing())
                    CastE(target);
            }
            if (!Snare) return;
            if (Ferocity == 5)
            {
                CastE(target);
            }
            else
            {
                CastQ(target);
                CastE(target);
                CastW(target);
                CastItems(target);
            }
        }

        private static void CastQ(Obj_AI_Base target)
        {
            var enemy = GetTarget(_q.Range);
            if (target == null)
            {
                if (IsValidTarget(enemy, (int) _q.Range) && _q.IsReady())
                    _q.Cast();
            }
            else
            {
                if (IsValidTarget(target as AIHeroClient, (int) _q.Range) && _q.IsReady())
                    _q.Cast();
            }
        }

        private static void CastW(Obj_AI_Base target)
        {
            if (RengarR)
                return;
            var enemy = GetTarget(_w.Range);

            if (target == null)
            {
                if (IsValidTarget(enemy, (int) _w.Range) && _w.IsReady())
                    _w.Cast();
            }
            else
            {
                if (IsValidTarget(target as AIHeroClient, (int) _w.Range) && _w.IsReady())
                    _w.Cast();
            }
        }

        private static bool IsValidTarget(AIHeroClient target, int range = 500)
        {
            return target != null && target.IsValidTarget(range) && target.Distance(Rengar) < range && target.IsEnemy;
        }

        private static void CastE(AIHeroClient target)
        {
            if (RengarR || Rengar.HasBuff("rengarpassivebuff"))
                return;
            if (IsValidTarget(target, (int) _e.Range))
            {
                var prediction2 = _e.GetPrediction(target);

                if (prediction2.HitChance < HitChance.High || !IsValidTarget(target, (int) _e.Range) ||
                    !_e.IsReady() || target.Distance(Rengar) > _e.Range)
                    return;

                _e.Cast(target);
            }
            else
            {
                var targetE = GetTarget(_e.Range);

                var prediction = _e.GetPrediction(targetE);

                if (prediction.HitChance < HitChance.High || !IsValidTarget(targetE, (int) _e.Range) ||
                    !_e.IsReady() || targetE.Distance(Rengar) > _e.Range)
                    return;

                _e.Cast(targetE);
            }
        }

        private static AIHeroClient GetTarget(float range = 1250)
        {
            return _selectedEnemy ?? TargetSelector.GetTarget(range, DamageType.Physical);
        }
    }
}