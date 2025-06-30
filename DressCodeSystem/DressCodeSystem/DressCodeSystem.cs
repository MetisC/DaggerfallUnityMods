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

        #region Clothing Data Const

        // Dress state constants (final set)
        const int DRESS_FULLY_NUDE = 0;
        const int DRESS_UNDERWEAR_ONLY = 1;
        const int DRESS_COMMONER_TOPLESS = 2;
        const int DRESS_COMMONER_BOTTOMLESS = 3;
        const int DRESS_ARMORED_TOPLESS = 4;
        const int DRESS_ARMORED_BOTTOMLESS = 5;
        const int DRESS_COMMONER_FULL = 6;
        const int DRESS_NOBLE_FULL = 7;
        const int DRESS_ARMORED_ONLY = 8;
        const int DRESS_BATTLE_READY = 9;
        const int DRESS_RELIGIOUS_GARB = 10;
        const int DRESS_NOBLE_INDECENT = 11;
        const int DRESS_NOBLE_NO_JEWELS = 12;
        const int DRESS_NOBLE_BATTLE_READY = 13;

        #endregion

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<DressCodeSystem>();
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
                    //UpdateClothingState();
                    // PENDIENTE PENDIENTE  PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE PENDIENTE
                    // OJO ESTO COMPRUEBA CADA 5 SEGUNDOS, en PRODUCCION LO QUIERO !!
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
            StartGameBehaviour.OnStartGame += StartSaver_OnStartGame;
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
                    // Not closed, just a messagebox
                    return;
                }
                // Inventory closed
                wasInInventory = false;
                UpdateClothingState();
            }
        }

        private void StartSaver_OnStartGame(object sender, EventArgs e)
        {
            UpdateClothingState();
        }

        // Estado de vestimenta
        int clothingState = -1;

        // Tipos de prendas adicionales
        int footwearType = -1;

        // ***************************************************************************************************************************
        // ***************************************************************************************************************************
        // ***************************************************************************************************************************

        private void UpdateClothingState()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            var equipment = player.ItemEquipTable;

            // Evaluate dress code using the updated routine
            clothingState = EvaluateDressCode();

            // Translate state to readable string
            string clothingStateText;

            switch (clothingState)
            {
                case DRESS_FULLY_NUDE:
                    clothingStateText = "Fully Nude";
                    break;
                case DRESS_UNDERWEAR_ONLY:
                    clothingStateText = "Wearing Only Underwear";
                    break;
                case DRESS_COMMONER_TOPLESS:
                    clothingStateText = "Commoner Topless";
                    break;
                case DRESS_COMMONER_BOTTOMLESS:
                    clothingStateText = "Commoner Bottomless";
                    break;
                case DRESS_ARMORED_TOPLESS:
                    clothingStateText = "Armored Topless";
                    break;
                case DRESS_ARMORED_BOTTOMLESS:
                    clothingStateText = "Armored Bottomless";
                    break;
                case DRESS_COMMONER_FULL:
                    clothingStateText = "Commoner Full Dress";
                    break;
                case DRESS_NOBLE_FULL:
                    clothingStateText = "Noble Full Dress";
                    break;
                case DRESS_ARMORED_ONLY:
                    clothingStateText = "Only Wearing Armor";
                    break;
                case DRESS_BATTLE_READY:
                    clothingStateText = "Battle-Ready";
                    break;
                case DRESS_RELIGIOUS_GARB:
                    clothingStateText = "Religious Garb";
                    break;
                case DRESS_NOBLE_INDECENT:
                    clothingStateText = "Indecent Noble Dress";
                    break;
                case DRESS_NOBLE_NO_JEWELS:
                    clothingStateText = "Noble Dress Without Jewels";
                    break;
                case DRESS_NOBLE_BATTLE_READY:
                    clothingStateText = "Noble Battle Ready";
                    break;

                default:
                    clothingStateText = $"Unknown State ({clothingState})";
                    break;
            }


            DaggerfallUI.AddHUDText($"[DressCode] Current State: {clothingStateText}", 5f);

            /*
            // Optional debug block (uncomment for deep trace)
            var slotNames = new (EquipSlots Slot, string Name)[]
            {
                (EquipSlots.ChestClothes, "ChestClothes"),
                (EquipSlots.ChestArmor, "ChestArmor"),
                (EquipSlots.LegsClothes, "LegsClothes"),
                (EquipSlots.LegsArmor, "LegsArmor"),
                (EquipSlots.Feet, "Feet"),
                (EquipSlots.Gloves, "Gloves"),
                (EquipSlots.Cloak1, "Cloak1"),
                (EquipSlots.Cloak2, "Cloak2"),
                (EquipSlots.Head, "Head"),
                (EquipSlots.Amulet0, "Amulet0"),
                (EquipSlots.Amulet1, "Amulet1"),
                (EquipSlots.Ring0, "Ring0"),
                (EquipSlots.Ring1, "Ring1"),
                (EquipSlots.Bracelet0, "Bracelet0"),
                (EquipSlots.Bracelet1, "Bracelet1"),
                (EquipSlots.Bracer0, "Bracer0"),
                (EquipSlots.Bracer1, "Bracer1"),
            };

            var block1 = new System.Text.StringBuilder();
            var block2 = new System.Text.StringBuilder();
            var block3 = new System.Text.StringBuilder();

            block1.AppendLine("[DressCode - DEBUG] Equipped Items (1/3):");
            block2.AppendLine("[DressCode - DEBUG] Equipped Items (2/3):");

            for (int i = 0; i < slotNames.Length; i++)
            {
                var (slot, name) = slotNames[i];
                var item = equipment.GetItem(slot);
                string line = item != null
                    ? $"{name}: {item.LongName} (Category {GetGarmentCategory(item)})"
                    : $"{name}: [none]";

                if (i < 8)
                    block1.AppendLine(line);
                else
                    block2.AppendLine(line);
            }

            int nobleJewelCount = CountNobleJewelsEquipped(equipment);

            block3.AppendLine("[DressCode - DEBUG] Summary:");
            block3.AppendLine($"Noble jewels equipped: {nobleJewelCount}");
            block3.AppendLine($"Clothing state: {clothingState}");
            block3.AppendLine($"Final label: {clothingStateText}");

            DaggerfallUI.AddHUDText(block1.ToString(), 15f);
            DaggerfallUI.AddHUDText(block2.ToString(), 15f);
            DaggerfallUI.AddHUDText(block3.ToString(), 15f);
            */
        }



        // ---------------------------------------------------------------
        #region Clothing evaluation

        private int EvaluateDressCode()
        {
            var player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return DRESS_FULLY_NUDE;

            var table = player.ItemEquipTable;

            var chestClothes = table.GetItem(EquipSlots.ChestClothes);
            var chestArmor = table.GetItem(EquipSlots.ChestArmor);
            var legClothes = table.GetItem(EquipSlots.LegsClothes);
            var legArmor = table.GetItem(EquipSlots.LegsArmor);

            var hasChestArmor = chestArmor != null;
            var hasLegArmor = legArmor != null;

            var isTopCovered = IsTopCovered(chestClothes ?? chestArmor, legClothes ?? legArmor);
            var isBottomCovered = IsBottomCovered(legClothes ?? legArmor, chestClothes ?? chestArmor);
            var isFullyCovered = isTopCovered && isBottomCovered;

            bool hasArmor = hasChestArmor || hasLegArmor;
            bool hasClothesTop = HasClothesOnTop(table);
            bool hasClothesBottom = HasClothesOnBottom(table);
            bool onlyUnderwear = IsWearingOnlyUnderwear(table);
            bool hasReligious = HasVisibleReligious(table);
            int nobleJewels = CountNobleJewelsEquipped(table);
            bool hasNoble = HasNobleOutfit(chestClothes ?? chestArmor, legClothes ?? legArmor);
            if (HasOnlyNobleCloak(table))
                return DRESS_NOBLE_INDECENT;

            bool topIsNobleOnly = IsNobleButNotFull(chestClothes ?? chestArmor);
            bool bottomIsNobleOnly = IsNobleButNotFull(legClothes ?? legArmor);
            bool hasSoloNoble = (topIsNobleOnly && !HasClothesOnBottom(table) && legArmor == null)
                             || (bottomIsNobleOnly && !HasClothesOnTop(table) && chestArmor == null);

            if (hasSoloNoble)
                return DRESS_NOBLE_INDECENT;


            if (!isTopCovered && !isBottomCovered)
                return DRESS_FULLY_NUDE;

            if (onlyUnderwear)
                return DRESS_UNDERWEAR_ONLY;

            // Noble + armadura, con joyas -> Noble Battle Ready
            if (hasNoble)
            {
                if (isFullyCovered)
                {
                    if (hasArmor)
                    {
                        if (nobleJewels >= 1)
                            return DRESS_NOBLE_BATTLE_READY;
                        else
                            return DRESS_NOBLE_NO_JEWELS;
                    }
                    else
                    {
                        if (nobleJewels >= 1)
                            return DRESS_NOBLE_FULL;
                        else
                            return DRESS_NOBLE_NO_JEWELS;
                    }
                }
                else
                {
                    bool topIsNoble = IsNobleCategory(chestClothes ?? chestArmor);
                    bool bottomIsNoble = IsNobleCategory(legClothes ?? legArmor);

                    if (topIsNoble || bottomIsNoble)
                        return DRESS_NOBLE_INDECENT;

                    if (topIsNoble || bottomIsNoble || HasOnlyNobleCloak(table))
                        return DRESS_NOBLE_INDECENT;
                }
            }

            if (!isTopCovered && !hasArmor)
                return DRESS_COMMONER_TOPLESS;

            if (!isBottomCovered && !hasArmor)
                return DRESS_COMMONER_BOTTOMLESS;

            if (!isTopCovered && hasArmor)
                return DRESS_ARMORED_TOPLESS;

            if (!isBottomCovered && hasArmor)
                return DRESS_ARMORED_BOTTOMLESS;

            if (hasReligious)
                return DRESS_RELIGIOUS_GARB;

            // Aquí ahora aplicamos BATTLE_READY solo para no nobles
            if (!hasNoble && hasArmor && (chestClothes != null || legClothes != null))
                return DRESS_BATTLE_READY;

            if (!hasNoble && isFullyCovered && !hasArmor)
                return DRESS_COMMONER_FULL;

            if (hasArmor && !hasClothesTop && !hasClothesBottom && !onlyUnderwear)
                return DRESS_ARMORED_ONLY;

            return DRESS_COMMONER_FULL;
        }


        #endregion

        #region Clothing evaluation (Helpers)

        private bool IsSlotEmpty(DaggerfallUnityItem item) => item == null;

        private bool IsFullBodyGarment(DaggerfallUnityItem item)
        {
            if (item == null) return false;

            int cat = GetGarmentCategory(item);
            return cat == GARMENT_TYPE_COMMON_FULL ||
                cat == GARMENT_TYPE_NOBLE_FULL ||
                item.TemplateIndex == (int)WomensClothing.Priestess_robes ||
                item.TemplateIndex == (int)MensClothing.Priest_robes;

        }

        private bool HasClothesOnTop(ItemEquipTable table)
        {
            return table.GetItem(EquipSlots.ChestClothes) != null;
        }

        private bool HasClothesOnBottom(ItemEquipTable table)
        {
            return table.GetItem(EquipSlots.LegsClothes) != null;
        }

        private bool HasOnlyNobleCloak(ItemEquipTable table)
        {
            bool hasNobleCape = false;
            bool hasOtherNoble = false;

            var cloak1 = table.GetItem(EquipSlots.Cloak1);
            var cloak2 = table.GetItem(EquipSlots.Cloak2);

            if (IsNobleCape(cloak1) || IsNobleCape(cloak2))
                hasNobleCape = true;

            foreach (var item in table.EquipTable)
            {
                if (item == null) continue;
                if (item == cloak1 || item == cloak2) continue;

                int cat = GetGarmentCategory(item);
                if (cat == GARMENT_TYPE_NOBLE || cat == GARMENT_TYPE_NOBLE_FULL)
                {
                    hasOtherNoble = true;
                    break;
                }
            }

            return hasNobleCape && !hasOtherNoble;
        }

        private bool IsNobleButNotFull(DaggerfallUnityItem item)
        {
            if (item == null)
                return false;

            int cat = GetGarmentCategory(item);
            return cat == GARMENT_TYPE_NOBLE;
        }

        private bool IsNobleCape(DaggerfallUnityItem item)
        {
            if (item == null) return false;
            if (item.ItemGroup != ItemGroups.MensClothing && item.ItemGroup != ItemGroups.WomensClothing)
                return false;

            int cat = GetGarmentCategory(item);
            return cat == GARMENT_TYPE_NOBLE;
        }


        private bool IsNobleCategory(DaggerfallUnityItem item)
        {
            if (item == null)
                return false;

            int cat = GetGarmentCategory(item);
            return cat == GARMENT_TYPE_NOBLE || cat == GARMENT_TYPE_NOBLE_FULL || cat == GARMENT_TYPE_COMMON_ACCEPTABLE;
        }

        private bool IsWearingOnlyUnderwear(ItemEquipTable table)
        {
            foreach (var item in table.EquipTable)
            {
                if (item == null) continue;

                int cat = GetGarmentCategory(item);

                // Si lleva algo que NO sea ropa interior, no cumple
                if (cat != GARMENT_TYPE_UNDERWEAR)
                    return false;
            }
            return true;
        }


        private bool IsTopCovered(DaggerfallUnityItem chestItem, DaggerfallUnityItem legItem)
        {
            if (IsSlotEmpty(chestItem))
            {
                if (IsFullBodyGarment(legItem))
                    return true;
                return false;
            }
            return true;
        }

        private bool IsBottomCovered(DaggerfallUnityItem legItem, DaggerfallUnityItem chestItem)
        {
            if (IsSlotEmpty(legItem))
            {
                if (IsFullBodyGarment(chestItem))
                    return true;
                return false;
            }
            return true;
        }

        private bool HasNobleOutfit(DaggerfallUnityItem torso, DaggerfallUnityItem legs)
        {
            int top = torso != null ? GetGarmentCategory(torso) : -1;
            int bottom = legs != null ? GetGarmentCategory(legs) : -1;

            bool torsoIsFullBody = torso != null && IsFullBodyGarment(torso);

            if (torsoIsFullBody)
            {
                // If torso is full body noble garment, ignore legs
                return (top == GARMENT_TYPE_NOBLE || top == GARMENT_TYPE_NOBLE_FULL || top == GARMENT_TYPE_COMMON_ACCEPTABLE);
            }
            else
            {
                return (top == GARMENT_TYPE_NOBLE || top == GARMENT_TYPE_NOBLE_FULL || top == GARMENT_TYPE_COMMON_ACCEPTABLE) &&
                       (bottom == GARMENT_TYPE_NOBLE || bottom == GARMENT_TYPE_NOBLE_FULL || bottom == GARMENT_TYPE_COMMON_ACCEPTABLE);
            }
        }


        private bool HasVisibleReligious(ItemEquipTable table)
        {
            foreach (var item in table.EquipTable)
            {
                if (item != null && GetGarmentCategory(item) == GARMENT_TYPE_RELIGIOUS)
                    return true;
            }
            return false;
        }


        #endregion

        #region Clothing clasification

        const int GARMENT_TYPE_COMMON_ACCEPTABLE = 0;
        const int GARMENT_TYPE_COMMON = 1;
        const int GARMENT_TYPE_COMMON_FULL = 2;
        const int GARMENT_TYPE_NOBLE = 3;
        const int GARMENT_TYPE_NOBLE_FULL = 4;
        const int GARMENT_TYPE_ARMOR = 5;
        const int GARMENT_TYPE_RELIGIOUS = 6;
        const int GARMENT_TYPE_UNDERWEAR = 7;

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
                    // Full-body
                    case MensClothing.Khajiit_suit:
                        return GARMENT_TYPE_COMMON_FULL;

                    // Decorative or exotic straps and wraps treated as common acceptable
                    case MensClothing.Straps:
                    case MensClothing.Challenger_Straps:
                    case MensClothing.Champion_straps:
                    case MensClothing.Armbands:
                    case MensClothing.Fancy_Armbands:
                    case MensClothing.Wrap:
                    case MensClothing.Sash:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Underwear
                    case MensClothing.Loincloth:
                        return GARMENT_TYPE_UNDERWEAR;

                    // Religious garments
                    case MensClothing.Priest_robes:
                        return GARMENT_TYPE_RELIGIOUS;

                    // Noble (formal but incomplete)
                    case MensClothing.Formal_cloak:
                    case MensClothing.Formal_tunic:
                    case MensClothing.Dwynnen_surcoat:
                    case MensClothing.Anticlere_Surcoat:
                        return GARMENT_TYPE_NOBLE;

                    // Noble full-body coverage
                    case MensClothing.Toga:
                    case MensClothing.Eodoric:
                        return GARMENT_TYPE_NOBLE_FULL;

                    // Common full-body robes
                    case MensClothing.Plain_robes:
                        return GARMENT_TYPE_COMMON_FULL;

                    // Common garments acceptable in noble outfits
                    case MensClothing.Breeches:
                    case MensClothing.Casual_pants:
                    case MensClothing.Long_Skirt:
                    case MensClothing.Vest:
                    case MensClothing.Kimono:
                    case MensClothing.Reversible_tunic:
                    case MensClothing.Long_shirt_closed_top2:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Basic common garments (not acceptable for noble attire)
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

                    // Footwear acceptable for nobles
                    case MensClothing.Shoes:
                    case MensClothing.Tall_Boots:
                    case MensClothing.Boots:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Basic footwear
                    case MensClothing.Sandals:
                        return GARMENT_TYPE_COMMON;

                    default:
                        // All other or modded garments default to acceptable common
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;
                }
            }

            if (item.ItemGroup == ItemGroups.WomensClothing)
            {
                WomensClothing womens = (WomensClothing)item.TemplateIndex;
                switch (womens)
                {
                    // Reclassified exotic garments as common full coverage
                    case WomensClothing.Khajiit_suit:
                        return GARMENT_TYPE_COMMON_FULL;

                    // Decorative or exotic wraps treated as common acceptable
                    case WomensClothing.Wrap:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Underwear
                    case WomensClothing.Brassier:
                    case WomensClothing.Formal_brassier:
                    case WomensClothing.Loincloth:
                        return GARMENT_TYPE_UNDERWEAR;

                    // Religious garments
                    case WomensClothing.Priestess_robes:
                        return GARMENT_TYPE_RELIGIOUS;

                    // Noble (formal but incomplete)
                    case WomensClothing.Formal_cloak:
                        return GARMENT_TYPE_NOBLE;

                    // Noble full-body garments (dress or formal full outfits)
                    case WomensClothing.Day_gown:
                    case WomensClothing.Strapless_dress:
                    case WomensClothing.Evening_gown:
                    case WomensClothing.Formal_eodoric:
                    case WomensClothing.Eodoric:
                        return GARMENT_TYPE_NOBLE_FULL;

                    // Common full-body robes or dresses
                    case WomensClothing.Plain_robes:
                    case WomensClothing.Casual_dress:
                        return GARMENT_TYPE_COMMON_FULL;

                    // Common garments acceptable in noble outfits
                    case WomensClothing.Casual_pants:
                    case WomensClothing.Long_skirt:
                    case WomensClothing.Tights:
                    case WomensClothing.Vest:
                    case WomensClothing.Open_tunic:
                    case WomensClothing.Long_shirt_belt:
                    case WomensClothing.Short_shirt_belt:
                    case WomensClothing.Short_shirt_closed_belt:
                    case WomensClothing.Peasant_blouse:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Basic common garments (not acceptable in noble outfits)
                    case WomensClothing.Casual_cloak:
                    case WomensClothing.Short_shirt:
                    case WomensClothing.Long_shirt:
                    case WomensClothing.Short_shirt_closed:
                    case WomensClothing.Long_shirt_closed:
                    case WomensClothing.Long_shirt_closed_belt:
                    case WomensClothing.Short_shirt_unchangeable:
                    case WomensClothing.Long_shirt_unchangeable:
                        return GARMENT_TYPE_COMMON;

                    // Footwear acceptable for nobles
                    case WomensClothing.Shoes:
                    case WomensClothing.Tall_boots:
                    case WomensClothing.Boots:
                        return GARMENT_TYPE_COMMON_ACCEPTABLE;

                    // Basic footwear
                    case WomensClothing.Sandals:
                        return GARMENT_TYPE_COMMON;

                    default:
                        // Modded or unknown garments default to common acceptable
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