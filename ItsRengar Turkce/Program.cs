using System;
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
            ComboM = MainM.AddSubMenu("| Kombo Ayarları |");
            ClearM = MainM.AddSubMenu("| Lane/Jungle Temizleme |");
            MiscM = MainM.AddSubMenu("| Ekstra Ayarlar |");

            MainM.AddGroupLabel("ItsRengar");
            MainM.AddLabel("Its Rengar | Rexy Tarafından Yapılmıştır");

            ComboM.AddLabel("Combo Ayarları");
            ComboM.AddSeparator();

            ComboM.Add("mode", new ComboBox("Combo Modu :", 0, "Tek Atma", "E-Combosu"));
            ComboM.Add("keymode", new KeyBind("Kombo Modunu Kolayca Değiştir", false, KeyBind.BindTypes.HoldActive, 'G'));
            ComboM.AddSeparator();
            ComboM.Add("forcee", new CheckBox("Düşman Q menzilinden \n uzaktaysa E kullanımını zorla"));
            ComboM.Add("switche", new CheckBox("E-Combosu modunda E attıktan sonra \n Tek Atma moduna geç"));

            ClearM.AddLabel("Temizleme ayarları otomatik olarak Q-W-E \n Şeklindedir Artı olarak 5 yükte iken skill kullanmaz");

            MiscM.AddLabel("Ekstra ayarlar");
            MiscM.Add("ultitimer", new CheckBox("Ultinin kalan \n süresini göster"));
			MiscM.AddSeparator();
            MiscM.AddLabel("Eger Otomatik W kullanımı \n istemiyorsanız aşağıdaki değeri 0 yapınız");
            MiscM.Add("autohp", new Slider("Otomatik W kullanımı için yüzdelik hp", 25));
            MiscM.Add("drawselectedtarget", new CheckBox("Seçilen hedefi çiz"));
            MiscM.Add("drawmode", new CheckBox("Combo Mode'unu çiz"));
            MiscM.AddSeparator();

            MiscM.Add("skin",
                new ComboBox("Skin hilesi : :", 1, "Klasik", "Kelle Avcısı", "Gece Avcısı", "SSW"));

            Game.OnUpdate += OnUpdate;
            Orbwalker.OnPreAttack += OnPreAttack;
            Orbwalker.OnPostAttack += OnPostAttack;
            Dash.OnDash += OnDash;
            Obj_AI_Base.OnSpellCast += OnSpellCast;
            Game.OnWndProc += OnWndProc;
            Drawing.OnDraw += OnDraw;
            Game.OnTick += OnTick;

            Chat.Print("ItsRengar | Yüklendi", Color.White);
        }

        private static void OnSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe)
                return;

            if (args.Slot == SpellSlot.Q && GetTarget().IsValidTarget(_q.Range)) { 
                Orbwalker.ResetAutoAttack();
                Player.IssueOrder(GameObjectOrder.AttackTo, GetTarget());
            }

            if (SwitchE && Snare && args.Slot == SpellSlot.E)
            {
                ComboM["mode"].Cast<ComboBox>().CurrentValue = 0;
            }
        }

        private static void OnTick(EventArgs args)
        {
            if (SwitchComboMode)
            {
                if (OneShot)
                    Core.DelayAction(() => ComboM["mode"].Cast<ComboBox>().CurrentValue = 1, 250);
                else if (Snare)
                    Core.DelayAction(() => ComboM["mode"].Cast<ComboBox>().CurrentValue = 0, 250);
            }

            if (Rengar.SkinId != Skin)
                Rengar.SetSkinId(Skin);
        }

        private static void OnWndProc(WndEventArgs args)
        {
            if (args.Msg != (uint) WindowMessages.LeftButtonDown)
            {
                return;
            }

            var unit = EntityManager.Heroes.Enemies
                    .FindAll(hero => hero.IsValidTarget() && hero.Distance(Game.CursorPos, true) < 40000)
                    .OrderBy(h => h.Distance(Game.CursorPos, true)).FirstOrDefault();

            _selectedEnemy = unit;
        }

        private static void OnDraw(EventArgs args)
        {
            if (Rengar.IsDead)
                return;
			
            if (DrawSelectedTarget)
                if (_selectedEnemy.IsValidTarget() && _selectedEnemy.IsVisible)
                    Drawing.DrawText(
                        Drawing.WorldToScreen(_selectedEnemy.Position).X - 40,
                        Drawing.WorldToScreen(_selectedEnemy.Position).Y + 10,
                        System.Drawing.Color.White,
                        "Seçilen hedef");
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
                    "Tek atma modu");
            }
            else if (Snare)
            {
                Drawing.DrawText(
                    Drawing.Width*0.70f,
                    Drawing.Height*0.95f,
                    System.Drawing.Color.DarkGoldenrod,
                    "E-Combosu modu");
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

            if (aatarget.NetworkId != target.NetworkId)
            {
                Orbwalker.ResetAutoAttack();
                args.Process = false;
                Orbwalker.ResetAutoAttack();
            }

            var targetQ = GetTarget(_q.Range);


            if (IsValidTarget(targetQ, (int) _q.Range) && _q.IsReady())
                _q.Cast();
        }

        private static void OnUpdate(EventArgs args)
        {
            if (Rengar.IsDead)
                return;
            if (Rengar.HealthPercent <= AutoHp && _w.IsReady() && Ferocity == 5 && !RengarR)
                _w.Cast();
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
            var laneclearminion =
                EntityManager.MinionsAndMonsters.EnemyMinions.Where(x => x.IsValidTarget(_w.Range))
                    .OrderByDescending(x => x.Health)
                    .FirstOrDefault(x => x != null);

            if (laneclearminion == null || !laneclearminion.IsValidTarget())
                return;
            if (Ferocity == 5)
                return;

            if (_q.IsReady())
                _q.Cast();
            if (_w.IsReady())
                _w.Cast();
            if (_e.IsReady())
                _e.Cast(laneclearminion);
        }

        private static void JungleClear()
        {
            var junglemonsters =
                EntityManager.MinionsAndMonsters.GetJungleMonsters(Rengar.Position, _w.Range)
                    .OrderByDescending(x => x.MaxHealth)
                    .FirstOrDefault(x => x != null);

            if (junglemonsters == null || !junglemonsters.IsValidTarget())
                return;
            if (Ferocity == 5)
                return;
            if(_q.IsReady())
                _q.Cast();
            if (_w.IsReady())
                _w.Cast();
            if (_e.IsReady())
                _e.Cast(junglemonsters);
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
            return !_selectedEnemy.IsValidTarget(range) || _selectedEnemy.IsDead || !_selectedEnemy.IsVisible ? TargetSelector.GetTarget(range, DamageType.Physical) : _selectedEnemy;
        }
    }
}