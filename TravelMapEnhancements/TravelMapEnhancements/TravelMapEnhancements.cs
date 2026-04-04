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

        // EL FLAG DEL CONFIG: Ponlo a false desde tus settings para ocultar todo lo de Favoritos
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
            // FIX DEL CURSOR Y EL TOGGLE:
            if (Input.GetKeyUp(toggleKey))
            {
                // 1. Si la ventana ya existe y es la que está activa arriba del todo, ¡la chapamos!
                if (dashboardInstance != null && DaggerfallUI.UIManager.TopWindow == dashboardInstance)
                {
                    dashboardInstance.CloseWindow();
                }
                // 2. Si no, y estamos en pleno juego sin otros menús estorbando, la abrimos
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
                // Lo pasamos a minúsculas y le quitamos los espacios para que sea a prueba de manazas
                keyString = keyString.Trim().ToLower();

                // EL TRADUCTOR UNIVERSAL (De humano a Unity)
                switch (keyString)
                {
                    // Números (El usuario pone "1", Unity exige "Alpha1")
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

                    // Símbolos clásicos
                    case ".": case "period": keyString = "Period"; break;
                    case ",": case "comma": keyString = "Comma"; break;
                    case "-": case "minus": keyString = "Minus"; break;
                    case "+": case "plus": keyString = "Plus"; break;
                    case " ": case "space": keyString = "Space"; break;

                    // Teclas de control y otras hierbas
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

                // FIX C# 6.0
                KeyCode result;

                // El 'true' ignora las mayúsculas de lo que quede (ej: "m" funcionará como "M")
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
            }

            Panel mainPanel;
            ListBox questListBox;
            ListBox favListBox;
            TextBox locationTextBox;
            Button favModeToggle;

            List<TravelData> activeQuests = new List<TravelData>();
            static List<TravelData> favorites = new List<TravelData>();

            int selectedRegion = -1;
            static bool isGlobalFavs = true;

            static readonly string favPath = Path.Combine(Application.persistentDataPath, "TravelFavsData.txt");
            static readonly string favModePath = Path.Combine(Application.persistentDataPath, "TravelFavsMode.txt");

            public TravelDashboardWindow(IUserInterfaceManager uiManager, Mod mod) : base(uiManager)
            {
                PauseWhileOpen = true;

                // Carga inicial para que la primera vez no salga vacío
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
                        locationTextBox.Text = activeQuests[idx].Name;
                        selectedRegion = activeQuests[idx].Region;
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

                // Forzamos carga por si hemos cambiado de personaje en la misma sesión
                LoadFavMode();
                LoadFavs();

                if (favModeToggle != null)
                    favModeToggle.Label.Text = isGlobalFavs ? "List: Global" : "List: Character";

                if (GameManager.Instance.PlayerMouseLook != null)
                    GameManager.Instance.PlayerMouseLook.cursorActive = true;

                // FIX DEL CRASH SILENCIOSO: Comprobamos que el textbox ya esté creado
                if (locationTextBox != null)
                    locationTextBox.Text = "";

                selectedRegion = -1;
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
                LoadFavs();
                RefreshLists();
            }

            void RefreshLists()
            {
                if (questListBox != null)
                {
                    questListBox.ClearItems();
                    activeQuests.Clear();
                    var sites = QuestMachine.Instance?.GetAllActiveQuestSites();
                    HashSet<string> seen = new HashSet<string>();
                    if (sites != null)
                    {
                        foreach (var s in sites)
                        {
                            if (!string.IsNullOrEmpty(s.locationName) && seen.Add(s.locationName))
                            {
                                activeQuests.Add(new TravelData { Name = s.locationName, Region = s.regionIndex });
                                questListBox.AddItem(s.locationName);
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

                    // FIX MAPA ZOMBI: Usar instancia global
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
                    // FIX C# 6.0
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
                    if (!favorites.Exists(f => f.Name == n)) { favorites.Add(new TravelData { Name = n, Region = r }); SaveFavs(); RefreshLists(); }
                }
            }
            void AddFavSelected()
            {
                if (locationTextBox == null) return;
                string n = locationTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(n) && selectedRegion != -1)
                {
                    if (!favorites.Exists(f => f.Name == n)) { favorites.Add(new TravelData { Name = n, Region = selectedRegion }); SaveFavs(); RefreshLists(); }
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
                        // FIX C# 6.0
                        int r;
                        if (parts.Length == 2 && int.TryParse(parts[0], out r))
                        {
                            favorites.Add(new TravelData { Name = parts[1], Region = r });
                        }
                    }
                }
            }
            #endregion
        }
    }
}