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

        // Footwear
        const int FOOTWEAR_NONE = 0;
        const int FOOTWEAR_COMMON = 1;
        const int FOOTWEAR_NOBLE = 2;
        const int FOOTWEAR_ARMORED = 3;
        const int FOOTWEAR_WEIRD = 4;

        // Gloves
        const int GLOVES_NONE = 0;
        const int GLOVES_COMMON = 1;
        const int GLOVES_NOBLE = 2;
        const int GLOVES_ARMORED = 3;
        const int GLOVES_WEIRD = 4;

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
            Debug.Log("[Dress Code System] Ready");
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
                    Debug.Log("[DressCodeSystem] Skipped UpdateClothingState due to message box.");
                    return;
                }
                Debug.Log("[DressCodeSystem] Inventory just closed. Updating clothing state.");
                wasInInventory = false;
                //UpdateClothingState();
            }
        }

        private static void StartSaver_OnStartGame(object sender, EventArgs e)
        {
            Debug.Log("[Start Saver] Starting");
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            string saveName = "PPPPPPPP-2";
            GameManager.Instance.SaveLoadManager.Save(playerEntity.Name, saveName);
            Debug.Log("[Start Saver] Game Saved");
            DaggerfallUI.MessageBox("PO");
        }

        // Current clothing state
        int clothingState = -1;

        private void UpdateClothingState()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            var equipment = player.ItemEquipTable;

            // Check torso-related slots
            DaggerfallUnityItem chestArmor = equipment.GetItem(EquipSlots.ChestArmor);
            DaggerfallUnityItem chestClothes = equipment.GetItem(EquipSlots.ChestClothes);
            DaggerfallUnityItem cloak1 = equipment.GetItem(EquipSlots.Cloak1);
            DaggerfallUnityItem cloak2 = equipment.GetItem(EquipSlots.Cloak2);

            // Check legs-related slots
            DaggerfallUnityItem legArmor = equipment.GetItem(EquipSlots.LegsArmor);
            DaggerfallUnityItem legClothes = equipment.GetItem(EquipSlots.LegsClothes);

            bool hasUpper = chestArmor != null || chestClothes != null || cloak1 != null || cloak2 != null;
            bool hasLower = legArmor != null || legClothes != null;

            if (!hasUpper && !hasLower)
                clothingState = DRESS_FULLY_NUDE; 
            else if (!hasUpper || !hasLower)
                clothingState = PARTIALLY_NUDE;
            else
                clothingState = CLOTHED;

            DaggerfallUI.MessageBox(clothingState.ToString());

            Debug.Log($"[DressCode] Clothing state updated: {clothingState}");
        }


    }
}