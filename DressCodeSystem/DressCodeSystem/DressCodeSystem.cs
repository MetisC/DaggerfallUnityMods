using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;
using DaggerfallWorkshop.Game.Utility;
using System;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Serialization;
using System.Collections;

namespace DressCodeSystem
{

    public class DressCodeSystem : MonoBehaviour
    {
        static Mod mod;

        private bool wasInInventory = false;

        #region Clothing Data

        // Player dress state constants
        const int DRESS_FULLY_NUDE = 0;

        const int DRESS_COMMONER_TOPLESS = 1;
        const int DRESS_COMMONER_BOTTOMLESS = 2;
        const int DRESS_COMMONER_FULL = 3;
        const int DRESS_COMMONER_CAPE_ONLY = 4;
        const int DRESS_UNDERWEAR_ONLY = 5;
        const int DRESS_PRISONER_LOOK = 6;

        const int DRESS_NOBLE_FULL = 7;
        const int DRESS_NOBLE_INDECENT_TOPLESS = 8;
        const int DRESS_NOBLE_INDECENT_BOTTOMLESS = 9;
        const int DRESS_NOBLE_CAPE_ONLY = 10;

        const int DRESS_ARMORED_ONLY = 11;
        const int DRESS_BATTLE_READY = 12;
        const int DRESS_OVERDRESSED = 13;
        const int DRESS_IMPROPER = 14;
        const int DRESS_MISMATCHED_STYLES = 15;
        const int DRESS_FESTIVAL_ATTIRE = 16;
        const int DRESS_RELIGIOUS_GARB = 17;
        const int DRESS_MAGICAL_ATTIRE = 18;

        const int DRESS_ARMORED_TOPLESS = 19;
        const int DRESS_ARMORED_BOTTOMLESS = 20;

        #endregion

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<DressCodeSystem>();
            // SOLO PARA PRUEBAS !!
            //StartGameBehaviour.OnStartGame += StartSaver_OnStartGame;

        }

        private Coroutine clothingChecker;

        void Start()
        {
            clothingChecker = StartCoroutine(CheckClothingRoutine());
        }

        IEnumerator CheckClothingRoutine()
        {
            while (true)
            {
                if (GameManager.Instance.IsPlayingGame())
                {
                    UpdateClothingState();
                }

                yield return new WaitForSeconds(5f);
            }
        }


        void OnDestroy()
        {
            if (clothingChecker != null) StopCoroutine(clothingChecker);
        }

        void Awake()
        {
            mod.IsReady = true;
            Debug.LogFormat("[Dress Code System] Ready");
            DaggerfallUI.UIManager.OnWindowChange += UIManager_OnWindowChange;
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
        }

        private void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            UpdateClothingState();
        }

        private void UIManager_OnWindowChange(object sender, EventArgs e)
        {
            var currentWindow = DaggerfallUI.UIManager.TopWindow;

            if (currentWindow is DaggerfallInventoryWindow)
            {
                wasInInventory = true;
            }
            else if (wasInInventory)
            {
                if (currentWindow is DaggerfallMessageBox)
                {
                    Debug.LogFormat("[DressCodeSystem] Skipped UpdateClothingState due to message box.");
                    return;
                }
                Debug.LogFormat("[DressCodeSystem] Inventory just closed. Updating clothing state.");
                wasInInventory = false;
                UpdateClothingState();
            }
        }

        private static void StartSaver_OnStartGame(object sender, EventArgs e)
        {
            Debug.LogFormat("[Start Saver] Starting");
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            string saveName = "PPPPPPPP-2";
            GameManager.Instance.SaveLoadManager.Save(playerEntity.Name, saveName);
            Debug.LogFormat("[Start Saver] Game Saved");
            DaggerfallUI.MessageBox("PO");
        }

        // Estado de vestimenta
        int clothingState = -1;

        // Tipos de prendas adicionales
        int footwearType = -1;

        private void UpdateClothingState()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            var equipment = player.ItemEquipTable;

            // Partes del cuerpo
            var chestArmor = equipment.GetItem(EquipSlots.ChestArmor);
            var chestClothes = equipment.GetItem(EquipSlots.ChestClothes);
            var cloak1 = equipment.GetItem(EquipSlots.Cloak1);
            var cloak2 = equipment.GetItem(EquipSlots.Cloak2);

            var legArmor = equipment.GetItem(EquipSlots.LegsArmor);
            var legClothes = equipment.GetItem(EquipSlots.LegsClothes);

            var boots = equipment.GetItem(EquipSlots.Feet);
            var gloves = equipment.GetItem(EquipSlots.Gloves);

            // ¿Lleva algo en cada zona principal?
            bool hasUpper = chestArmor != null || chestClothes != null || cloak1 != null || cloak2 != null;
            bool hasLower = legArmor != null || legClothes != null;

            // Determinar estado de vestimenta
            if (!hasUpper && !hasLower)
            {
                clothingState = DRESS_FULLY_NUDE;
                //DaggerfallUI.MessageBox("FULLY NUDE");
            }

            int nobleJewelCount = CountNobleJewelsEquipped(equipment);

            DaggerfallUI.AddHUDText($"[DressCode - DEBUG] Joyas nobles equipadas: {nobleJewelCount}");

            DaggerfallUI.AddHUDText($"[DressCode - DEBUG] clothingState = {clothingState}, footwearType = {footwearType}");
        }

        const int CLOTHING_TYPE_COMMONER = 0;
        const int CLOTHING_TYPE_NOBLE = 1;
        const int CLOTHING_TYPE_ARMOR = 2;

        private int GetClothingClass(DaggerfallUnityItem item)
        {
            if (item.ItemGroup == ItemGroups.Armor)
                return CLOTHING_TYPE_ARMOR;

            if (item.ItemGroup == ItemGroups.MensClothing)
            {
                MensClothing mens = (MensClothing)item.TemplateIndex;
                switch (mens)
                {
                    case MensClothing.Formal_tunic:
                    case MensClothing.Toga:
                    case MensClothing.Formal_cloak:
                    case MensClothing.Dwynnen_surcoat:
                        return CLOTHING_TYPE_NOBLE;

                    default:
                        return CLOTHING_TYPE_COMMONER;
                }
            }

            if (item.ItemGroup == ItemGroups.WomensClothing)
            {
                WomensClothing womens = (WomensClothing)item.TemplateIndex;
                switch (womens)
                {
                    case WomensClothing.Evening_gown:
                    case WomensClothing.Day_gown:
                    case WomensClothing.Formal_cloak:
                    case WomensClothing.Formal_eodoric:
                        return CLOTHING_TYPE_NOBLE;

                    default:
                        return CLOTHING_TYPE_COMMONER;
                }
            }

            // Si llega aquí, tratamos cualquier otra cosa como commoner por defecto
            return CLOTHING_TYPE_COMMONER;
        }

        #region Clothing clasification

        const int GARMENT_TYPE_COMMON = 0;
        const int GARMENT_TYPE_NOBLE = 1;
        const int GARMENT_TYPE_COMMON_FULL = 2;
        const int GARMENT_TYPE_NOBLE_FULL = 3;
        const int GARMENT_TYPE_ARMOR = 4;
        const int GARMENT_TYPE_RELIGIOUS = 5;
        const int GARMENT_TYPE_UNDERWEAR = 6;
        const int GARMENT_TYPE_WEIRD = 7;
        const int GARMENT_TYPE_COMMON_ACCEPTABLE = 8; // Common clothing that is acceptable in noble outfits

        #endregion

        #region Garment Category

        int GetGarmentCategory(DaggerfallUnityItem item)
        {
            if (item.ItemGroup == ItemGroups.Armor)
                return GARMENT_TYPE_ARMOR;

            if (item.ItemGroup == ItemGroups.MensClothing)
            {
                MensClothing mens = (MensClothing)item.TemplateIndex;
                switch (mens)
                {
                    // WEIRD (decorative, incomplete or exotic)
                    case MensClothing.Straps:
                    case MensClothing.Challenger_Straps:
                    case MensClothing.Champion_straps:
                    case MensClothing.Armbands:
                    case MensClothing.Fancy_Armbands:
                    case MensClothing.Wrap:
                    case MensClothing.Khajiit_suit:
                    case MensClothing.Sash:
                    case MensClothing.Eodoric:
                        return GARMENT_TYPE_WEIRD;

                    // UNDERWEAR
                    case MensClothing.Loincloth:
                        return GARMENT_TYPE_UNDERWEAR;

                    // RELIGIOUS
                    case MensClothing.Priest_robes:
                        return GARMENT_TYPE_RELIGIOUS;

                    // NOBLE (formal but incomplete)
                    case MensClothing.Formal_cloak:
                    case MensClothing.Formal_tunic:
                    case MensClothing.Dwynnen_surcoat:
                    case MensClothing.Anticlere_Surcoat:
                        return GARMENT_TYPE_NOBLE;

                    // NOBLE FULL (full-body noble)
                    case MensClothing.Toga:
                        return GARMENT_TYPE_NOBLE_FULL;

                    // COMMON FULL (full-body coverage)
                    case MensClothing.Plain_robes:
                        return GARMENT_TYPE_COMMON_FULL;

                    // COMMON clothing that is also socially acceptable in noble attire
                    case MensClothing.Breeches:
                    case MensClothing.Casual_pants:
                    case MensClothing.Long_Skirt:
                    case MensClothing.Vest:
                    case MensClothing.Kimono:
                    case MensClothing.Reversible_tunic:
                    case MensClothing.Long_shirt_closed_top2:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // COMMON (standard, not acceptable in noble contexts)
                    case MensClothing.Casual_cloak:
                    case MensClothing.Short_skirt:
                    case MensClothing.Short_tunic:
                    case MensClothing.Short_shirt:
                    case MensClothing.Short_shirt_with_belt:
                    case MensClothing.Long_shirt:
                    case MensClothing.Long_shirt_with_belt:
                    case MensClothing.Short_shirt_closed_top:
                    case MensClothing.Long_shirt_closed_top:
                    case MensClothing.Open_Tunic:
                    case MensClothing.Short_shirt_unchangeable:
                    case MensClothing.Long_shirt_unchangeable:
                        return GARMENT_TYPE_COMMON;

                    // FOOTWEAR
                    case MensClothing.Shoes:
                    case MensClothing.Tall_Boots:
                    case MensClothing.Boots:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;
                    case MensClothing.Sandals:
                        return GARMENT_TYPE_COMMON;

                    default:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;
                }
            }

            if (item.ItemGroup == ItemGroups.WomensClothing)
            {
                WomensClothing womens = (WomensClothing)item.TemplateIndex;
                switch (womens)
                {
                    // WEIRD (decorativa, incompleta o exótica)
                    case WomensClothing.Wrap:
                    case WomensClothing.Khajiit_suit:
                    case WomensClothing.Eodoric:
                        return GARMENT_TYPE_WEIRD;

                    // UNDERWEAR
                    case WomensClothing.Brassier:
                    case WomensClothing.Formal_brassier:
                    case WomensClothing.Loincloth:
                        return GARMENT_TYPE_UNDERWEAR;

                    // RELIGIOUS
                    case WomensClothing.Priestess_robes:
                        return GARMENT_TYPE_RELIGIOUS;

                    // NOBLE (formal pero incompleto)
                    case WomensClothing.Formal_cloak:
                    case WomensClothing.Formal_eodoric:
                    case WomensClothing.Evening_gown:
                        return GARMENT_TYPE_NOBLE;

                    // NOBLE FULL (vestido largo o conjunto completo noble)
                    case WomensClothing.Day_gown:
                    case WomensClothing.Strapless_dress:
                        return GARMENT_TYPE_NOBLE_FULL;

                    // COMMON FULL (cubre torso y piernas)
                    case WomensClothing.Plain_robes:
                    case WomensClothing.Casual_dress:
                        return GARMENT_TYPE_COMMON_FULL;

                    // COMMON aceptable para nobles
                    case WomensClothing.Casual_pants:
                    case WomensClothing.Long_skirt:
                    case WomensClothing.Tights:
                    case WomensClothing.Vest:
                    case WomensClothing.Open_tunic:
                    case WomensClothing.Long_shirt_belt:
                    case WomensClothing.Short_shirt_belt:
                    case WomensClothing.Short_shirt_closed_belt:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // COMMON (no aceptable para nobles)
                    case WomensClothing.Casual_cloak:
                    case WomensClothing.Peasant_blouse:
                    case WomensClothing.Short_shirt:
                    case WomensClothing.Long_shirt:
                    case WomensClothing.Short_shirt_closed:
                    case WomensClothing.Long_shirt_closed:
                    case WomensClothing.Long_shirt_closed_belt:
                    case WomensClothing.Short_shirt_unchangeable:
                    case WomensClothing.Long_shirt_unchangeable:
                        return GARMENT_TYPE_COMMON;

                    // FOOTWEAR
                    case WomensClothing.Shoes:
                    case WomensClothing.Tall_boots:
                    case WomensClothing.Boots:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;
                    case WomensClothing.Sandals:
                        return GARMENT_TYPE_COMMON;

                    default:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;
                }
            }

            // If not in any list likely a mod outfit, acceptable to everyone.
            return GARMENT_TYPE_COMMON_ACCEPTABLE;
        }

        #endregion

        #region Jewel Count

        int CountNobleJewelsEquipped(ItemEquipTable equipTable)
        {
            int count = 0;

            EquipSlots[] jewelSlots = new EquipSlots[]
            {
                EquipSlots.Amulet0, EquipSlots.Amulet1,
                EquipSlots.Bracer0, EquipSlots.Bracer1,
                EquipSlots.Bracelet0, EquipSlots.Bracelet1,
                EquipSlots.Ring0, EquipSlots.Ring1,
            };

            foreach (var slot in jewelSlots)
            {
                var item = equipTable.GetItem(slot);
                if (item == null || item.ItemGroup != ItemGroups.Jewellery)
                    continue;

                // Filtramos solo las nobles
                Jewellery jewellery = (Jewellery)item.TemplateIndex;
                switch (jewellery)
                {
                    case Jewellery.Amulet:
                    case Jewellery.Bracer:
                    case Jewellery.Ring:
                    case Jewellery.Bracelet:
                    case Jewellery.Torc:
                        count++;
                        break;
                }
            }

            return count;
        }

        #endregion

        #region Debug Utility

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                GiveAllClothingAndJewels();
                //DaggerfallUI.AddHUDText("DEBUG: Ropa y joyas añadidas", 4f);
            }
        }

        void GiveAllClothingAndJewels()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            bool isPlayerFemale = player.Gender == Genders.Female;

            DaggerfallUI.AddHUDText("[DressCode - DEBUG] Adding all equipable items...");

            if (isPlayerFemale)
            {
                foreach (WomensClothing womens in Enum.GetValues(typeof(WomensClothing)))
                {
                    var item = ItemBuilder.CreateItem(ItemGroups.WomensClothing, (int)womens);
                    player.Items.AddItem(item);
                }
            }
            else
            {
                foreach (MensClothing mens in Enum.GetValues(typeof(MensClothing)))
                {
                    var item = ItemBuilder.CreateItem(ItemGroups.MensClothing, (int)mens);
                    player.Items.AddItem(item);
                }
            }

            // Añadir joyas
            foreach (Jewellery jewel in Enum.GetValues(typeof(Jewellery)))
            {

                        var item = ItemBuilder.CreateItem(ItemGroups.Jewellery, (int)jewel);
                        player.Items.AddItem(item);
            }

            // Añadir objetos religiosos si quieres también probar esos
            foreach (ReligiousItems relic in Enum.GetValues(typeof(ReligiousItems)))
            {
                var item = ItemBuilder.CreateItem(ItemGroups.ReligiousItems, (int)relic);
                player.Items.AddItem(item);
            }

            // Add all armor pieces in all materials
            foreach (DaggerfallWorkshop.Game.Items.Armor armorType in Enum.GetValues(typeof(DaggerfallWorkshop.Game.Items.Armor)))
            {
                foreach (ArmorMaterialTypes material in Enum.GetValues(typeof(ArmorMaterialTypes)))
                {
                    if (material == ArmorMaterialTypes.None)
                        continue;

                    var item = ItemBuilder.CreateArmor(player.Gender, player.Race, armorType, material);
                    player.Items.AddItem(item);
                }
            }

            // Add all weapons in Daedric material
            foreach (Weapons weapon in Enum.GetValues(typeof(Weapons)))
            {
                // Skip arrows (they're ammo, not equipable weapons)
                if (weapon == Weapons.Arrow)
                    continue;

                var item = ItemBuilder.CreateWeapon(weapon, WeaponMaterialTypes.Daedric);
                player.Items.AddItem(item);
            }

            DaggerfallUI.AddHUDText("[DressCode - DEBUG] Added!", 4f);
        }

        #endregion

    }
}