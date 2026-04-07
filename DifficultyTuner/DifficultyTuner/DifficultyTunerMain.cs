using System;
using DaggerfallConnect;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;

namespace DifficultyTuner
{
    public class DifficultyTunerMain : MonoBehaviour
    {
        static Mod mod;
        static DifficultyTunerMain instance;
        static float damageReceivedMult = 1f;
        static float damageDealtMult = 1f;
        static float enemyHealthMult = 1f;

        private enum DifficultyPreset
        {
            VeryEasy,
            Easy,
            Normal,
            Hard,
            VeryHard,
            Extreme,
            Custom,
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject(mod.Title);
            instance = go.AddComponent<DifficultyTunerMain>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            RegisterFormulaOverrides();
            mod.IsReady = true;
        }

        private static void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            DifficultyPreset preset = DifficultyPreset.Normal;
            string presetName = settings.GetValue<string>("General", "DifficultyPreset");

            if (!string.IsNullOrEmpty(presetName))
            {
                switch (presetName.Trim())
                {
                    case "Very Easy":
                        preset = DifficultyPreset.VeryEasy;
                        break;
                    case "Easy":
                        preset = DifficultyPreset.Easy;
                        break;
                    case "Normal":
                        preset = DifficultyPreset.Normal;
                        break;
                    case "Hard":
                        preset = DifficultyPreset.Hard;
                        break;
                    case "Very Hard":
                        preset = DifficultyPreset.VeryHard;
                        break;
                    case "Extreme":
                        preset = DifficultyPreset.Extreme;
                        break;
                    case "Custom":
                        preset = DifficultyPreset.Custom;
                        break;
                    default:
                        preset = DifficultyPreset.Normal;
                        break;
                }
            }

            int damageReceivedPercent;
            int damageDealtPercent;
            int enemyHealthPercent;

            if (preset == DifficultyPreset.Custom)
            {
                damageReceivedPercent = ClampPercent(settings.GetValue<int>("General", "DamageReceivedPercent"));
                damageDealtPercent = ClampPercent(settings.GetValue<int>("General", "DamageDealtPercent"));
                enemyHealthPercent = ClampPercent(settings.GetValue<int>("General", "EnemyHealthPercent"));
            }
            else
            {
                ApplyPreset(preset, out damageReceivedPercent, out damageDealtPercent, out enemyHealthPercent);
            }

            damageReceivedMult = PercentToMultiplier(damageReceivedPercent);
            damageDealtMult = PercentToMultiplier(damageDealtPercent);
            enemyHealthMult = PercentToMultiplier(enemyHealthPercent);
        }

        private static void ApplyPreset(
            DifficultyPreset preset,
            out int damageReceivedPercent,
            out int damageDealtPercent,
            out int enemyHealthPercent)
        {
            damageReceivedPercent = 100;
            damageDealtPercent = 100;
            enemyHealthPercent = 100;

            switch (preset)
            {
                case DifficultyPreset.VeryEasy:
                    damageReceivedPercent = 50;
                    damageDealtPercent = 150;
                    enemyHealthPercent = 75;
                    break;
                case DifficultyPreset.Easy:
                    damageReceivedPercent = 75;
                    damageDealtPercent = 125;
                    enemyHealthPercent = 90;
                    break;
                case DifficultyPreset.Normal:
                    damageReceivedPercent = 100;
                    damageDealtPercent = 100;
                    enemyHealthPercent = 100;
                    break;
                case DifficultyPreset.Hard:
                    damageReceivedPercent = 125;
                    damageDealtPercent = 90;
                    enemyHealthPercent = 115;
                    break;
                case DifficultyPreset.VeryHard:
                    damageReceivedPercent = 150;
                    damageDealtPercent = 80;
                    enemyHealthPercent = 130;
                    break;
                case DifficultyPreset.Extreme:
                    damageReceivedPercent = 200;
                    damageDealtPercent = 65;
                    enemyHealthPercent = 160;
                    break;
                case DifficultyPreset.Custom:
                default:
                    break;
            }
        }

        private static int ClampPercent(int value)
        {
            return Mathf.Clamp(value, 25, 300);
        }

        private static float PercentToMultiplier(int percent)
        {
            return ClampPercent(percent) / 100f;
        }

        private static void RegisterFormulaOverrides()
        {
            FormulaHelper.RegisterOverride<Func<DaggerfallEntity, DaggerfallEntity, bool, int, DaggerfallUnityItem, int>>(
                mod,
                "CalculateAttackDamage",
                CalculateAttackDamageOverride);

            FormulaHelper.RegisterOverride<Func<int, int, int>>(
                mod,
                "RollEnemyClassMaxHealth",
                RollEnemyClassMaxHealthOverride);
        }

        private static int CalculateAttackDamageOverride(
            DaggerfallEntity attacker,
            DaggerfallEntity target,
            bool isEnemyFacingAwayFromPlayer,
            int weaponAnimTime,
            DaggerfallUnityItem weapon)
        {
            if (attacker == null || target == null)
                return 0;

            int damage = 0;
            int skillId = FormulaHelper.CalculateWeaponToHit(attacker, weapon);
            int struckBodyPart = FormulaHelper.CalculateStruckBodyPart();
            int chanceToHitMod = attacker.Skills.GetLiveSkillValue((DFCareer.Skills)skillId);

            chanceToHitMod += FormulaHelper.CalculateRacialModifiers(attacker, target, weapon).toHitMod;
            chanceToHitMod += FormulaHelper.CalculateAdjustmentsToHit(attacker, target);
            chanceToHitMod += FormulaHelper.CalculateWeaponAttackDamage(attacker, target, 0, weaponAnimTime, weapon);
            chanceToHitMod += FormulaHelper.CalculateAdrenalineRushToHit(attacker, target);

            if (FormulaHelper.CalculateSuccessfulHit(attacker, target, chanceToHitMod, struckBodyPart) <= 0)
                return 0;

            int minBaseDamage;
            int maxBaseDamage;

            if (skillId == (short)DFCareer.Skills.HandToHand)
            {
                minBaseDamage = FormulaHelper.CalculateHandToHandMinDamage(attacker.Skills.GetLiveSkillValue(DFCareer.Skills.HandToHand));
                maxBaseDamage = FormulaHelper.CalculateHandToHandMaxDamage(attacker.Skills.GetLiveSkillValue(DFCareer.Skills.HandToHand));
            }
            else
            {
                Weapons weaponType = weapon != null ? weapon.GetWeaponType() : Weapons.None;
                minBaseDamage = FormulaHelper.CalculateWeaponMinDamage(weaponType);
                maxBaseDamage = FormulaHelper.CalculateWeaponMaxDamage(weaponType);
            }

            damage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);

            FormulaHelper.ToHitAndDamageMods raceMods = FormulaHelper.CalculateRacialModifiers(attacker, target, weapon);
            damage += raceMods.damageMod;

            if (attacker == GameManager.Instance.PlayerEntity)
                damage += FormulaHelper.DamageModifier(attacker.Stats.LiveStrength);

            int backstabChance = 0;
            PlayerEntity attackerAsPlayer = attacker as PlayerEntity;
            if (attackerAsPlayer != null)
                backstabChance = FormulaHelper.CalculateBackstabChance(attackerAsPlayer, target, isEnemyFacingAwayFromPlayer);

            damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
            damage = FormulaHelper.AdjustWeaponAttackDamage(attacker, target, damage, weaponAnimTime, weapon);

            if (damage <= 0)
                return 0;

            bool attackerIsPlayer = attacker == GameManager.Instance.PlayerEntity;
            bool targetIsPlayer = target == GameManager.Instance.PlayerEntity;
            float multiplier = 1f;

            if (attackerIsPlayer && !targetIsPlayer)
                multiplier = damageDealtMult;
            else if (!attackerIsPlayer && targetIsPlayer)
                multiplier = damageReceivedMult;

            return Mathf.Max(0, Mathf.RoundToInt(damage * multiplier));
        }

        private static int RollEnemyClassMaxHealthOverride(int level, int hitPointsPerLevel)
        {
            const int baseHealth = 10;
            int vanilla = baseHealth;
            for (int i = 0; i < level; i++)
                vanilla += UnityEngine.Random.Range(1, hitPointsPerLevel + 1);

            return Mathf.Max(1, Mathf.RoundToInt(vanilla * enemyHealthMult));
        }
    }
}
