using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallConnect;
using DaggerfallConnect.Save;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Guilds;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.UserInterface;

namespace HitChance
{
	public class ModLoader
    {
        public static Mod mod;
        public static GameObject ExampleGo;

        [Invoke]
        public static void InitAtStartState(InitParams initParams)
        {
            mod = initParams.Mod;
            Debug.Log("**********Started setup of : " + mod.Title);
            ModManager.Instance.GetComponent<MonoBehaviour>().StartCoroutine(mod.LoadAllAssetsFromBundleAsync(true));
            mod.IsReady = true;

			Func<DaggerfallEntity, DaggerfallEntity, int, int, bool> hit = (a, b, c, d) => {
				Debug.Log("**********Override hit: 100%");
				return true;
			};
			FormulaHelper.RegisterOverride(mod, "CalculateSuccessfulHit", hit);

			Func<DaggerfallEntity, DaggerfallEntity, int, int, DaggerfallUnityItem, int> attack = (a, b, c, d, e) => {
				Debug.Log("**********Override attack");
				int? dam = CalculateAttackDamage(a, b, c, d, e);

                if(dam == null)
                    return 0;
				
				if(dam <= 0)
					dam = 1;
				
				return (int)dam;
			};
			FormulaHelper.RegisterOverride(mod, "CalculateAttackDamage", attack);
        }

        [Invoke(StateManager.StateTypes.Game)]
        public static void InitAtGameState(InitParams initParams)
        {
			Debug.Log("InitAtGameState");
        }

		//0.10.23
        public static int? CalculateAttackDamage(DaggerfallEntity attacker, DaggerfallEntity target, int enemyAnimStateRecord, int weaponAnimTime, DaggerfallUnityItem weapon)
        {
            if (attacker == null || target == null) {
                Debug.Log("attacker, target: " + attacker + "," + target);
                return null;
            }

            int damageModifiers = 0;
            int damage = 0;
            int backstabChance = 0;
            PlayerEntity player = GameManager.Instance.PlayerEntity;
            short skillID = 0;

            // Choose whether weapon-wielding enemies use their weapons or weaponless attacks.
            // In classic, weapon-wielding enemies use the damage values of their weapons
            // instead of their weaponless values.
            // For some enemies this gives lower damage than similar-tier monsters
            // and the weaponless values seems more appropriate, so here
            // enemies will choose to use their weaponless attack if it is more damaging.
            EnemyEntity AIAttacker = attacker as EnemyEntity;
            if (AIAttacker != null && weapon != null)
            {
                int weaponAverage = (weapon.GetBaseDamageMin() + weapon.GetBaseDamageMax()) / 2;
                int noWeaponAverage = (AIAttacker.MobileEnemy.MinDamage + AIAttacker.MobileEnemy.MaxDamage) / 2;

                if (noWeaponAverage > weaponAverage)
                {
                    // Use hand-to-hand
                    weapon = null;
                }
            }

            if (weapon != null)
            {
                // If the attacker is using a weapon, check if the material is high enough to damage the target
                if (target.MinMetalToHit > (WeaponMaterialTypes)weapon.NativeMaterialValue)
                {
                    if (attacker == player)
                    {
                        DaggerfallUI.Instance.PopupMessage(TextManager.Instance.GetLocalizedText("materialIneffective"));
                    }
                    Debug.Log("ineffective");
                    return null;
                }
                // Get weapon skill used
                skillID = weapon.GetWeaponSkillIDAsShort();
            }
            else
            {
                skillID = (short)DFCareer.Skills.HandToHand;
            }

            if (attacker == player)
            {
                // Apply swing modifiers
                FormulaHelper.ToHitAndDamageMods swingMods = FormulaHelper.CalculateSwingModifiers(GameManager.Instance.WeaponManager.ScreenWeapon);
                damageModifiers += swingMods.damageMod;

                // Apply proficiency modifiers
                FormulaHelper.ToHitAndDamageMods proficiencyMods = FormulaHelper.CalculateProficiencyModifiers(attacker, weapon);
                damageModifiers += proficiencyMods.damageMod;

                // Apply racial bonuses
                FormulaHelper.ToHitAndDamageMods racialMods = FormulaHelper.CalculateRacialModifiers(attacker, weapon, player);
                damageModifiers += racialMods.damageMod;

                backstabChance = FormulaHelper.CalculateBackstabChance(player, null, enemyAnimStateRecord);
            }

            // Choose struck body part
            int struckBodyPart = FormulaHelper.CalculateStruckBodyPart();

            // Get damage for weaponless attacks
            if (skillID == (short)DFCareer.Skills.HandToHand)
            {
                if (attacker == player || (AIAttacker != null && AIAttacker.EntityType == EntityTypes.EnemyClass))
                {
					damage = FormulaHelper.CalculateHandToHandAttackDamage(attacker, target, damageModifiers, attacker == player);

					damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);
                }
                else if (AIAttacker != null) // attacker is a monster
                {
                    // Handle multiple attacks by AI
                    int minBaseDamage = 0;
                    int maxBaseDamage = 0;
                    int attackNumber = 0;
                    while (attackNumber < 3) // Classic supports up to 5 attacks but no monster has more than 3
                    {
                        if (attackNumber == 0)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage;
                        }
                        else if (attackNumber == 1)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage2;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage2;
                        }
                        else if (attackNumber == 2)
                        {
                            minBaseDamage = AIAttacker.MobileEnemy.MinDamage3;
                            maxBaseDamage = AIAttacker.MobileEnemy.MaxDamage3;
                        }

                        int reflexesChance = 50 - (10 * ((int)player.Reflexes - 2));

                        if (DFRandom.rand() % 100 < reflexesChance && minBaseDamage > 0)
                        {
                            int hitDamage = UnityEngine.Random.Range(minBaseDamage, maxBaseDamage + 1);
                            // Apply special monster attack effects
                            if (hitDamage > 0)
                                FormulaHelper.OnMonsterHit(AIAttacker, target, hitDamage);

                            damage += hitDamage;
                        }
                        ++attackNumber;
                    }
                }
            }
            // Handle weapon attacks
            else if (weapon != null)
            {
				damage = FormulaHelper.CalculateWeaponAttackDamage(attacker, target, damageModifiers, weaponAnimTime, weapon);

				damage = FormulaHelper.CalculateBackstabDamage(damage, backstabChance);

                // Handle poisoned weapons
                if (damage > 0 && weapon.poisonType != Poisons.None)
                {
                    FormulaHelper.InflictPoison(target, weapon.poisonType, false);
                    weapon.poisonType = Poisons.None;
                }
            }

            damage = Mathf.Max(0, damage);
            Debug.Log("duper: " + damage);

            FormulaHelper.DamageEquipment(attacker, target, damage, weapon, struckBodyPart);

            // Apply Ring of Namira effect
            if (target == player)
            {
                DaggerfallUnityItem[] equippedItems = target.ItemEquipTable.EquipTable;
                DaggerfallUnityItem item = null;
                if (equippedItems.Length != 0)
                {
                    if (IsRingOfNamira(equippedItems[(int)EquipSlots.Ring0]) || IsRingOfNamira(equippedItems[(int)EquipSlots.Ring1]))
                    {
                        IEntityEffect effectTemplate = GameManager.Instance.EntityEffectBroker.GetEffectTemplate(RingOfNamiraEffect.EffectKey);
                        effectTemplate.EnchantmentPayloadCallback(EnchantmentPayloadFlags.None,
                            targetEntity: AIAttacker.EntityBehaviour,
                            sourceItem: item,
                            sourceDamage: damage);
                    }
                }
            }
            //Debug.LogFormat("Damage {0} applied, animTime={1}  ({2})", damage, weaponAnimTime, GameManager.Instance.WeaponManager.ScreenWeapon.WeaponState);

            Debug.Log("damage: " + damage);
            return (int?)damage;
        }

		private static bool IsRingOfNamira(DaggerfallUnityItem item)
        {
            return item != null && item.ContainsEnchantment(DaggerfallConnect.FallExe.EnchantmentTypes.SpecialArtifactEffect, (int)ArtifactsSubTypes.Ring_of_Namira);
        }

    }
}
