using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Questing;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DaggerfallWorkshop;

namespace TravelMapEnhancements
{
    public class TravelMapEnhancements : MonoBehaviour
    {
        static Mod mod;
        static KeyCode toggleKey = KeyCode.P;

        public static bool showFavorites = true;

        static TravelDashboardWindow dashboardInstance;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<TravelMapEnhancements>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            mod.IsReady = true;
        }

        void Update()
        {
            if (Input.GetKeyUp(toggleKey))
            {
                if (dashboardInstance != null && DaggerfallUI.UIManager.TopWindow == dashboardInstance)
                {
                    dashboardInstance.CloseWindow();
                }
                else if (GameManager.Instance.IsPlayingGame() && DaggerfallUI.UIManager.WindowCount <= 1)
                {
                    if (dashboardInstance == null)
                        dashboardInstance = new TravelDashboardWindow(DaggerfallUI.UIManager, mod);

                    DaggerfallUI.UIManager.PushWindow(dashboardInstance);
                }
            }
        }

        static void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            string keyString = settings.GetValue<string>("General", "ToggleKey");

            if (!string.IsNullOrEmpty(keyString))
            {
                keyString = keyString.Trim().ToLower();

                switch (keyString)
                {
                    case "0": keyString = "Alpha0"; break;
                    case "1": keyString = "Alpha1"; break;
                    case "2": keyString = "Alpha2"; break;
                    case "3": keyString = "Alpha3"; break;
                    case "4": keyString = "Alpha4"; break;
                    case "5": keyString = "Alpha5"; break;
                    case "6": keyString = "Alpha6"; break;
                    case "7": keyString = "Alpha7"; break;
                    case "8": keyString = "Alpha8"; break;
                    case "9": keyString = "Alpha9"; break;

                    case ".": case "period": keyString = "Period"; break;
                    case ",": case "comma": keyString = "Comma"; break;
                    case "-": case "minus": keyString = "Minus"; break;
                    case "+": case "plus": keyString = "Plus"; break;
                    case " ": case "space": keyString = "Space"; break;

                    case "esc": case "escape": keyString = "Escape"; break;
                    case "enter": case "return": keyString = "Return"; break;
                    case "ctrl": case "leftctrl": case "leftcontrol": keyString = "LeftControl"; break;
                    case "rightctrl": case "rightcontrol": keyString = "RightControl"; break;
                    case "alt": case "leftalt": keyString = "LeftAlt"; break;
                    case "rightalt": keyString = "RightAlt"; break;
                    case "shift": case "leftshift": keyString = "LeftShift"; break;
                    case "rightshift": keyString = "RightShift"; break;
                    case "tab": keyString = "Tab"; break;
                    case "capslock": case "caps": keyString = "CapsLock"; break;
                }

                KeyCode result;
                if (Enum.TryParse(keyString, true, out result))
                {
                    toggleKey = result;
                    Debug.Log("[TravelMapEnhancements] Tecla configurada con éxito: " + toggleKey.ToString());
                }
                else
                {
                    Debug.LogWarning("[TravelMapEnhancements] ERROR: La tecla ingresada ('" + keyString + "') no es válida. Se usará la tecla 'P' por defecto.");
                }
            }

            try { showFavorites = settings.GetValue<bool>("General", "ShowFavorites"); } catch { }
        }

        public class TravelDashboardWindow : DaggerfallPopupWindow
        {
            struct TravelData
            {
                public string Name;
                public int Region;
                public bool IsHeader;
                public ulong QuestUID;
                public Place QuestPlace; // Guardamos el objeto original del motor
            }

            Panel mainPanel;
            ListBox questListBox;
            ListBox favListBox;
            TextBox locationTextBox;
            Button favModeToggle;

            List<TravelData> activeQuests = new List<TravelData>();
            static List<TravelData> favorites = new List<TravelData>();

            HashSet<ulong> collapsedQuests = new HashSet<ulong>();

            int selectedRegion = -1;
            Place selectedQuestPlace = null; // Chivato de la ubicación exacta
            static bool isGlobalFavs = true;

            static readonly string favPath = Path.Combine(Application.persistentDataPath, "TravelFavsData.txt");
            static readonly string favModePath = Path.Combine(Application.persistentDataPath, "TravelFavsMode.txt");

            public TravelDashboardWindow(IUserInterfaceManager uiManager, Mod mod) : base(uiManager)
            {
                PauseWhileOpen = true;
                LoadFavMode();
                LoadFavs();
            }

            protected override void Setup()
            {
                ParentPanel.BackgroundColor = Color.clear;
                NativePanel.BackgroundColor = Color.clear;

                mainPanel = DaggerfallUI.AddPanel(new Rect(40, 30, 240, 140), NativePanel);
                mainPanel.BackgroundColor = new Color(0, 0, 0, 0.45f);
                mainPanel.Outline.Enabled = true;

                Button closeBtn = DaggerfallUI.AddButton(new Rect(225, 2, 12, 10), mainPanel);
                closeBtn.Label.Text = "X";
                closeBtn.BackgroundColor = new Color(0.6f, 0.1f, 0.1f, 0.8f);
                closeBtn.OnMouseClick += (s, m) => CloseWindow();

                DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, new Vector2(5, 5), "Active Quest Locations", mainPanel);
                questListBox = new ListBox { Position = new Vector2(5, 15), Size = new Vector2(110, 90), RowsDisplayed = 10 };
                mainPanel.Components.Add(questListBox);

                questListBox.OnSelectItem += () => {
                    int idx = questListBox.SelectedIndex;
                    if (idx >= 0 && idx < activeQuests.Count)
                    {
                        var data = activeQuests[idx];
                        if (data.IsHeader)
                        {
                            if (collapsedQuests.Contains(data.QuestUID))
                                collapsedQuests.Remove(data.QuestUID);
                            else
                                collapsedQuests.Add(data.QuestUID);

                            locationTextBox.Text = "";
                            selectedRegion = -1;
                            selectedQuestPlace = null;

                            RefreshLists();
                        }
                        else
                        {
                            locationTextBox.Text = data.Name;
                            selectedRegion = data.Region;
                            selectedQuestPlace = data.QuestPlace; // Guardamos el objeto exacto de la misión
                        }
                    }
                };

                if (TravelMapEnhancements.showFavorites)
                {
                    DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, new Vector2(125, 5), "Favorites", mainPanel);
                    favListBox = new ListBox { Position = new Vector2(125, 15), Size = new Vector2(110, 75), RowsDisplayed = 8 };
                    mainPanel.Components.Add(favListBox);

                    favListBox.OnSelectItem += () => {
                        int idx = favListBox.SelectedIndex;
                        if (idx >= 0 && idx < favorites.Count)
                        {
                            locationTextBox.Text = favorites[idx].Name;
                            selectedRegion = favorites[idx].Region;
                            selectedQuestPlace = null; // Es un favorito, limpiamos el objeto de misión
                        }
                    };

                    CreateButtons();

                    favModeToggle = DaggerfallUI.AddButton(new Rect(125, 103, 110, 10), mainPanel);
                    favModeToggle.BackgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
                    favModeToggle.OnMouseClick += (s, m) => ToggleFavMode();
                }

                locationTextBox = new TextBox { Position = new Vector2(5, 115), Size = new Vector2(180, 12), MaxCharacters = 32 };
                locationTextBox.ReadOnly = true;
                locationTextBox.UseFocus = false;
                locationTextBox.Outline.Enabled = true;
                mainPanel.Components.Add(locationTextBox);

                Button goButton = DaggerfallUI.AddButton(new Rect(190, 115, 45, 12), mainPanel);
                goButton.Label.Text = "GO";
                goButton.BackgroundColor = new Color(0.1f, 0.4f, 0.1f, 0.8f);
                goButton.OnMouseClick += (s, m) => ExecuteTravel();

                RefreshLists();
            }

            public override void OnPush()
            {
                base.OnPush();

                LoadFavMode();
                LoadFavs();

                if (favModeToggle != null)
                    favModeToggle.Label.Text = isGlobalFavs ? "List: Global" : "List: Character";

                if (GameManager.Instance.PlayerMouseLook != null)
                    GameManager.Instance.PlayerMouseLook.cursorActive = true;

                if (locationTextBox != null)
                    locationTextBox.Text = "";

                selectedRegion = -1;
                selectedQuestPlace = null;
                RefreshLists();
            }

            public override void OnPop()
            {
                base.OnPop();
                if (GameManager.Instance.PlayerMouseLook != null)
                    GameManager.Instance.PlayerMouseLook.cursorActive = false;
            }

            void CreateButtons()
            {
                float x = 125; float y = 92;
                Button cur = DaggerfallUI.AddButton(new Rect(x, y, 35, 10), mainPanel);
                cur.Label.Text = "Cur"; cur.OnMouseClick += (s, m) => AddFavCurrent();
                Button add = DaggerfallUI.AddButton(new Rect(x + 37, y, 10, 10), mainPanel); add.Label.Text = "+";
                add.OnMouseClick += (s, m) => AddFavSelected();
                Button del = DaggerfallUI.AddButton(new Rect(x + 49, y, 10, 10), mainPanel); del.Label.Text = "-";
                del.OnMouseClick += (s, m) => RemoveFav();
                Button up = DaggerfallUI.AddButton(new Rect(x + 61, y, 15, 10), mainPanel); up.Label.Text = "Up";
                up.OnMouseClick += (s, m) => MoveFav(-1);
                Button down = DaggerfallUI.AddButton(new Rect(x + 78, y, 22, 10), mainPanel); down.Label.Text = "Dn";
                down.OnMouseClick += (s, m) => MoveFav(1);
            }

            void ToggleFavMode()
            {
                isGlobalFavs = !isGlobalFavs;
                if (favModeToggle != null)
                    favModeToggle.Label.Text = isGlobalFavs ? "List: Global" : "List: Character";

                SaveFavMode();

                if (locationTextBox != null)
                    locationTextBox.Text = "";

                selectedRegion = -1;
                selectedQuestPlace = null;
                LoadFavs();
                RefreshLists();
            }

            void RefreshLists()
            {
                if (questListBox == null) return;

                questListBox.ClearItems();
                activeQuests.Clear();

                var sites = QuestMachine.Instance?.GetAllActiveQuestSites();

                if (sites != null)
                {
                    Dictionary<ulong, List<Place>> questGroups = new Dictionary<ulong, List<Place>>();

                    foreach (var s in sites)
                    {
                        if (string.IsNullOrEmpty(s.locationName) || s.regionIndex < 0) continue;

                        Quest q = QuestMachine.Instance.GetQuest(s.questUID);

                        if (q == null || q.QuestComplete || q.QuestTombstoned)
                            continue;

                        var logMessages = q.GetLogMessages();
                        bool hasLog = (logMessages != null && logMessages.Length > 0);
                        bool isMainStory = !string.IsNullOrEmpty(q.QuestName) && q.QuestName.ToUpper().StartsWith("S0");

                        if (!hasLog && !isMainStory)
                            continue;

                        // Localizamos el Place exacto
                        Place actualPlace = null;
                        var questPlaces = q.GetAllResources(typeof(Place));

                        if (questPlaces != null)
                        {
                            foreach (Place p in questPlaces)
                            {
                                if (p.SiteDetails.mapId == s.mapId)
                                {
                                    actualPlace = p;
                                    break;
                                }
                            }
                        }

                        // Fuera morralla y spoilers ocultos
                        if (actualPlace == null || actualPlace.IsHidden)
                            continue;

                        if (!questGroups.ContainsKey(s.questUID))
                            questGroups[s.questUID] = new List<Place>();

                        questGroups[s.questUID].Add(actualPlace);
                    }

                    foreach (var kvp in questGroups)
                    {
                        ulong qUID = kvp.Key;
                        var qSites = kvp.Value;

                        Quest q = QuestMachine.Instance.GetQuest(qUID);
                        string qName = (q != null && !string.IsNullOrEmpty(q.DisplayName)) ? q.DisplayName : "Unknown Quest";

                        activeQuests.Add(new TravelData { Name = qName, Region = -1, IsHeader = true, QuestUID = qUID, QuestPlace = null });

                        string headerPrefix = collapsedQuests.Contains(qUID) ? "[+] " : "[-] ";
                        questListBox.AddItem(headerPrefix + qName);

                        if (!collapsedQuests.Contains(qUID))
                        {
                            HashSet<string> seen = new HashSet<string>();
                            foreach (var p in qSites)
                            {
                                string locName = p.SiteDetails.locationName;
                                string regName = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(p.SiteDetails.regionIndex);
                                var realLoc = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetLocation(regName, locName);

                                if (!realLoc.Loaded) continue;

                                if (seen.Add(locName))
                                {
                                    activeQuests.Add(new TravelData { Name = locName, Region = p.SiteDetails.regionIndex, IsHeader = false, QuestUID = qUID, QuestPlace = p });
                                    questListBox.AddItem("  - " + locName);
                                }
                            }
                        }
                    }
                }

                if (favListBox != null)
                {
                    favListBox.ClearItems();
                    foreach (var f in favorites) favListBox.AddItem(f.Name);
                }
            }

            void ExecuteTravel()
            {
                if (locationTextBox == null) return;

                string loc = locationTextBox.Text.Trim();
                if (string.IsNullOrEmpty(loc) || selectedRegion == -1) return;

                CloseWindow();

                // Si tenemos el Place exacto (Misión), lo usamos directamente. 
                // Esto es infalible y va a la coordenada exacta del ID, llámese como se llame.
                if (selectedQuestPlace != null)
                {
                    DaggerfallUI.Instance.DfTravelMapWindow.GotoPlace(selectedQuestPlace);
                    DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenTravelMapWindow);
                }
                else
                {
                    // Si es un Favorito, tiramos del sistema clásico de búsqueda por nombre
                    string regName = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetRegionName(selectedRegion);
                    DaggerfallConnect.DFLocation realLocation = DaggerfallUnity.Instance.ContentReader.MapFileReader.GetLocation(regName, loc);

                    if (realLocation.Loaded)
                    {
                        Place fakePlace = new Place(null);
                        SiteDetails fakeDetails = new SiteDetails();

                        fakeDetails.locationName = realLocation.Name;
                        fakeDetails.regionName = realLocation.RegionName;
                        fakeDetails.regionIndex = realLocation.RegionIndex;
                        fakeDetails.locationId = realLocation.Exterior.ExteriorData.LocationId;
                        fakeDetails.mapId = realLocation.MapTableData.MapId;

                        fakePlace.SiteDetails = fakeDetails;

                        DaggerfallUI.Instance.DfTravelMapWindow.GotoPlace(fakePlace);
                        DaggerfallUI.PostMessage(DaggerfallUIMessages.dfuiOpenTravelMapWindow);
                    }
                    else
                    {
                        DaggerfallMessageBox msgBox = new DaggerfallMessageBox(DaggerfallUI.UIManager, null);
                        msgBox.SetText("Error de GPS: El motor no encuentra las coordenadas de " + loc);
                        msgBox.ClickAnywhereToClose = true;
                        msgBox.Show();
                    }
                }
            }

            #region Persistencia de Modos y Rutas
            void SaveFavMode()
            {
                File.WriteAllText(favModePath, isGlobalFavs.ToString());
            }

            void LoadFavMode()
            {
                if (File.Exists(favModePath))
                {
                    string data = File.ReadAllText(favModePath).Trim();
                    bool result;
                    if (bool.TryParse(data, out result)) isGlobalFavs = result;
                }
            }

            string GetFavPath()
            {
                if (isGlobalFavs) return favPath;
                string charName = GameManager.Instance.PlayerEntity.Name;
                foreach (char c in Path.GetInvalidFileNameChars()) { charName = charName.Replace(c, '_'); }
                return Path.Combine(Application.persistentDataPath, $"TravelFavsData_{charName}.txt");
            }

            void AddFavCurrent()
            {
                if (GameManager.Instance.PlayerGPS.IsPlayerInLocationRect)
                {
                    string n = GameManager.Instance.PlayerGPS.CurrentLocation.Name;
                    int r = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
                    if (!favorites.Exists(f => f.Name == n)) { favorites.Add(new TravelData { Name = n, Region = r, IsHeader = false, QuestPlace = null }); SaveFavs(); RefreshLists(); }
                }
            }
            void AddFavSelected()
            {
                if (locationTextBox == null) return;
                string n = locationTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(n) && selectedRegion != -1)
                {
                    if (!favorites.Exists(f => f.Name == n)) { favorites.Add(new TravelData { Name = n, Region = selectedRegion, IsHeader = false, QuestPlace = null }); SaveFavs(); RefreshLists(); }
                }
            }
            void RemoveFav() { if (favListBox != null && favListBox.SelectedIndex >= 0) { favorites.RemoveAt(favListBox.SelectedIndex); SaveFavs(); RefreshLists(); } }
            void MoveFav(int d)
            {
                if (favListBox == null) return;
                int i = favListBox.SelectedIndex; int n = i + d;
                if (i >= 0 && n >= 0 && n < favorites.Count)
                {
                    var t = favorites[i]; favorites[i] = favorites[n]; favorites[n] = t;
                    SaveFavs(); RefreshLists(); favListBox.SelectedIndex = n;
                }
            }
            void SaveFavs()
            {
                var lines = new List<string>();
                foreach (var f in favorites) lines.Add(f.Region + "|" + f.Name);
                File.WriteAllLines(GetFavPath(), lines.ToArray());
            }
            void LoadFavs()
            {
                favorites.Clear();
                string path = GetFavPath();
                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (var l in lines)
                    {
                        var parts = l.Split('|');
                        int r;
                        if (parts.Length == 2 && int.TryParse(parts[0], out r))
                        {
                            favorites.Add(new TravelData { Name = parts[1], Region = r, IsHeader = false, QuestPlace = null });
                        }
                    }
                }
            }
            #endregion
        }
    }
}