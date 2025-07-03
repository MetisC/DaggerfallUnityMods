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
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using System.Collections.Generic;
using DaggerfallWorkshop.Utility;

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
        private Coroutine ambientTextDisplay;

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
                yield return new WaitForSeconds((float)clothingUpdateFrequency);
            }
        }

        void OnDestroy()
        {
            if (clothingChecker != null) StopCoroutine(clothingChecker);
        }

        #region Mod Settings ************************************************************************************

        // Updates clothes frequency
        private int clothingUpdateFrequency = 1;

        // Censors the nude states
        private bool nudityEnabled = true;

        // Frequency ambient texts
        private bool enableAmbientContextText = true;
        private int ambientContextTextFrequency = 25;

        // Shows a message with the dress code after closing inventory
        private bool showCurrentAppearanceAfterCloseInventory;

        // Shows the dress code in the status window
        private bool showCurrentAppearanceInCharacterSheet;

        private bool enforceDressCodeInSensitiveAreas;
        private bool enableDressCodeCrime;
        private int crimeGracePeriod;

        private bool enableDressCommentsBeforeTalk;

        void Awake()
        {
            mod.LoadSettingsCallback = (ModSettings settings, ModSettingsChange change) =>
            {
                LoadSettings(settings, change);
            };
            mod.LoadSettings();
            mod.IsReady = true;
            Debug.LogFormat("[Dress Code System] Ready");
            StartGameBehaviour.OnStartGame += StartSaver_OnStartGame;
            DaggerfallUI.UIManager.OnWindowChange += UIManager_OnWindowChange;
            SaveLoadManager.OnLoad += SaveLoadManager_OnLoad;
            PlayerEnterExit.OnTransitionInterior += PlayerEnterExit_OnTransitionInterior;
        }

        private void PlayerEnterExit_OnTransitionInterior(PlayerEnterExit.TransitionEventArgs args)
        {
            warningAlreadyShown = false;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("General", "Nudity"))
                nudityEnabled = settings.GetValue<bool>("General", "Nudity");

            if (change.HasChanged("General", "ShowCurrentAppearanceAfterCloseInventory"))
                showCurrentAppearanceAfterCloseInventory = settings.GetValue<bool>("General", "ShowCurrentAppearanceAfterCloseInventory");

            if (change.HasChanged("General", "ShowCurrentAppearanceInCharacterSheet"))
                showCurrentAppearanceInCharacterSheet = settings.GetValue<bool>("General", "ShowCurrentAppearanceInCharacterSheet");

            if (change.HasChanged("General", "EnableAmbientContextText") || change.HasChanged("General", "AmbientContextTextFrequency"))
            {
                enableAmbientContextText = settings.GetValue<bool>("General", "EnableAmbientContextText");
                ambientContextTextFrequency = settings.GetValue<int>("General", "AmbientContextTextFrequency");
                RestartAmbientRoutine();
            }

            if (change.HasChanged("General", "EnforceDressCodeInSensitiveAreas"))
                enforceDressCodeInSensitiveAreas = settings.GetValue<bool>("General", "EnforceDressCodeInSensitiveAreas");

            if (change.HasChanged("General", "EnableDressCodeCrime") || change.HasChanged("General", "CrimeGracePeriod"))
            {
                enableDressCodeCrime = settings.GetValue<bool>("General", "EnableDressCodeCrime");
                crimeGracePeriod = settings.GetValue<int>("General", "CrimeGracePeriod");
            }

            if (change.HasChanged("General", "EnableDressCommentsBeforeTalk"))
                enableDressCommentsBeforeTalk = settings.GetValue<bool>("General", "EnableDressCommentsBeforeTalk");

        }

        void RestartAmbientRoutine()
        {
            // Stop old coroutine if running
            if (ambientTextDisplay != null)
            {
                StopCoroutine(ambientTextDisplay);
                ambientTextDisplay = null;
            }

            // Don't start new one if disabled
            if (!enableAmbientContextText)
            {
                if (printDebugText)
                    DaggerfallUI.AddHUDText("[AmbientText] Disabled in config - coroutine not started", 2f);
                return;
            }

            // Start coroutine
            ambientTextDisplay = StartCoroutine(ShowAmbientTextDisplay());
        }

        #endregion

        private void SaveLoadManager_OnLoad(SaveData_v1 saveData)
        {
            UpdateClothingState();
            warningAlreadyShown = false;
        }

        private TextLabel dresscodeStatusLabel = null;
        private bool hasBlockedTalkWindow = false;

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

                if (showCurrentAppearanceAfterCloseInventory)
                {
                    // We show the current cloth after closing inventory
                    DaggerfallUI.AddHUDText("Current appearance: " + GetDressCodeStatusText(clothingState), 7f);
                    return;
                }   
            }

            DaggerfallCharacterSheetWindow statusWindow = null;
            if (DaggerfallUI.UIManager.TopWindow is DaggerfallCharacterSheetWindow)
            {
                statusWindow = (DaggerfallCharacterSheetWindow)DaggerfallUI.UIManager.TopWindow;
            }
            
            // Remove if already exists
            if (dresscodeStatusLabel != null && statusWindow != null)
            {
                statusWindow.NativePanel.Components.Remove(dresscodeStatusLabel);
                dresscodeStatusLabel = null;
            }

            if (statusWindow != null && showCurrentAppearanceInCharacterSheet)
            {
                // Update dresscode state
                UpdateClothingState();

                // Create new label at temporary position
                dresscodeStatusLabel = DaggerfallUI.AddTextLabel(
                    DaggerfallUI.DefaultFont,
                    Vector2.zero,
                    GetDressCodeStatusText(clothingState),
                    statusWindow.NativePanel);

                // Visual setup
                dresscodeStatusLabel.TextColor = DaggerfallUI.DaggerfallDefaultTextColor;
                dresscodeStatusLabel.ShadowPosition = Vector2.zero;
                dresscodeStatusLabel.BackgroundColor = Color.black;

                // Centered position
                float textWidth = dresscodeStatusLabel.TextWidth;
                float centeredX = (statusWindow.NativePanel.InteriorWidth - textWidth) * 0.5f + 94;
                dresscodeStatusLabel.Position = new Vector2(centeredX, 193);

            }

            // ⛔ Block conversation if Dress Code is violated when talk window opens
            if (enableDressCommentsBeforeTalk && currentWindow is DaggerfallTalkWindow)
            {
                PrintDebug("[TalkBlock] DaggerfallTalkWindow detected.");

                UpdateClothingState();
                PrintDebug($"[TalkBlock] Updated clothing state: {clothingState}");

                string context = GetCurrentContextGroup();
                PrintDebug($"[TalkBlock] Current context: {context}");

                if (!string.IsNullOrEmpty(context))
                {
                    bool allowed = IsAccessAllowed(context, clothingState);
                    PrintDebug($"[TalkBlock] IsAccessAllowed result: {allowed}");

                    if (!allowed)
                    {
                        PrintDebug("[TalkBlock] Outfit not acceptable. Blocking conversation.");
                        DaggerfallUI.UIManager.PopWindow();

                        int randomIndex = UnityEngine.Random.Range(0, 20); // total de frases
                        string messageTalkBlock = mod.Localize("talkblock_" + randomIndex);
                        if (string.IsNullOrEmpty(messageTalkBlock) || messageTalkBlock.StartsWith("No translation"))
                            messageTalkBlock = "‘I don't talk to walking wardrobe malfunctions.’";

                        DaggerfallUI.MessageBox(messageTalkBlock);
                        PrintDebug($"[TalkBlock] Block message shown: {messageTalkBlock}");
                        return;
                    }

                    // ✅ Outfit allowed – check for verbal reaction
                    int[] group_REVEALING = new int[]
                    {
                        DRESS_FULLY_NUDE,
                        DRESS_COMMONER_TOPLESS,
                        DRESS_COMMONER_BOTTOMLESS,
                        DRESS_ARMORED_TOPLESS,
                        DRESS_ARMORED_BOTTOMLESS,
                    };

                    string npcType = DetectSimpleNPCTypeGrouped(); // commoner, noble, guard
                    int index = UnityEngine.Random.Range(0, 5);
                    string group = "";
                    string key = "";

                    if (Array.Exists(group_REVEALING, s => s == clothingState))
                    {
                        group = "REVEALING";
                        key = $"talkreact_{context}_{group}_{npcType}_{index}";
                    }
                    else if (clothingState == DRESS_NOBLE_INDECENT)
                    {
                        group = "NOBLE_INDECENT";
                        key = $"talkreact_{context}_{group}_{npcType}_{index}";
                    }

                    if (hasBlockedTalkWindow)
                    {
                        hasBlockedTalkWindow = false;
                        return;
                    }

                    string message = mod.Localize(key);
                    if (!string.IsNullOrEmpty(message) && !message.StartsWith("No translation"))
                    {
                        hasBlockedTalkWindow = true;
                        DaggerfallUI.MessageBox(message);
                        PrintDebug($"[TalkReact] Message shown: {message}");
                    }
                    else
                    {
                        PrintDebug($"[TalkReact] No message for key: {key}");
                    }

                }
            }

        }

        private string DetectSimpleNPCTypeGrouped()
        {
            if (!TalkManager.HasInstance)
                return "commoner"; // fallback

            if (TalkManager.Instance.CurrentNPCType == TalkManager.NPCType.Static && TalkManager.Instance.StaticNPC != null)
            {
                var npc = TalkManager.Instance.StaticNPC;
                string name = npc.DisplayName?.ToLowerInvariant();

                if (name.Contains("guard") || name.Contains("watch"))
                    return "guard";
                if (name.Contains("lord") || name.Contains("noble"))
                    return "noble";

                return "commoner";
            }
            else if (TalkManager.Instance.CurrentNPCType == TalkManager.NPCType.Mobile && TalkManager.Instance.MobileNPC != null)
            {
                var npc = TalkManager.Instance.MobileNPC;

                if (npc.IsGuard)
                    return "guard";

                string npcName = npc.name?.ToLowerInvariant();
                if (npcName?.Contains("knight") == true || npcName?.Contains("noble") == true)
                    return "noble";

                return "commoner";
            }

            return "commoner";
        }


        private void StartSaver_OnStartGame(object sender, EventArgs e)
        {
            UpdateClothingState();
        }

        // Clothing State
        int clothingState = -1;

        bool footwear = false;

        // ***************************************************************************************************************************
        // ***************************************************************************************************************************
        // ***************************************************************************************************************************

        // Track if the warning has already been shown (to avoid repetition)
        private bool warningAlreadyShown = false;

        private void UpdateClothingState()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            var equipment = player.ItemEquipTable;

            // Evaluate dress code using the updated routine
            clothingState = EvaluateDressCode();

            if (graceCountdownCoroutine != null)
            {
                string context = GetCurrentContextGroup();
                if (!string.IsNullOrEmpty(context) && IsAccessAllowed(context, clothingState))
                {
                    StopCoroutine(graceCountdownCoroutine);
                    graceCountdownCoroutine = null;
                    warningAlreadyShown = false; // Reset warning flag
                    PrintDebug("[DressCode] Player dressed properly during grace - guard spawn cancelled");
                    DaggerfallUI.MessageBox(mod.Localize("dresscode_calm_message"));
                }
            }

            // Detect if player became indecent inside a restricted area
            if (enforceDressCodeInSensitiveAreas && GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding && !IsForcedEntry() && graceCountdownCoroutine == null)
            {
                string context = GetCurrentContextGroup();

                if (!string.IsNullOrEmpty(context) && !IsAccessAllowed(context, clothingState) && !warningAlreadyShown)
                {
                    PrintDebug("[DressCode] Player changed clothes and became indecent inside a restricted area");

                    string key = $"dresswarning_{context}";
                    string warningMessage = mod.Localize(key);
                    PrintDebug("[DressCode] Localized key: " + key);

                    if (string.IsNullOrEmpty(warningMessage) || warningMessage.StartsWith("No translation"))
                    {
                        PrintDebug("[DressCode] No message found or translation missing");
                        return;
                    }

                    PrintDebug("[DressCode] Message: " + warningMessage);

                    List<string> lines = new List<string>();
                    lines.Add(warningMessage);

                    if (enableDressCodeCrime)
                    {
                        string graceMessage = mod.Localize("dresscrime_grace");

                        if (!string.IsNullOrEmpty(graceMessage) && !graceMessage.StartsWith("No translation"))
                        {
                            graceMessage = graceMessage.Replace("%d", crimeGracePeriod.ToString());
                            lines.Add(graceMessage);
                            graceCountdownCoroutine = StartCoroutine(DelayedGuardSpawn((float)crimeGracePeriod));
                            PrintDebug("[DressCode] Grace period started (clothing change)");
                        }
                        else
                        {
                            PrintDebug("[DressCode] No grace message found or translation missing");
                        }
                    }

                    // Show warning popup just like on interior transition
                    DaggerfallUI.MessageBox(lines.ToArray());
                    warningAlreadyShown = true;
                }
            }

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
        }

        void PrintDebug(string message)
        {
#if UNITY_EDITOR
            if (printDebugText) DaggerfallUI.AddHUDText("[DressCodeSystem]" + message, 7f);
#endif
        }

        // ---------------------------------------------------------------
        #region Clothing evaluation

        private int EvaluateDressCode()
        {
            var player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return Censorship(DRESS_FULLY_NUDE);

            var table = player.ItemEquipTable;

            footwear = table.GetItem(EquipSlots.Feet) != null;

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
                return Censorship(DRESS_NOBLE_INDECENT);

            bool topIsNobleOnly = IsNobleButNotFull(chestClothes ?? chestArmor);
            bool bottomIsNobleOnly = IsNobleButNotFull(legClothes ?? legArmor);
            bool hasSoloNoble = (topIsNobleOnly && !HasClothesOnBottom(table) && legArmor == null)
                             || (bottomIsNobleOnly && !HasClothesOnTop(table) && chestArmor == null);

            if (hasSoloNoble)
                return Censorship(DRESS_NOBLE_INDECENT);

            if (!isTopCovered && !isBottomCovered)
                return Censorship(DRESS_FULLY_NUDE);

            if (onlyUnderwear)
                return Censorship(DRESS_UNDERWEAR_ONLY);

            if (hasNoble)
            {
                if (isFullyCovered)
                {
                    if (hasArmor)
                    {
                        if (nobleJewels >= 1)
                            return Censorship(DRESS_NOBLE_BATTLE_READY);
                        else
                            return Censorship(DRESS_NOBLE_NO_JEWELS);
                    }
                    else
                    {
                        if (nobleJewels >= 1)
                            return Censorship(DRESS_NOBLE_FULL);
                        else
                            return Censorship(DRESS_NOBLE_NO_JEWELS);
                    }
                }
                else
                {
                    bool topIsNoble = IsNobleCategory(chestClothes ?? chestArmor);
                    bool bottomIsNoble = IsNobleCategory(legClothes ?? legArmor);

                    if (topIsNoble || bottomIsNoble)
                        return Censorship(DRESS_NOBLE_INDECENT);

                    if (topIsNoble || bottomIsNoble || HasOnlyNobleCloak(table))
                        return Censorship(DRESS_NOBLE_INDECENT);
                }
            }

            if (!isTopCovered && !hasArmor)
                return Censorship(DRESS_COMMONER_TOPLESS);

            if (!isBottomCovered && !hasArmor)
                return Censorship(DRESS_COMMONER_BOTTOMLESS);

            if (!isTopCovered && hasArmor)
                return Censorship(DRESS_ARMORED_TOPLESS);

            if (!isBottomCovered && hasArmor)
                return Censorship(DRESS_ARMORED_BOTTOMLESS);

            if (hasReligious)
                return Censorship(DRESS_RELIGIOUS_GARB);

            if (!hasNoble && hasArmor && (chestClothes != null || legClothes != null))
                return Censorship(DRESS_BATTLE_READY);

            if (!hasNoble && isFullyCovered && !hasArmor)
                return Censorship(DRESS_COMMONER_FULL);

            if (hasArmor && !hasClothesTop && !hasClothesBottom && !onlyUnderwear)
                return Censorship(DRESS_ARMORED_ONLY);

            return Censorship(DRESS_COMMONER_FULL);
        }

        private int Censorship(int state)
        {
            if (!nudityEnabled)
            {
                switch (state)
                {
                    case DRESS_FULLY_NUDE:
                    case DRESS_COMMONER_TOPLESS:
                    case DRESS_COMMONER_BOTTOMLESS:
                    case DRESS_ARMORED_TOPLESS:
                    case DRESS_ARMORED_BOTTOMLESS:
                    case DRESS_NOBLE_INDECENT:
                        return DRESS_UNDERWEAR_ONLY;
                }
            }
            return state;
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

        #region Update

        private float movementCheckTimer = 0f;

        void Update()
        {
            if (graceCountdownCoroutine != null)
            {
                movementCheckTimer += Time.deltaTime;

                if (movementCheckTimer >= 0.5f)
                {
                    movementCheckTimer = 0f;

                    if (!GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding)
                    {
                        StopCoroutine(graceCountdownCoroutine);
                        graceCountdownCoroutine = null;
                        PrintDebug("[DressCode] Player left the area - guard spawn cancelled");
                    }
                }
            }

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                GiveAllClothingAndJewels();
            }
            if (Input.GetKeyDown(KeyCode.RightArrow))
            {
                TestAllDressStatus();
            }
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                DebugPrintAmbientContextInfo();
            }
            if (Input.GetKeyDown(KeyCode.KeypadMinus))
            {
                if (!printDebugText)
                {
                    printDebugText = true;
                    DaggerfallUI.AddHUDText("[DEBUG MODE ACTIVATED]", 3f);
                }
                else
                {
                    printDebugText = false;
                    DaggerfallUI.AddHUDText("[DEBUG MODE DISABLED]", 3f);
                }
            }
#endif
        }

        void GiveAllClothingAndJewels()
        {
            PlayerEntity player = GameManager.Instance?.PlayerEntity;
            if (player == null)
                return;

            bool isPlayerFemale = player.Gender == Genders.Female;

            PrintDebug("[DressCode - DEBUG] Adding all equipable items...");

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

            PrintDebug("[DressCode - DEBUG] Added!");
        }

        #endregion

        // /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        #region Show Ambient Text Display (INCOMPLETO)

        private bool printDebugText = false;

        IEnumerator ShowAmbientTextDisplay()
        {

            if (!enableAmbientContextText)
                yield break;

            string lastKeyUsed = null;

            while (true)
            {
                if (GameManager.Instance.IsPlayingGame() &&
                    DaggerfallUI.UIManager.WindowCount == 0)
                {

                    PrintDebug("[Ambient] Game running and UI clear");

                    UpdateClothingState();
                    PrintDebug("[Ambient] Clothing state updated");

                    string context = GetCurrentContextGroup();
                    PrintDebug("[Ambient] Context = " + context);

                    if (string.IsNullOrEmpty(context))
                    {

                        PrintDebug("[Ambient] Context is null - skipping");

                        yield return WaitWithVariation();
                        continue;
                    }

                    bool isDark = GameManager.Instance.PlayerEnterExit.IsPlayerInDarkness;
                    bool isStreet = context == "STREET";
                    bool isStreetAndDay = isStreet && !isDark;

                    bool isInside = GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding;
                    bool isValidInterior = isInside && !IsForcedEntry();

                    PrintDebug("[Ambient] Hour: " + DaggerfallUnity.Instance.WorldTime.Now.Hour.ToString("F1"));
                    PrintDebug("[Ambient] IsDark: " + isDark + ", IsStreet: " + isStreet + ", IsStreetAndDay: " + isStreetAndDay);
                    PrintDebug("[Ambient] IsInside: " + isInside + ", ForcedEntry: " + IsForcedEntry() + ", IsValidInterior: " + isValidInterior);

                    if (!isStreetAndDay && !isValidInterior)
                    {

                        PrintDebug("[Ambient] Not a valid moment to show ambient - skipping");

                        yield return WaitWithVariation();
                        continue;
                    }

                    string dressKey = GetDressCodeStateKey(clothingState);
                    string gender = GameManager.Instance.PlayerEntity.Gender == Genders.Male ? "M" : "F";

                    string key;
                    int attempts = 0;
                    do
                    {
                        int index = UnityEngine.Random.Range(0, 10);
                        key = $"ambient_{context}_{dressKey}_{gender}_{index}";
                        attempts++;
                    }
                    while (key == lastKeyUsed && attempts < 10);

                    lastKeyUsed = key;

                    string message = mod.Localize(key);
                    PrintDebug("[Ambient] Generated key: " + key);

                    if (!string.IsNullOrEmpty(message) && !message.StartsWith("No translation"))
                    {
                        if (ShouldDisplayAmbient(context, clothingState))
                            DaggerfallUI.AddHUDText(message, 6f);
                        else
                            PrintDebug("[Ambient] Message valid but suppressed by filter");
                    }
                    else
                    {
                        PrintDebug("[Ambient] No message found or translation missing");
                    }
                }
                else
                {
                    PrintDebug("[Ambient] IsPlayingGame=" + GameManager.Instance.IsPlayingGame() + ", WindowCount=" + DaggerfallUI.UIManager.WindowCount + " - skipping");
                }

                yield return WaitWithVariation();
            }
        }

        WaitForSeconds WaitWithVariation()
        {
            float variation = UnityEngine.Random.Range(-2f, 2f);
            float waitTime = Mathf.Max(10f, (float)ambientContextTextFrequency + variation);
            return new WaitForSeconds(waitTime);
        }

        private string GetCurrentContextGroup()
        {
            var enterExit = GameManager.Instance.PlayerEnterExit;
            var playerGPS = GameManager.Instance.PlayerGPS;

            // Street
            if (!enterExit.IsPlayerInside)
            {
                if (playerGPS.IsPlayerInTown())
                    return "STREET";
                else
                    return null;
            }

            // Dungeon
            if (enterExit.IsPlayerInsideDungeon)
                return null;

            // Interior
            if (enterExit.IsPlayerInsideBuilding)
            {
                switch (enterExit.BuildingType)
                {
                    // Shop
                    case DFLocation.BuildingTypes.Alchemist:
                    case DFLocation.BuildingTypes.Armorer:
                    case DFLocation.BuildingTypes.Bookseller:
                    case DFLocation.BuildingTypes.ClothingStore:
                    case DFLocation.BuildingTypes.FurnitureStore:
                    case DFLocation.BuildingTypes.GemStore:
                    case DFLocation.BuildingTypes.GeneralStore:
                    case DFLocation.BuildingTypes.PawnShop:
                    case DFLocation.BuildingTypes.WeaponSmith:
                        return "SHOP";

                    // House
                    case DFLocation.BuildingTypes.House1:
                    case DFLocation.BuildingTypes.House2:
                    case DFLocation.BuildingTypes.House3:
                    case DFLocation.BuildingTypes.House4:
                    case DFLocation.BuildingTypes.House5:
                    case DFLocation.BuildingTypes.House6:
                        return "HOUSE";

                    // Tavern
                    case DFLocation.BuildingTypes.Tavern:
                        return "TAVERN";

                    // Temple
                    case DFLocation.BuildingTypes.Temple:
                        return DetectTempleStrictness((int)enterExit.FactionID);

                    // Palace
                    case DFLocation.BuildingTypes.Palace:
                        return "CASTLE";

                    // Strict Shops
                    case DFLocation.BuildingTypes.Bank:
                    case DFLocation.BuildingTypes.Library:
                        return "GROUPINTERIOR_STRICT";

                    // Town
                    case DFLocation.BuildingTypes.Town4:
                    case DFLocation.BuildingTypes.Town23:
                        return "STREET";

                    // Other
                    case DFLocation.BuildingTypes.Ship:
                    case DFLocation.BuildingTypes.Special1:
                    case DFLocation.BuildingTypes.Special2:
                    case DFLocation.BuildingTypes.Special3:
                    case DFLocation.BuildingTypes.Special4:
                    case DFLocation.BuildingTypes.HouseForSale:
                        return null;

                    // Guild
                    case DFLocation.BuildingTypes.GuildHall:
                    default:
                        return DetectFactionInteriorStrictness((int)enterExit.FactionID);
                }
            }

            return null;
        }

        private string DetectTempleStrictness(int factionId)
        {
            var player = GameManager.Instance.PlayerEntity;
            if (player == null) return "GROUPINTERIOR_STRICT";

            FactionFile.FactionData factionData;
            bool ok = player.FactionData.GetFactionData(factionId, out factionData);
            if (!ok)
                return "GROUPINTERIOR_STRICT";

            string facName = factionData.name;

            if (facName.Contains("Dibella") || facName.Contains("Kynareth") || facName.Contains("Zenithar"))
                return "GROUPINTERIOR_LIBERAL";

            if (facName.Contains("Arkay") || facName.Contains("Mara") || facName.Contains("Akatosh") || facName.Contains("Julianos") || facName.Contains("Stendarr"))
                return "GROUPINTERIOR_STRICT";

            return "GROUPINTERIOR_STRICT";
        }

        private string DetectFactionInteriorStrictness(int factionId)
        {
            var player = GameManager.Instance.PlayerEntity;
            if (player == null) return "GROUPINTERIOR_STRICT";

            FactionFile.FactionData factionData;
            bool ok = player.FactionData.GetFactionData(factionId, out factionData);
            if (!ok) return "GROUPINTERIOR_STRICT";

            string name = factionData.name;

            if (name.Contains("Noble") || name.Contains("Royalty"))
                return "CASTLE";

            if (name.Contains("Thieves") ||
                name.Contains("Brotherhood") ||
                name.Contains("Witches") ||
                name.Contains("Bards") ||
                name.Contains("Commoners") ||
                name.Contains("Vampire"))
                return "GROUPINTERIOR_LIBERAL";

            if (name.Contains("Knight") ||
                name.Contains("Guard") ||
                name.Contains("Bank") ||
                name.Contains("Scholar") ||
                name.Contains("Merchant"))
                return "GROUPINTERIOR_STRICT";

            return "GROUPINTERIOR_STRICT";
        }

        /// <summary>
        /// Determines whether an ambient message should be shown,
        /// based on current clothing state and contextual location.
        /// </summary>
        private bool ShouldDisplayAmbient(string context, int dressCode)
        {
            switch (dressCode)
            {
                case DRESS_COMMONER_FULL:
                    // Don't show messages when dressed as a commoner in regular places
                    // Only show if in noble/strict environments
                    return context == "CASTLE" || context == "GROUPINTERIOR_STRICT";

                case DRESS_NOBLE_FULL:
                    // Don't show reactions in noble/strict places (expected attire)
                    return context != "CASTLE" && context != "GROUPINTERIOR_STRICT";

                case DRESS_RELIGIOUS_GARB:
                    // Don't comment on religious garb in strict (temple-like) areas
                    return context != "GROUPINTERIOR_STRICT";

                case DRESS_NOBLE_NO_JEWELS:
                    // Always react to lack of noble jewelry
                    return true;

                default:
                    // All other dress codes (nudity, partial armor, etc.) are always noticeable
                    return true;
            }
        }


        #endregion

        #region Show Status

        string GetDressCodeStatusText(int clothingState)
        {
            switch (clothingState)
            {
                case DRESS_FULLY_NUDE:
                    return mod.Localize("dresscode_fully_nude");
                case DRESS_UNDERWEAR_ONLY:
                    return mod.Localize("dresscode_underwear_only");
                case DRESS_COMMONER_TOPLESS:
                    return mod.Localize("dresscode_commoner_topless");
                case DRESS_COMMONER_BOTTOMLESS:
                    return mod.Localize("dresscode_commoner_bottomless");
                case DRESS_ARMORED_TOPLESS:
                    return mod.Localize("dresscode_armored_topless");
                case DRESS_ARMORED_BOTTOMLESS:
                    return mod.Localize("dresscode_armored_bottomless");
                case DRESS_COMMONER_FULL:
                    return mod.Localize("dresscode_commoner_full");
                case DRESS_NOBLE_FULL:
                    return mod.Localize("dresscode_noble_full");
                case DRESS_ARMORED_ONLY:
                    return mod.Localize("dresscode_armored_only");
                case DRESS_BATTLE_READY:
                    return mod.Localize("dresscode_battle_ready");
                case DRESS_RELIGIOUS_GARB:
                    return mod.Localize("dresscode_religious_garb");
                case DRESS_NOBLE_INDECENT:
                    return mod.Localize("dresscode_noble_indecent");
                case DRESS_NOBLE_NO_JEWELS:
                    return mod.Localize("dresscode_noble_no_jewels");
                case DRESS_NOBLE_BATTLE_READY:
                    return mod.Localize("dresscode_noble_battle_ready");
                default:
                    return string.Format(mod.Localize("dresscode_unknown"), clothingState);
            }
        }

        string GetDressCodeStateKey(int clothingState)
        {
            switch (clothingState)
            {
                case DRESS_FULLY_NUDE: return "FULLY_NUDE";
                case DRESS_UNDERWEAR_ONLY: return "UNDERWEAR_ONLY";
                case DRESS_COMMONER_TOPLESS: return "COMMONER_TOPLESS";
                case DRESS_COMMONER_BOTTOMLESS: return "COMMONER_BOTTOMLESS";
                case DRESS_ARMORED_TOPLESS: return "ARMORED_TOPLESS";
                case DRESS_ARMORED_BOTTOMLESS: return "ARMORED_BOTTOMLESS";
                case DRESS_COMMONER_FULL: return "COMMONER_FULL";
                case DRESS_NOBLE_FULL: return "NOBLE_FULL";
                case DRESS_ARMORED_ONLY: return "ARMORED_ONLY";
                case DRESS_BATTLE_READY: return "BATTLE_READY";
                case DRESS_RELIGIOUS_GARB: return "RELIGIOUS_GARB";
                case DRESS_NOBLE_INDECENT: return "NOBLE_INDECENT";
                case DRESS_NOBLE_NO_JEWELS: return "NOBLE_NO_JEWELS";
                case DRESS_NOBLE_BATTLE_READY: return "NOBLE_BATTLE_READY";
                default: return "UNKNOWN";
            }
        }

        void TestAllDressStatus()
        {
            int[] allStates = new int[]
            {
                DRESS_FULLY_NUDE,
                DRESS_UNDERWEAR_ONLY,
                DRESS_COMMONER_TOPLESS,
                DRESS_COMMONER_BOTTOMLESS,
                DRESS_ARMORED_TOPLESS,
                DRESS_ARMORED_BOTTOMLESS,
                DRESS_COMMONER_FULL,
                DRESS_NOBLE_FULL,
                DRESS_ARMORED_ONLY,
                DRESS_BATTLE_READY,
                DRESS_RELIGIOUS_GARB,
                DRESS_NOBLE_INDECENT,
                DRESS_NOBLE_NO_JEWELS,
                DRESS_NOBLE_BATTLE_READY,
                999  // default
            };

            foreach (int state in allStates)
            {
                string msg = GetDressCodeStatusText(state);
                DaggerfallUI.MessageBox(msg);
            }
        }

        void DebugPrintAmbientContextInfo()
        {
            List<string> lines = new List<string>();

            var enterExit = GameManager.Instance.PlayerEnterExit;
            var playerGPS = GameManager.Instance.PlayerGPS;
            var playerEntity = GameManager.Instance.PlayerEntity;

            lines.Add("[DressCode Debug]");
            lines.Add($"IsPlayingGame: {GameManager.Instance.IsPlayingGame()}");
            lines.Add($"InGameHour: {DaggerfallUnity.Instance.WorldTime.Now.Hour:F1}");
            lines.Add($"IsNight (IsPlayerInDarkness): {enterExit.IsPlayerInDarkness}");
            lines.Add($"IsPlayerInside: {enterExit.IsPlayerInside}");
            lines.Add($"IsPlayerInsideBuilding: {enterExit.IsPlayerInsideBuilding}");
            lines.Add($"IsPlayerInsideDungeon: {enterExit.IsPlayerInsideDungeon}");
            lines.Add($"IsPlayerInTown (exterior): {playerGPS.IsPlayerInTown()}");

            string context = GetCurrentContextGroup();
            lines.Add($"Calculated Context Group: {context}");

            if (enterExit.IsPlayerInsideBuilding)
            {
                lines.Add($"BuildingType: {enterExit.BuildingType}");
                var bld = enterExit.BuildingDiscoveryData;
                lines.Add($"BuildingKey: {bld.buildingKey} (0 = forced entry)");
                lines.Add($"Building Name: {bld.displayName}");
                lines.Add($"Location Name: {GameManager.Instance.PlayerGPS.CurrentLocation.Name}");
            }

            uint facId = enterExit.FactionID;
            lines.Add($"Faction ID: {facId}");

            if (playerEntity != null)
            {
                FactionFile.FactionData factionData;
                bool ok = playerEntity.FactionData.GetFactionData((int)facId, out factionData);
                if (ok)
                {
                    lines.Add($"Faction Name: {factionData.name}");
                    lines.Add($"Faction Type: {factionData.type}");
                    lines.Add($"Reputation: {playerEntity.FactionData.GetReputation((int)facId)}");
                }
                else
                {
                    lines.Add("No faction data found for this ID.");
                }
            }

            if (!string.IsNullOrEmpty(context))
            {
                string dressKey = GetDressCodeStateKey(clothingState);
                string gender = playerEntity.Gender == Genders.Male ? "M" : "F";
                string exampleKey = $"ambient_{context}_{dressKey}_{gender}_0";
                lines.Add($"Sample Ambient Key: {exampleKey}");
            }

            var messageBox = new DaggerfallMessageBox(DaggerfallUI.UIManager);
            messageBox.SetText(lines.ToArray());
            messageBox.ClickAnywhereToClose = true;
            messageBox.Show();
        }

        #endregion

        #region Restricted Clothes Zones

        private Coroutine graceCountdownCoroutine;

        IEnumerator DelayedGuardSpawn(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);

            EnforceDressCodeCrime();

            PrintDebug("[TransitionMessage] Delayed guard spawn triggered");
        }
        
        /// <summary>
        /// Determines if the given dress code is appropriate for the specified context.
        /// Returns true if access should be allowed, false if a warning should be triggered.
        /// </summary>
        private bool IsAccessAllowed(string context, int dressCode)
        {
            // Allow high reputation players to bypass dress code restrictions
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideBuilding)
            {
                var player = GameManager.Instance.PlayerEntity;
                int factionId = (int)GameManager.Instance.PlayerEnterExit.FactionID;

                // 80+ means you're essentially a top-rank member like Archmage or Grandmaster
                if (player.FactionData.GetReputation((int)factionId) >= 80)
                {
                    PrintDebug($"[DressCode] High reputation ({player.FactionData.GetReputation((int)factionId)}) with faction {factionId}. Dress code ignored.");
                    return true;
                }
            }
            switch (context)
            {
                case "CASTLE":
                case "GROUPINTERIOR_STRICT":
                    // Require full or noble/religious attire.
                    return dressCode == DRESS_COMMONER_FULL ||
                           dressCode == DRESS_NOBLE_FULL ||
                           dressCode == DRESS_NOBLE_NO_JEWELS ||
                           dressCode == DRESS_NOBLE_BATTLE_READY ||
                           dressCode == DRESS_RELIGIOUS_GARB;

                case "GROUPINTERIOR_LIBERAL":
                case "TAVERN":
                    // Fully permissive – even nudity is accepted.
                    return true;

                case "HOUSE":
                case "SHOP":
                    // Deny if fully nude or exposing top/bottom regardless of armor.
                    switch (dressCode)
                    {
                        case DRESS_FULLY_NUDE:
                        case DRESS_COMMONER_TOPLESS:
                        case DRESS_COMMONER_BOTTOMLESS:
                        case DRESS_ARMORED_TOPLESS:
                        case DRESS_ARMORED_BOTTOMLESS:
                            return false;
                        default:
                            return true;
                    }

                case "STREET":
                default:
                    return true;
            }
        }

        /// <summary>
        /// Determines if player has entered a building by force (e.g., at night when it's supposed to be closed).
        /// Only applies to specific building types typically closed at night.
        /// </summary>
        private bool IsForcedEntry()
        {
            var enterExit = GameManager.Instance.PlayerEnterExit;

            if (!enterExit.IsPlayerInsideBuilding)
            {
                PrintDebug("[IsForcedEntry] Not inside a building.");
                return false;
            }

            DFLocation.BuildingTypes type = enterExit.BuildingType;
            string context = GetCurrentContextGroup();

            PrintDebug($"[IsForcedEntry] BuildingType = {type}");
            PrintDebug($"[IsForcedEntry] Context = {context}");

            // --- SHOPS ---
            if (context == "SHOP")
            {
                bool isOpen = enterExit.IsPlayerInsideOpenShop;
                PrintDebug($"[IsForcedEntry] Shop detected. Is open? {isOpen}");

                if (!isOpen)
                {
                    PrintDebug("[IsForcedEntry] Shop is closed. This is a forced entry.");
                    return true;
                }

                PrintDebug("[IsForcedEntry] Shop is open. Not a forced entry.");
                return false;
            }

            // --- OTHER ---
            bool isNight = DaggerfallUnity.Instance.WorldTime.Now.IsNight;
            PrintDebug($"[IsForcedEntry] Is night? {isNight}");

            if (!isNight)
            {
                PrintDebug("[IsForcedEntry] Daytime. Not a forced entry.");
                return false;
            }

            switch (type)
            {
                case DFLocation.BuildingTypes.Alchemist:
                case DFLocation.BuildingTypes.Armorer:
                case DFLocation.BuildingTypes.Bookseller:
                case DFLocation.BuildingTypes.ClothingStore:
                case DFLocation.BuildingTypes.FurnitureStore:
                case DFLocation.BuildingTypes.GemStore:
                case DFLocation.BuildingTypes.GeneralStore:
                case DFLocation.BuildingTypes.PawnShop:
                case DFLocation.BuildingTypes.WeaponSmith:
                case DFLocation.BuildingTypes.Bank:
                case DFLocation.BuildingTypes.Library:
                    PrintDebug("[IsForcedEntry] Closed-at-night building entered at night. Forced entry.");
                    return true;
            }

            PrintDebug("[IsForcedEntry] No forced entry detected.");
            return false;
        }




        /// <summary>
        /// Triggers a crime and appropriate punishment for dress code violation,
        /// adapting response based on building type and faction.
        /// </summary>
        private void EnforceDressCodeCrime()
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            if (playerEntity == null)
                return;

            var enterExit = GameManager.Instance.PlayerEnterExit;
            DFLocation.BuildingTypes buildingType = enterExit.BuildingType;
            uint factionID = enterExit.FactionID;

            // Assign the most suitable crime type for indecent exposure
            playerEntity.CrimeCommitted = PlayerEntity.Crimes.Vagrancy;

            DaggerfallUI.AddHUDText("You refused to dress properly. The city guard has been alerted.", 3f);

            switch (buildingType)
            {
                // Normal interiors where guard spawning is expected
                case DFLocation.BuildingTypes.Alchemist:
                case DFLocation.BuildingTypes.GeneralStore:
                case DFLocation.BuildingTypes.WeaponSmith:
                case DFLocation.BuildingTypes.PawnShop:
                case DFLocation.BuildingTypes.Tavern:
                case DFLocation.BuildingTypes.Palace:
                    playerEntity.SpawnCityGuards(true);
                    PrintDebug("[DressCodeCrime] Spawned guards via standard system.");
                    break;

                // Temples or guilds - don't spawn guards normally but react with reputation and enemies
                case DFLocation.BuildingTypes.Temple:
                case DFLocation.BuildingTypes.GuildHall:
                    if (playerEntity.FactionData.GetFactionData((int)factionID, out var factionData))
                    {
                        playerEntity.FactionData.ChangeReputation((int)factionID, -2, true);

                        // Choose a punishment enemy type based on the faction
                        MobileTypes punishmentType = MobileTypes.Knight_CityWatch;
                        if (factionData.name.Contains("Brotherhood"))
                            punishmentType = MobileTypes.Assassin;
                        else if (factionData.name.Contains("Knight"))
                            punishmentType = MobileTypes.Knight;

                        GameObjectHelper.CreateFoeSpawner(false, punishmentType, 2, 3, 8);
                        PrintDebug($"[DressCodeCrime] Forced spawn: {punishmentType} from faction {factionData.name}");
                    }
                    break;

                // Catch-all fallback for unknown interiors
                default:
                    GameObjectHelper.CreateFoeSpawner(false, MobileTypes.Knight_CityWatch, 2, 3, 8);
                    PrintDebug("[DressCodeCrime] Fallback punishment triggered.");
                    break;
            }
        }

        #endregion

    }
}