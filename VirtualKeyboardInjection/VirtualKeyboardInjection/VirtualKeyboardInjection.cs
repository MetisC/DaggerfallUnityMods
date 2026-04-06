using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Banking;
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
        private TextBox handledTextBox;
        private Panel keyboardParent;
        private string lastLoggedDetectionKey;

        private readonly List<BaseScreenComponent> blockedComponents = new List<BaseScreenComponent>();
        private readonly Dictionary<BaseScreenComponent, bool> blockedComponentStates = new Dictionary<BaseScreenComponent, bool>();

        private static bool isTopPosition = false;
        private static readonly string posFilePath = Path.Combine(Application.persistentDataPath, "VirtualKeyboardPos.txt");

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
            LoadPosition();
            mod.IsReady = true;
            Debug.Log("[VirtualKeyboardInjection] Mod cargado. Comprobando TextBox con Enabled a la antigua.");
        }

        private void OnDestroy()
        {
            HideKeyboard();
        }

        private void Update()
        {
            IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;

            if (topWindow == null)
            {
                if (handledTextBox != null)
                {
                    handledTextBox = null;
                    HideKeyboard();
                }
                return;
            }

            Panel rootPanel = null;
            DaggerfallBaseWindow baseWin = topWindow as DaggerfallBaseWindow;
            if (baseWin != null)
                rootPanel = baseWin.NativePanel;

            TextBox currentTextBox = null;

            DaggerfallInputMessageBox inputBox = topWindow as DaggerfallInputMessageBox;
            if (inputBox != null)
            {
                currentTextBox = inputBox.TextBox;
            }
            else if (rootPanel != null)
            {
                currentTextBox = FindFirstEditableTextBox(rootPanel.Components);
            }

            if (currentTextBox != null && !currentTextBox.Enabled)
                currentTextBox = null;

            // Si el teclado está visible, no entremos en pánico por un frame suelto
            // en el que la búsqueda no devuelva la caja actual por cambios internos de la UI.
            if (currentTextBox == null && keyboardPanel != null && handledTextBox != null)
                currentTextBox = handledTextBox;

            if (currentTextBox != handledTextBox)
            {
                HideKeyboard();
                handledTextBox = currentTextBox;

                if (handledTextBox != null && rootPanel != null)
                {
                    LogDetection(topWindow, handledTextBox);

                    handledTextBox.OnMouseClick -= HandledTextBox_OnMouseClick;
                    handledTextBox.OnMouseClick += HandledTextBox_OnMouseClick;

                    CreateKeyboard(rootPanel);
                }
                else
                {
                    ClearDetectionLogKeyIfNeeded(handledTextBox);
                }
            }
        }

        private void LogDetection(IUserInterfaceWindow topWindow, TextBox textBox)
        {
            if (topWindow == null || textBox == null)
                return;

            string key = topWindow.GetType().FullName + "|" + textBox.GetHashCode();
            if (key == lastLoggedDetectionKey)
                return;

            lastLoggedDetectionKey = key;

            Debug.Log(string.Format(
                "[VirtualKeyboardInjection] TextBox detectada | Window={0} | TextBox={1} | Numeric={2} | ReadOnly={3} | Enabled={4} | MaxChars={5} | Text='{6}'",
                topWindow.GetType().Name,
                textBox.GetType().Name,
                textBox.Numeric,
                textBox.ReadOnly,
                textBox.Enabled,
                textBox.MaxCharacters,
                textBox.Text));
        }

        private void ClearDetectionLogKeyIfNeeded(TextBox currentTextBox)
        {
            if (currentTextBox == null)
                lastLoggedDetectionKey = null;
        }

        private void LogEnterRoute(string routeName, IUserInterfaceWindow topWindow)
        {
            string windowName = topWindow != null ? topWindow.GetType().Name : "<null>";
            Debug.Log(string.Format("[VirtualKeyboardInjection] DONE -> {0} | Window={1}", routeName, windowName));
        }

        private void HandledTextBox_OnMouseClick(BaseScreenComponent sender, Vector2 position)
        {
            if (keyboardPanel == null && handledTextBox != null && handledTextBox.Enabled)
            {
                IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;
                DaggerfallBaseWindow baseWin = topWindow as DaggerfallBaseWindow;
                if (baseWin != null && baseWin.NativePanel != null)
                {
                    CreateKeyboard(baseWin.NativePanel);
                }
            }
        }

        private static TextBox FindFirstEditableTextBox(ScreenComponentCollection components)
        {
            if (components == null) return null;

            foreach (BaseScreenComponent component in components)
            {
                TextBox textBox = component as TextBox;
                if (textBox != null && !textBox.ReadOnly && textBox.Enabled)
                    return textBox;

                Panel panel = component as Panel;
                if (panel != null && panel.Enabled)
                {
                    TextBox nested = FindFirstEditableTextBox(panel.Components);
                    if (nested != null) return nested;
                }
            }

            return null;
        }

        private void CreateKeyboard(Panel parent)
        {
            float screenWidth = 320f;
            float panelHeight = 74f;
            float yPos = isTopPosition ? 2f : (parent.InteriorHeight - panelHeight - 2f);

            keyboardParent = parent;
            keyboardPanel = DaggerfallUI.AddPanel(new Rect(6, yPos, screenWidth - 12, panelHeight), parent);
            keyboardPanel.BackgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            keyboardPanel.Outline.Enabled = true;
            keyboardPanel.UseFocus = true;

            keyboardPanel.OnMouseClick += (s, p) => { };
            keyboardPanel.OnRightMouseClick += (s, p) => { };
            keyboardPanel.OnMiddleMouseClick += (s, p) => { };
            keyboardPanel.OnMouseDown += (s, p) => { };
            keyboardPanel.OnMouseUp += (s, p) => { };
            keyboardPanel.OnMouseScrollDown += (s) => { };
            keyboardPanel.OnMouseScrollUp += (s) => { };

            BlockUnderlyingComponents(parent);

            Button posBtn = DaggerfallUI.AddButton(new Rect(keyboardPanel.InteriorWidth - 40, 2, 24, 10), keyboardPanel);
            posBtn.Label.Text = "Pos";
            posBtn.BackgroundColor = new Color(0.1f, 0.4f, 0.6f, 0.95f);
            posBtn.OnMouseClick += (s, p) => TogglePosition();

            Button closeBtn = DaggerfallUI.AddButton(new Rect(keyboardPanel.InteriorWidth - 14, 2, 12, 10), keyboardPanel);
            closeBtn.Label.Text = "X";
            closeBtn.BackgroundColor = new Color(0.6f, 0.1f, 0.1f, 0.95f);
            closeBtn.OnMouseClick += (s, p) => HideKeyboard();

            AddLetterRows();
            AddNumpad();
            AddSpecialKeys();
        }

        private void BlockUnderlyingComponents(Panel parent)
        {
            RestoreBlockedComponents();

            if (parent == null || keyboardPanel == null)
                return;

            Rect keyboardRect = keyboardPanel.Rectangle;

            foreach (BaseScreenComponent component in parent.Components)
            {
                if (component == null || component == keyboardPanel)
                    continue;

                if (!component.Enabled)
                    continue;

                // Jamás desactivar la caja que estamos usando ni ningún contenedor suyo,
                // o entraremos en un bucle show/hide de campeonato.
                if (BelongsToHandledTextBox(component))
                    continue;

                if (component.Rectangle.Overlaps(keyboardRect))
                {
                    blockedComponents.Add(component);
                    blockedComponentStates[component] = component.Enabled;
                    component.Enabled = false;
                }
            }
        }

        private bool BelongsToHandledTextBox(BaseScreenComponent component)
        {
            if (component == null || handledTextBox == null)
                return false;

            BaseScreenComponent current = handledTextBox;
            while (current != null)
            {
                if (current == component)
                    return true;

                current = current.Parent as BaseScreenComponent;
            }

            return false;
        }

        private void RestoreBlockedComponents()
        {
            for (int i = 0; i < blockedComponents.Count; i++)
            {
                BaseScreenComponent component = blockedComponents[i];
                if (component == null)
                    continue;

                bool previousState;
                if (blockedComponentStates.TryGetValue(component, out previousState))
                    component.Enabled = previousState;
            }

            blockedComponents.Clear();
            blockedComponentStates.Clear();
        }

        private void AddLetterRows()
        {
            string[] rows = { "QWERTYUIOP", "ASDFGHJKL", "ZXCVBNM" };
            const float keyWidth = 16f;
            const float keyHeight = 11f;
            const float spacing = 2f;
            const float originY = 18f;

            for (int row = 0; row < rows.Length; row++)
            {
                string rowChars = rows[row];
                float rowWidth = rowChars.Length * keyWidth + (rowChars.Length - 1) * spacing;
                float startX = (keyboardPanel.InteriorWidth - rowWidth) * 0.35f;
                float y = originY + row * (keyHeight + spacing);

                for (int i = 0; i < rowChars.Length; i++)
                {
                    char key = rowChars[i];
                    float x = startX + i * (keyWidth + spacing);
                    Button button = DaggerfallUI.AddButton(new Rect(x, y, keyWidth, keyHeight), keyboardPanel);
                    button.Label.Text = key.ToString();
                    button.BackgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.95f);

                    char capturedKey = key;
                    button.OnMouseClick += (s, p) => AppendCharacter(capturedKey);
                }
            }
        }

        private void AddNumpad()
        {
            const float keyWidth = 14f;
            const float keyHeight = 11f;
            const float spacing = 2f;
            const float originY = 18f;

            float startX = keyboardPanel.InteriorWidth - (keyWidth * 3) - (spacing * 2) - 10f;

            string[] rows = { "789", "456", "123" };
            for (int row = 0; row < rows.Length; row++)
            {
                string rowChars = rows[row];
                float y = originY + row * (keyHeight + spacing);
                for (int i = 0; i < rowChars.Length; i++)
                {
                    char key = rowChars[i];
                    float x = startX + i * (keyWidth + spacing);
                    Button btn = DaggerfallUI.AddButton(new Rect(x, y, keyWidth, keyHeight), keyboardPanel);
                    btn.Label.Text = key.ToString();
                    btn.BackgroundColor = new Color(0.2f, 0.3f, 0.2f, 0.95f);

                    char capturedKey = key;
                    btn.OnMouseClick += (s, p) => AppendCharacter(capturedKey);
                }
            }

            float zeroY = originY + 3 * (keyHeight + spacing);
            Button zeroBtn = DaggerfallUI.AddButton(new Rect(startX, zeroY, keyWidth * 2 + spacing, keyHeight), keyboardPanel);
            zeroBtn.Label.Text = "0";
            zeroBtn.BackgroundColor = new Color(0.2f, 0.3f, 0.2f, 0.95f);
            zeroBtn.OnMouseClick += (s, p) => AppendCharacter('0');
        }

        private void AddSpecialKeys()
        {
            const float keyHeight = 11f;
            const float spacing = 2f;
            const float originY = 18f;
            float bottomY = originY + 3 * (keyHeight + spacing);

            float row0Width = 10 * 16f + 9 * 2f;
            float startX0 = (keyboardPanel.InteriorWidth - row0Width) * 0.35f;
            float bkspX = startX0 + row0Width + spacing;
            float numpadStartX = keyboardPanel.InteriorWidth - (14f * 3) - (spacing * 2) - 10f;
            float bkspWidth = numpadStartX - bkspX - spacing;

            Button backspaceButton = DaggerfallUI.AddButton(new Rect(bkspX, originY, bkspWidth, keyHeight), keyboardPanel);
            backspaceButton.Label.Text = "Bksp";
            backspaceButton.BackgroundColor = new Color(0.5f, 0.2f, 0.2f, 0.95f);
            backspaceButton.OnMouseClick += (s, p) => Backspace();

            float doneWidth = 44f;
            float doneX = numpadStartX - doneWidth - 4f;
            Button doneButton = DaggerfallUI.AddButton(new Rect(doneX, bottomY, doneWidth, keyHeight), keyboardPanel);
            doneButton.Label.Text = "Done";
            doneButton.BackgroundColor = new Color(0.2f, 0.5f, 0.2f, 0.95f);
            doneButton.OnMouseClick += (s, p) => SimulateEnter();

            float aposWidth = 14f;
            float aposX = doneX - aposWidth - 4f;
            Button aposButton = DaggerfallUI.AddButton(new Rect(aposX, bottomY, aposWidth, keyHeight), keyboardPanel);
            aposButton.Label.Text = "'";
            aposButton.BackgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
            aposButton.OnMouseClick += (s, p) => AppendCharacter('\'');

            float dashWidth = 16f;
            float dashX = startX0;
            Button dashButton = DaggerfallUI.AddButton(new Rect(dashX, bottomY, dashWidth, keyHeight), keyboardPanel);
            dashButton.Label.Text = "-";
            dashButton.BackgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
            dashButton.OnMouseClick += (s, p) => AppendCharacter('-');

            float dotX = dashX + dashWidth + spacing;
            Button dotButton = DaggerfallUI.AddButton(new Rect(dotX, bottomY, dashWidth, keyHeight), keyboardPanel);
            dotButton.Label.Text = ".";
            dotButton.BackgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.95f);
            dotButton.OnMouseClick += (s, p) => AppendCharacter('.');

            float spaceX = dotX + dashWidth + spacing;
            float spaceWidth = aposX - spaceX - 4f;
            Button spaceButton = DaggerfallUI.AddButton(new Rect(spaceX, bottomY, spaceWidth, keyHeight), keyboardPanel);
            spaceButton.Label.Text = "Space";
            spaceButton.BackgroundColor = new Color(0.2f, 0.2f, 0.4f, 0.95f);
            spaceButton.OnMouseClick += (s, p) => AppendCharacter(' ');
        }

        private void AppendCharacter(char c)
        {
            if (handledTextBox == null || handledTextBox.ReadOnly) return;
            handledTextBox.Text += c;
        }

        private void Backspace()
        {
            if (handledTextBox == null || string.IsNullOrEmpty(handledTextBox.Text)) return;
            handledTextBox.Text = handledTextBox.Text.Substring(0, handledTextBox.Text.Length - 1);
        }

        private void SimulateEnter()
        {
            if (handledTextBox == null)
                return;

            try
            {
                IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;
                if (topWindow == null)
                    return;

                if (topWindow is DaggerfallInputMessageBox)
                {
                    LogEnterRoute("InputMessageBox.Accept", topWindow);
                    DaggerfallInputMessageBox msgBox = (DaggerfallInputMessageBox)topWindow;
                    msgBox.textBox_OnAcceptUserInputHandler(handledTextBox, handledTextBox.Text);
                    handledTextBox = null;
                    HideKeyboard();
                    return;
                }

                Type windowType = topWindow.GetType();

                MethodInfo bankingSubmitMethod = windowType.GetMethod("HandleTransactionInput", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (bankingSubmitMethod != null)
                {
                    LogEnterRoute("Banking.HandleTransactionInput", topWindow);
                    bankingSubmitMethod.Invoke(topWindow, null);

                    MethodInfo bankingCloseInputMethod = windowType.GetMethod("ToggleTransactionInput", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (bankingCloseInputMethod != null)
                    {
                        ParameterInfo[] parameters = bankingCloseInputMethod.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum)
                        {
                            object noneValue = Enum.Parse(parameters[0].ParameterType, "None");
                            bankingCloseInputMethod.Invoke(topWindow, new object[] { noneValue });
                        }
                    }

                    handledTextBox = null;
                    HideKeyboard();
                    return;
                }

                MethodInfo saveLoadMethod = windowType.GetMethod("SaveLoadEventHandler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (saveLoadMethod != null)
                {
                    ParameterInfo[] parameters = saveLoadMethod.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == typeof(BaseScreenComponent) && parameters[1].ParameterType == typeof(Vector2))
                    {
                        LogEnterRoute("SaveGame.SaveLoadEventHandler", topWindow);
                        saveLoadMethod.Invoke(topWindow, new object[] { null, Vector2.zero });
                        handledTextBox = null;
                        HideKeyboard();
                        return;
                    }
                }

                MethodInfo findFromFilterTextMethod = windowType.GetMethod("FindFromFilterText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (findFromFilterTextMethod != null)
                {
                    LogEnterRoute("TravelMap.FindFromFilterText", topWindow);
                    findFromFilterTextMethod.Invoke(topWindow, null);
                    handledTextBox = null;
                    HideKeyboard();
                    return;
                }

                MethodInfo acceptNameMethod = windowType.GetMethod("AcceptName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (acceptNameMethod != null)
                {
                    LogEnterRoute("CreateChar.AcceptName", topWindow);
                    acceptNameMethod.Invoke(topWindow, null);
                    handledTextBox = null;
                    HideKeyboard();
                    return;
                }

                Debug.Log("[VirtualKeyboardInjection] ERROR: No encontré rutina de Enter para " + windowType.Name);
            }
            catch (Exception ex)
            {
                Debug.Log("[VirtualKeyboardInjection] Error crítico en SimulateEnter: " + ex);
            }
        }

        #region Persistencia y Ocultacion

        private void TogglePosition()
        {
            isTopPosition = !isTopPosition;
            SavePosition();

            Panel parent = keyboardParent;
            HideKeyboard();
            if (parent != null) CreateKeyboard(parent);
        }

        private void LoadPosition()
        {
            if (File.Exists(posFilePath))
            {
                string data = File.ReadAllText(posFilePath).Trim();
                bool result;
                if (bool.TryParse(data, out result)) isTopPosition = result;
            }
        }

        private void SavePosition()
        {
            File.WriteAllText(posFilePath, isTopPosition.ToString());
        }

        private void HideKeyboard()
        {
            RestoreBlockedComponents();

            if (keyboardPanel != null)
            {
                Panel parentPanel = keyboardPanel.Parent as Panel;
                if (parentPanel != null)
                    parentPanel.Components.Remove(keyboardPanel);

                keyboardPanel = null;
            }

            keyboardParent = null;
        }
        #endregion
    }
}
