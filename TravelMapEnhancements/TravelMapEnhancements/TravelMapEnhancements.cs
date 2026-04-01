using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using HarmonyLib;
using DaggerfallWorkshop.Game.Questing;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace TravelMapEnhancements
{
    public class TravelMapEnhancements : MonoBehaviour
    {
        static Mod mod;
        static bool showRelevantQuestLocations;
        static bool showFavoriteLocations;
        static bool showTravelMapVirtualKeyboard;
        static readonly List<string> favoriteLocations = new List<string>();
        const int MaxCustomLocationEntries = 64;
        const char FavoriteLocationsSeparator = '|';

        static readonly Dictionary<DaggerfallTravelMapWindow, TravelMapUiState> stateByWindow = new Dictionary<DaggerfallTravelMapWindow, TravelMapUiState>();

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<TravelMapEnhancements>();
        }

        void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            new Harmony(mod.Title).PatchAll(typeof(TravelMapEnhancements));
            mod.IsReady = true;
            Debug.Log("[TravelMapEnhancements] Ready");
        }

        static void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            if (change.HasChanged("General", "ShowRelevantQuestLocations"))
                showRelevantQuestLocations = settings.GetValue<bool>("General", "ShowRelevantQuestLocations");

            if (change.HasChanged("General", "ShowFavoriteLocations"))
                showFavoriteLocations = settings.GetValue<bool>("General", "ShowFavoriteLocations");

            if (change.HasChanged("General", "ShowTravelMapVirtualKeyboard"))
                showTravelMapVirtualKeyboard = settings.GetValue<bool>("General", "ShowTravelMapVirtualKeyboard");

            if (change.HasChanged("General", "TravelMapFavoriteLocations"))
                LoadFavoriteLocations(settings.GetValue<string>("General", "TravelMapFavoriteLocations"));
        }

        static void LoadFavoriteLocations(string serialized)
        {
            favoriteLocations.Clear();
            if (string.IsNullOrEmpty(serialized))
                return;

            string[] parts = serialized.Split(FavoriteLocationsSeparator);
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part) && !favoriteLocations.Contains(part))
                    favoriteLocations.Add(part);
            }
        }

        static void SaveFavoriteLocations()
        {
            string serialized = string.Join(FavoriteLocationsSeparator.ToString(), favoriteLocations.ToArray());
            mod.Settings.SetValue("General", "TravelMapFavoriteLocations", serialized);
            mod.SaveSettings();
        }

        [HarmonyPatch(typeof(DaggerfallTravelMapWindow), "Setup")]
        class TravelMapSetupPatch
        {
            static void Postfix(DaggerfallTravelMapWindow __instance)
            {
                EnsureUi(__instance);
                RefreshAll(__instance);
            }
        }

        [HarmonyPatch(typeof(DaggerfallTravelMapWindow), "OnPush")]
        class TravelMapOnPushPatch
        {
            static void Postfix(DaggerfallTravelMapWindow __instance)
            {
                if (stateByWindow.ContainsKey(__instance))
                    RefreshAll(__instance);
            }
        }

        [HarmonyPatch(typeof(DaggerfallTravelMapWindow), "Update")]
        class TravelMapUpdatePatch
        {
            static void Postfix(DaggerfallTravelMapWindow __instance)
            {
                if (!stateByWindow.TryGetValue(__instance, out TravelMapUiState state))
                    return;

                state.QuestPanel.Enabled = showRelevantQuestLocations;
                state.FavoritesPanel.Enabled = showFavoriteLocations;
                state.KeyboardPanel.Enabled = showTravelMapVirtualKeyboard;

                if (state.FilterTextBox.HasFocus() && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    CopyFilterToSearchBox(__instance, state.FilterTextBox.Text);
            }
        }

        static void EnsureUi(DaggerfallTravelMapWindow window)
        {
            if (stateByWindow.ContainsKey(window))
                return;

            Panel nativePanel = window.NativePanel;
            if (nativePanel == null)
                return;

            TravelMapUiState state = new TravelMapUiState();

            state.FilterTextBox = new TextBox
            {
                Position = new Vector2(3, 164),
                Size = new Vector2(224, 10),
                MaxCharacters = 32,
                UseFocus = true,
                TextColor = DaggerfallUI.DaggerfallDefaultTextColor,
                BackgroundColor = new Color(0, 0, 0, 0.65f)
            };
            state.FilterTextBox.Outline.Enabled = true;
            nativePanel.Components.Add(state.FilterTextBox);

            state.QuestPanel = BuildListPanel(nativePanel, new Vector2(3, 92), new Vector2(106, 70), out state.QuestListBox);
            state.QuestListBox.RowsDisplayed = 6;
            state.QuestListBox.OnMouseClick += (s, p) =>
            {
                if (state.QuestListBox.SelectedIndex >= 0)
                    state.FilterTextBox.Text = state.QuestListBox.SelectedItem;
            };

            state.FavoritesPanel = BuildListPanel(nativePanel, new Vector2(112, 92), new Vector2(115, 70), out state.FavoriteListBox);
            state.FavoriteListBox.RowsDisplayed = 5;
            state.FavoriteListBox.Size = new Vector2(107, 49);
            state.FavoriteListBox.OnMouseClick += (s, p) =>
            {
                if (state.FavoriteListBox.SelectedIndex >= 0)
                    state.FilterTextBox.Text = state.FavoriteListBox.SelectedItem;
            };

            Button addCurrent = DaggerfallUI.AddButton(new Rect(2, 54, 36, 14), state.FavoritesPanel);
            addCurrent.Label.Text = "+ actual";
            addCurrent.Label.TextScale = 0.5f;
            addCurrent.OnMouseClick += (s, p) => AddFavorite(GetCurrentSearchBoxText(window));

            Button addFilter = DaggerfallUI.AddButton(new Rect(40, 54, 19, 14), state.FavoritesPanel);
            addFilter.Label.Text = "+";
            addFilter.OnMouseClick += (s, p) => AddFavorite(state.FilterTextBox.Text);

            Button remove = DaggerfallUI.AddButton(new Rect(61, 54, 19, 14), state.FavoritesPanel);
            remove.Label.Text = "-";
            remove.OnMouseClick += (s, p) =>
            {
                if (state.FavoriteListBox.SelectedIndex >= 0)
                {
                    favoriteLocations.Remove(state.FavoriteListBox.SelectedItem);
                    SaveFavoriteLocations();
                    RefreshFavorites(state);
                }
            };

            state.KeyboardPanel = new Panel
            {
                Position = new Vector2(3, 52),
                Size = new Vector2(224, 38),
                BackgroundColor = new Color(0, 0, 0, 0.6f)
            };
            state.KeyboardPanel.Outline.Enabled = true;
            nativePanel.Components.Add(state.KeyboardPanel);
            BuildKeyboard(state);

            stateByWindow[window] = state;
        }

        static Panel BuildListPanel(Panel root, Vector2 position, Vector2 size, out ListBox listBox)
        {
            Panel panel = new Panel { Position = position, Size = size, BackgroundColor = new Color(0, 0, 0, 0.6f) };
            panel.Outline.Enabled = true;
            root.Components.Add(panel);

            listBox = new ListBox { Position = new Vector2(2, 2), Size = new Vector2(size.x - 8, size.y - 8) };
            panel.Components.Add(listBox);
            return panel;
        }

        static void BuildKeyboard(TravelMapUiState state)
        {
            string chars = "1234567890QWERTYUIOPASDFGHJKLZXCVBNM";
            const int keyWidth = 20;
            const int keyHeight = 8;
            const int keyPadding = 2;
            const int keysPerRow = 10;

            for (int i = 0; i < chars.Length; i++)
            {
                int row = i / keysPerRow;
                int col = i % keysPerRow;
                string character = chars[i].ToString();
                Button key = DaggerfallUI.AddButton(new Rect(2 + col * (keyWidth + keyPadding), 2 + row * (keyHeight + keyPadding), keyWidth, keyHeight), state.KeyboardPanel);
                key.Label.Text = character;
                key.Label.TextScale = 0.7f;
                key.OnMouseClick += (s, p) => state.FilterTextBox.Text += character;
            }

            Button space = DaggerfallUI.AddButton(new Rect(2, 2 + 4 * (keyHeight + keyPadding), 218, keyHeight), state.KeyboardPanel);
            space.Label.Text = "[ ]";
            space.Label.TextScale = 0.7f;
            space.OnMouseClick += (s, p) => state.FilterTextBox.Text += " ";
        }

        static void RefreshAll(DaggerfallTravelMapWindow window)
        {
            if (!stateByWindow.TryGetValue(window, out TravelMapUiState state))
                return;

            state.QuestPanel.Enabled = showRelevantQuestLocations;
            state.FavoritesPanel.Enabled = showFavoriteLocations;
            state.KeyboardPanel.Enabled = showTravelMapVirtualKeyboard;
            RefreshFavorites(state);
            RefreshQuestLocations(state);
        }

        static void RefreshFavorites(TravelMapUiState state)
        {
            state.FavoriteListBox.ClearItems();
            foreach (string location in favoriteLocations)
                state.FavoriteListBox.AddItem(location);
        }

        static void RefreshQuestLocations(TravelMapUiState state)
        {
            state.QuestListBox.ClearItems();
            var sites = QuestMachine.Instance != null ? QuestMachine.Instance.GetAllActiveQuestSites() : null;
            if (sites == null)
                return;

            foreach (var site in sites)
            {
                if (!string.IsNullOrEmpty(site.SiteName))
                    state.QuestListBox.AddItem(site.SiteName);
            }
        }

        static void AddFavorite(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string trimmed = text.Trim();
            if (favoriteLocations.Contains(trimmed) || favoriteLocations.Count >= MaxCustomLocationEntries)
                return;

            favoriteLocations.Add(trimmed);
            SaveFavoriteLocations();
        }

        static void CopyFilterToSearchBox(DaggerfallTravelMapWindow window, string filterText)
        {
            string text = string.IsNullOrWhiteSpace(filterText) ? string.Empty : filterText.Trim();
            FieldInfo field = AccessTools.Field(window.GetType(), "locationNameTextBox");
            if (field == null)
                return;

            TextBox mapSearchBox = field.GetValue(window) as TextBox;
            if (mapSearchBox != null)
                mapSearchBox.Text = text;
        }

        static string GetCurrentSearchBoxText(DaggerfallTravelMapWindow window)
        {
            FieldInfo field = AccessTools.Field(window.GetType(), "locationNameTextBox");
            TextBox mapSearchBox = field != null ? field.GetValue(window) as TextBox : null;
            return mapSearchBox != null ? mapSearchBox.Text : string.Empty;
        }

        class TravelMapUiState
        {
            public TextBox FilterTextBox;
            public Panel QuestPanel;
            public ListBox QuestListBox;
            public Panel FavoritesPanel;
            public ListBox FavoriteListBox;
            public Panel KeyboardPanel;
        }
    }
}
