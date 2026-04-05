using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using UnityEngine;

namespace VirtualKeyboardInjection
{
    public class VirtualKeyboardInjection : MonoBehaviour
    {
        private static Mod mod;

        private Panel keyboardPanel;
        private TextBox activeTextBox;
        private IUserInterfaceWindow lastWindow;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject(mod.Title);
            go.AddComponent<VirtualKeyboardInjection>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            DaggerfallUI.UIManager.OnWindowChange += UIManager_OnWindowChange;
            mod.IsReady = true;
            Debug.Log("[VirtualKeyboardInjection] Ready");
        }

        private void OnDestroy()
        {
            DaggerfallUI.UIManager.OnWindowChange -= UIManager_OnWindowChange;
            ClearKeyboard();
        }

        private void UIManager_OnWindowChange(object sender, EventArgs e)
        {
            IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;

            if (topWindow == null)
            {
                ClearKeyboard();
                lastWindow = null;
                return;
            }

            if (ReferenceEquals(topWindow, lastWindow))
                return;

            lastWindow = topWindow;
            TryInjectIntoWindow(topWindow);
        }

        private void TryInjectIntoWindow(IUserInterfaceWindow window)
        {
            ClearKeyboard();

            Panel rootPanel = GetRootPanel(window);
            if (rootPanel == null)
                return;

            TextBox target = FindFirstValidTextBox(rootPanel.Components);
            if (target == null)
                return;

            activeTextBox = target;
            CreateKeyboard(rootPanel);
        }

        private static Panel GetRootPanel(IUserInterfaceWindow window)
        {
            DaggerfallBaseWindow baseWindow = window as DaggerfallBaseWindow;
            if (baseWindow == null)
                return null;

            if (baseWindow.ParentPanel != null)
                return baseWindow.ParentPanel;

            return baseWindow.NativePanel;
        }

        private static TextBox FindFirstValidTextBox(IEnumerable<BaseScreenComponent> components)
        {
            if (components == null)
                return null;

            foreach (BaseScreenComponent component in components)
            {
                TextBox textBox = component as TextBox;
                if (textBox != null && !textBox.ReadOnly)
                    return textBox;

                TextBox nested = FindFirstValidTextBox(component.Components);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void CreateKeyboard(Panel parent)
        {
            float panelHeight = 66f;
            keyboardPanel = DaggerfallUI.AddPanel(new Rect(6, parent.InteriorHeight - panelHeight - 4, parent.InteriorWidth - 12, panelHeight), parent);
            keyboardPanel.BackgroundColor = new Color(0f, 0f, 0f, 0.85f);
            keyboardPanel.Outline.Enabled = true;

            AddLetterRows();
            AddSpecialKeys();
        }

        private void AddLetterRows()
        {
            string[] rows =
            {
                "QWERTYUIOP",
                "ASDFGHJKL",
                "ZXCVBNM"
            };

            const float keyWidth = 16f;
            const float keyHeight = 14f;
            const float spacing = 2f;
            const float originY = 4f;

            for (int row = 0; row < rows.Length; row++)
            {
                string rowChars = rows[row];
                float rowWidth = rowChars.Length * keyWidth + (rowChars.Length - 1) * spacing;
                float startX = (keyboardPanel.InteriorWidth - rowWidth) * 0.5f;
                float y = originY + row * (keyHeight + spacing);

                for (int i = 0; i < rowChars.Length; i++)
                {
                    char key = rowChars[i];
                    float x = startX + i * (keyWidth + spacing);

                    Button button = DaggerfallUI.AddButton(new Rect(x, y, keyWidth, keyHeight), keyboardPanel);
                    button.Label.Text = key.ToString();
                    button.BackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.95f);

                    char capturedKey = key;
                    button.OnMouseClick += (s, p) => AppendCharacter(capturedKey);
                }
            }
        }

        private void AddSpecialKeys()
        {
            Button backspaceButton = DaggerfallUI.AddButton(new Rect(6, keyboardPanel.InteriorHeight - 16, 54, 12), keyboardPanel);
            backspaceButton.Label.Text = "Backspace";
            backspaceButton.BackgroundColor = new Color(0.45f, 0.15f, 0.15f, 0.95f);
            backspaceButton.OnMouseClick += (s, p) => Backspace();

            float spaceWidth = keyboardPanel.InteriorWidth - 72f;
            Button spaceButton = DaggerfallUI.AddButton(new Rect(66, keyboardPanel.InteriorHeight - 16, spaceWidth, 12), keyboardPanel);
            spaceButton.Label.Text = "Space";
            spaceButton.BackgroundColor = new Color(0.2f, 0.2f, 0.35f, 0.95f);
            spaceButton.OnMouseClick += (s, p) => AppendCharacter(' ');
        }

        private void AppendCharacter(char c)
        {
            if (activeTextBox == null || activeTextBox.ReadOnly)
                return;

            activeTextBox.Text += c;
        }

        private void Backspace()
        {
            if (activeTextBox == null || string.IsNullOrEmpty(activeTextBox.Text))
                return;

            activeTextBox.Text = activeTextBox.Text.Substring(0, activeTextBox.Text.Length - 1);
        }

        private void ClearKeyboard()
        {
            if (keyboardPanel != null && keyboardPanel.Parent != null)
            {
                keyboardPanel.Parent.Components.Remove(keyboardPanel);
            }

            keyboardPanel = null;
            activeTextBox = null;
        }
    }
}
