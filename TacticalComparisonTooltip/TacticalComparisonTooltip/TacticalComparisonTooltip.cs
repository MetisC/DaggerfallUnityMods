using System;
using System.Collections.Generic;
using System.Reflection;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using UnityEngine;

namespace TacticalComparisonTooltip
{
    public class TacticalComparisonTooltip : MonoBehaviour
    {
        private const string HeaderText = "EQUIP IMPACT";
        private static Mod mod;

        private Panel tooltipPanel;
        private TextLabel headerLabel;
        private readonly List<TextLabel> lineLabels = new List<TextLabel>();

        private DaggerfallUnityItem lastHoveredItem;
        private IUserInterfaceWindow lastWindow;

        private readonly Color panelBackground = new Color(0f, 0f, 0f, 0.78f);
        private readonly Color textGreen = new Color(0.3f, 1f, 0.3f, 1f);
        private readonly Color textRed = new Color(1f, 0.3f, 0.3f, 1f);
        private readonly Color textWhite = Color.white;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;

            GameObject go = new GameObject(mod.Title);
            go.AddComponent<TacticalComparisonTooltip>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            mod.IsReady = true;
            Debug.Log("[TacticalComparisonTooltip] Ready");
        }

        private void OnDestroy()
        {
            RemoveTooltipPanel();
        }

        private void Update()
        {
            IUserInterfaceWindow topWindow = DaggerfallUI.UIManager.TopWindow;
            DaggerfallInventoryWindow inventoryWindow = topWindow as DaggerfallInventoryWindow;

            if (inventoryWindow == null)
            {
                lastHoveredItem = null;
                lastWindow = topWindow;
                RemoveTooltipPanel();
                return;
            }

            DaggerfallUnityItem hoveredItem = GetHoveredItem(inventoryWindow);
            if (hoveredItem == null)
            {
                lastHoveredItem = null;
                lastWindow = topWindow;
                RemoveTooltipPanel();
                return;
            }

            if (hoveredItem != lastHoveredItem || topWindow != lastWindow)
            {
                lastHoveredItem = hoveredItem;
                lastWindow = topWindow;
                RefreshTooltip(inventoryWindow, hoveredItem);
            }

            if (tooltipPanel != null)
            {
                UpdatePanelPosition(inventoryWindow);
            }
        }

        private DaggerfallUnityItem GetHoveredItem(DaggerfallInventoryWindow inventoryWindow)
        {
            if (inventoryWindow == null)
                return null;

            DaggerfallUnityItem item = TryReadItemMember(inventoryWindow, "MouseOverItem");
            if (item != null)
                return item;

            item = TryReadItemMember(inventoryWindow, "mouseOverItem");
            if (item != null)
                return item;

            item = TryReadItemMember(inventoryWindow, "HoveredItem");
            if (item != null)
                return item;

            item = TryReadItemMember(inventoryWindow, "hoveredItem");
            if (item != null)
                return item;

            item = TryInvokeItemMethod(inventoryWindow, "GetMouseOverItem");
            if (item != null)
                return item;

            return null;
        }

        private DaggerfallUnityItem TryReadItemMember(object target, string memberName)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
            {
                object value = property.GetValue(target, null);
                DaggerfallUnityItem item = value as DaggerfallUnityItem;
                if (item != null)
                    return item;
            }

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
            {
                object value = field.GetValue(target);
                DaggerfallUnityItem item = value as DaggerfallUnityItem;
                if (item != null)
                    return item;
            }

            return null;
        }

        private DaggerfallUnityItem TryInvokeItemMethod(object target, string methodName)
        {
            if (target == null)
                return null;

            Type type = target.GetType();
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = type.GetMethod(methodName, flags, null, Type.EmptyTypes, null);

            if (method == null)
                return null;

            object value = method.Invoke(target, null);
            return value as DaggerfallUnityItem;
        }

        private void RefreshTooltip(DaggerfallInventoryWindow inventoryWindow, DaggerfallUnityItem hoveredItem)
        {
            RemoveTooltipPanel();

            DaggerfallEntityBehaviour playerBehaviour = GameManager.Instance.PlayerEntityBehaviour;
            if (playerBehaviour == null)
                return;

            PlayerEntity player = playerBehaviour.Entity as PlayerEntity;
            if (player == null)
                return;

            List<ImpactLine> lines = BuildImpactLines(player, hoveredItem);
            if (lines.Count == 0)
                return;

            CreateTooltipPanel(inventoryWindow, lines);
        }

        private List<ImpactLine> BuildImpactLines(PlayerEntity player, DaggerfallUnityItem hoveredItem)
        {
            List<ImpactLine> lines = new List<ImpactLine>();

            if (IsForbidden(player, hoveredItem))
            {
                lines.Add(new ImpactLine("!! UNUSABLE: FORBIDDEN !!", textRed));
                return lines;
            }

            EquipSlots slot = GetPreferredSlot(hoveredItem);
            DaggerfallUnityItem equippedItem = player.ItemEquipTable.GetItem(slot);

            if (hoveredItem.ItemGroup == ItemGroups.Weapons)
            {
                BuildWeaponLines(player, hoveredItem, equippedItem, lines);
            }
            else if (hoveredItem.ItemGroup == ItemGroups.Armor)
            {
                BuildArmorLines(hoveredItem, equippedItem, lines);
            }
            else if (IsClothingLike(hoveredItem))
            {
                BuildClothingSubstitutionLines(hoveredItem, equippedItem, lines);
            }

            return lines;
        }

        private void BuildWeaponLines(PlayerEntity player, DaggerfallUnityItem hoveredItem, DaggerfallUnityItem equippedItem, List<ImpactLine> lines)
        {
            int newSkill = GetWeaponAccuracyScore(player, hoveredItem);
            int oldSkill = GetWeaponAccuracyScore(player, equippedItem);
            int netAccuracy = newSkill - oldSkill;

            int newMinDamage;
            int newMaxDamage;
            int oldMinDamage;
            int oldMaxDamage;
            GetDamageRange(hoveredItem, out newMinDamage, out newMaxDamage);
            GetDamageRange(equippedItem, out oldMinDamage, out oldMaxDamage);

            int netMinDamage = newMinDamage - oldMinDamage;
            int netMaxDamage = newMaxDamage - oldMaxDamage;

            lines.Add(new ImpactLine(
                string.Format("Net Accuracy: {0}{1}%", netAccuracy > 0 ? "+" : string.Empty, netAccuracy),
                SelectDeltaColor(netAccuracy)));

            lines.Add(new ImpactLine(
                string.Format("Net Damage: {0}{1} to {2}{3}",
                    netMinDamage > 0 ? "+" : string.Empty,
                    netMinDamage,
                    netMaxDamage > 0 ? "+" : string.Empty,
                    netMaxDamage),
                SelectDeltaColor(netMinDamage + netMaxDamage)));
        }

        private void BuildArmorLines(DaggerfallUnityItem hoveredItem, DaggerfallUnityItem equippedItem, List<ImpactLine> lines)
        {
            int newArmorValue = GetArmorRating(hoveredItem);
            int oldArmorValue = GetArmorRating(equippedItem);
            int protectionDelta = oldArmorValue - newArmorValue;

            float newWeight = GetWeightKg(hoveredItem);
            float oldWeight = GetWeightKg(equippedItem);
            float weightDelta = newWeight - oldWeight;

            lines.Add(new ImpactLine(
                string.Format("Net Protection: {0}{1} AR", protectionDelta > 0 ? "+" : string.Empty, protectionDelta),
                SelectDeltaColor(protectionDelta)));

            lines.Add(new ImpactLine(
                string.Format("Weight Change: {0}{1:0.0} kg", weightDelta > 0 ? "+" : string.Empty, weightDelta),
                SelectWeightColor(weightDelta)));
        }

        private void BuildClothingSubstitutionLines(DaggerfallUnityItem hoveredItem, DaggerfallUnityItem equippedItem, List<ImpactLine> lines)
        {
            bool hoveredEnchanted = IsEnchanted(hoveredItem);
            bool equippedEnchanted = IsEnchanted(equippedItem);

            if (!hoveredEnchanted && equippedEnchanted)
            {
                string lostEffect = GetPrimaryEnchantmentDescription(equippedItem);
                if (string.IsNullOrEmpty(lostEffect))
                    lostEffect = "Enchantment";

                lines.Add(new ImpactLine(string.Format("Effect Lost: {0}", lostEffect), textRed));
                return;
            }

            if (hoveredEnchanted && !equippedEnchanted)
            {
                lines.Add(new ImpactLine("Enchantment Gain: yes", textGreen));
                return;
            }

            lines.Add(new ImpactLine("No direct stat shift detected.", textWhite));
        }

        private bool IsClothingLike(DaggerfallUnityItem item)
        {
            if (item == null)
                return false;

            return item.ItemGroup == ItemGroups.MensClothing || item.ItemGroup == ItemGroups.WomensClothing;
        }

        private bool IsForbidden(PlayerEntity player, DaggerfallUnityItem item)
        {
            if (player == null || item == null)
                return false;

            if (item.ItemGroup == ItemGroups.Weapons)
            {
                Weapon weapon = item as Weapon;
                if (weapon != null)
                {
                    bool forbidden = InvokePlayerRestriction(player, "IsWeaponForbidden", new object[] { weapon.GetWeaponType() });
                    if (forbidden)
                        return true;
                }
            }

            if (item.ItemGroup == ItemGroups.Armor)
            {
                int material = GetNativeMaterialValue(item);
                bool forbiddenByInt = InvokePlayerRestriction(player, "IsMaterialForbidden", new object[] { material });
                if (forbiddenByInt)
                    return true;

                ArmorMaterialTypes armorMat = ReadArmorMaterial(item);
                bool forbiddenByEnum = InvokePlayerRestriction(player, "IsArmorMaterialForbidden", new object[] { armorMat });
                if (forbiddenByEnum)
                    return true;
            }

            return false;
        }

        private bool InvokePlayerRestriction(PlayerEntity player, string methodName, object[] args)
        {
            if (player == null)
                return false;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo[] methods = player.GetType().GetMethods(flags);

            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != methodName)
                    continue;

                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                    continue;

                try
                {
                    object result = method.Invoke(player, args);
                    if (result is bool)
                        return (bool)result;
                }
                catch
                {
                }
            }

            return false;
        }

        private int GetWeaponAccuracyScore(PlayerEntity player, DaggerfallUnityItem weaponItem)
        {
            if (player == null || weaponItem == null)
                return 0;

            DFCareer.Skills skill = GetWeaponSkill(weaponItem);
            int skillValue = player.Skills.GetLiveSkillValue(skill);
            int materialBonus = GetMaterialAccuracyBonus(weaponItem);
            return skillValue + materialBonus;
        }

        private DFCareer.Skills GetWeaponSkill(DaggerfallUnityItem item)
        {
            Weapon weapon = item as Weapon;
            if (weapon == null)
                return DFCareer.Skills.ShortBlade;

            WeaponTypes weaponType = weapon.GetWeaponType();

            if (weaponType == WeaponTypes.Dagger || weaponType == WeaponTypes.Tanto || weaponType == WeaponTypes.Wakazashi || weaponType == WeaponTypes.Shortsword)
                return DFCareer.Skills.ShortBlade;

            if (weaponType == WeaponTypes.Saber || weaponType == WeaponTypes.Broadsword || weaponType == WeaponTypes.Longsword || weaponType == WeaponTypes.Katana || weaponType == WeaponTypes.Dai_Katana)
                return DFCareer.Skills.LongBlade;

            if (weaponType == WeaponTypes.BattleAxe || weaponType == WeaponTypes.WarAxe)
                return DFCareer.Skills.Axe;

            if (weaponType == WeaponTypes.Mace || weaponType == WeaponTypes.Flail || weaponType == WeaponTypes.Warhammer || weaponType == WeaponTypes.Staff)
                return DFCareer.Skills.BluntWeapon;

            if (weaponType == WeaponTypes.Short_Bow || weaponType == WeaponTypes.Long_Bow)
                return DFCareer.Skills.Archery;

            return DFCareer.Skills.HandToHand;
        }

        private int GetMaterialAccuracyBonus(DaggerfallUnityItem item)
        {
            int nativeValue = GetNativeMaterialValue(item);

            if (nativeValue <= 0)
                return 0;

            return nativeValue;
        }

        private int GetNativeMaterialValue(DaggerfallUnityItem item)
        {
            if (item == null)
                return 0;

            try
            {
                return item.NativeMaterialValue;
            }
            catch
            {
                return 0;
            }
        }

        private void GetDamageRange(DaggerfallUnityItem item, out int minDamage, out int maxDamage)
        {
            minDamage = 0;
            maxDamage = 0;

            if (item == null)
                return;

            Weapon weapon = item as Weapon;
            if (weapon != null)
            {
                minDamage = weapon.GetBaseDamageMin();
                maxDamage = weapon.GetBaseDamageMax();
                return;
            }

            object minObj = TryReadMember(item, "baseDamageMin");
            object maxObj = TryReadMember(item, "baseDamageMax");

            if (minObj is int)
                minDamage = (int)minObj;

            if (maxObj is int)
                maxDamage = (int)maxObj;
        }

        private int GetArmorRating(DaggerfallUnityItem item)
        {
            if (item == null)
                return 0;

            object ar = TryReadMember(item, "currentArmorValue");
            if (ar is int)
                return (int)ar;

            ar = TryReadMember(item, "armorValue");
            if (ar is int)
                return (int)ar;

            Armor armor = item as Armor;
            if (armor != null)
                return armor.Protection;

            return 0;
        }

        private float GetWeightKg(DaggerfallUnityItem item)
        {
            if (item == null)
                return 0f;

            object weight = TryReadMember(item, "weightInKg");
            if (weight is float)
                return (float)weight;

            if (weight is double)
                return (float)(double)weight;

            if (weight is int)
                return (int)weight;

            object value = TryInvokeMember(item, "GetWeightInKg", Type.EmptyTypes, new object[0]);
            if (value is float)
                return (float)value;

            if (value is double)
                return (float)(double)value;

            return 0f;
        }

        private bool IsEnchanted(DaggerfallUnityItem item)
        {
            if (item == null)
                return false;

            object value = TryReadMember(item, "IsEnchanted");
            if (value is bool)
                return (bool)value;

            value = TryReadMember(item, "isEnchanted");
            if (value is bool)
                return (bool)value;

            return false;
        }

        private string GetPrimaryEnchantmentDescription(DaggerfallUnityItem item)
        {
            if (item == null)
                return string.Empty;

            object effect = TryInvokeMember(item, "GetEnchantmentSummary", Type.EmptyTypes, new object[0]);
            if (effect is string)
                return (string)effect;

            effect = TryReadMember(item, "longName");
            if (effect is string)
                return (string)effect;

            return string.Empty;
        }

        private ArmorMaterialTypes ReadArmorMaterial(DaggerfallUnityItem item)
        {
            if (item == null)
                return ArmorMaterialTypes.None;

            object value = TryReadMember(item, "NativeMaterialValue");
            if (value is int)
            {
                int raw = (int)value;
                if (Enum.IsDefined(typeof(ArmorMaterialTypes), raw))
                    return (ArmorMaterialTypes)raw;
            }

            return ArmorMaterialTypes.None;
        }

        private object TryReadMember(object target, string memberName)
        {
            if (target == null)
                return null;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();

            PropertyInfo property = type.GetProperty(memberName, flags);
            if (property != null)
                return property.GetValue(target, null);

            FieldInfo field = type.GetField(memberName, flags);
            if (field != null)
                return field.GetValue(target);

            return null;
        }

        private object TryInvokeMember(object target, string methodName, Type[] paramTypes, object[] args)
        {
            if (target == null)
                return null;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo method = target.GetType().GetMethod(methodName, flags, null, paramTypes, null);
            if (method == null)
                return null;

            return method.Invoke(target, args);
        }

        private EquipSlots GetPreferredSlot(DaggerfallUnityItem item)
        {
            if (item == null)
                return EquipSlots.None;

            object slotObject = TryReadMember(item, "EquipSlot");
            if (slotObject is EquipSlots)
                return (EquipSlots)slotObject;

            slotObject = TryInvokeMember(item, "GetEquipSlot", Type.EmptyTypes, new object[0]);
            if (slotObject is EquipSlots)
                return (EquipSlots)slotObject;

            if (item.ItemGroup == ItemGroups.Weapons)
                return EquipSlots.RightHand;

            return EquipSlots.None;
        }

        private Color SelectDeltaColor(int delta)
        {
            if (delta > 0)
                return textGreen;
            if (delta < 0)
                return textRed;
            return textWhite;
        }

        private Color SelectWeightColor(float delta)
        {
            if (Math.Abs(delta) < 0.01f)
                return textWhite;

            if (delta < 0f)
                return textGreen;

            return textRed;
        }

        private void CreateTooltipPanel(DaggerfallInventoryWindow inventoryWindow, List<ImpactLine> lines)
        {
            Panel parent = inventoryWindow.NativePanel;
            if (parent == null)
                return;

            int width = 174;
            int height = 24 + (lines.Count * 12) + 4;

            tooltipPanel = DaggerfallUI.AddPanel(new Rect(0, 0, width, height), parent);
            tooltipPanel.BackgroundColor = panelBackground;
            tooltipPanel.Outline.Enabled = true;
            tooltipPanel.Outline.Color = new Color(0.8f, 0.8f, 0.8f, 1f);

            headerLabel = DaggerfallUI.AddTextLabel(DaggerfallUI.DefaultFont, new Vector2(4, 3), HeaderText, tooltipPanel);
            headerLabel.TextColor = textWhite;

            for (int i = 0; i < lines.Count; i++)
            {
                ImpactLine line = lines[i];
                TextLabel label = DaggerfallUI.AddTextLabel(
                    DaggerfallUI.DefaultFont,
                    new Vector2(4, 15 + (i * 10)),
                    line.Text,
                    tooltipPanel);

                label.TextColor = line.Color;
                label.ShadowPosition = Vector2.zero;
                lineLabels.Add(label);
            }

            UpdatePanelPosition(inventoryWindow);
        }

        private void UpdatePanelPosition(DaggerfallInventoryWindow inventoryWindow)
        {
            if (inventoryWindow == null || tooltipPanel == null)
                return;

            Vector2 mousePosition = DaggerfallUI.MousePosition;

            float x = mousePosition.x + 90f;
            float y = mousePosition.y - 8f;

            float maxX = inventoryWindow.NativePanel.InteriorWidth - tooltipPanel.Size.x;
            float maxY = inventoryWindow.NativePanel.InteriorHeight - tooltipPanel.Size.y;

            if (x > maxX)
                x = maxX;
            if (x < 0f)
                x = 0f;

            if (y > maxY)
                y = maxY;
            if (y < 0f)
                y = 0f;

            tooltipPanel.Position = new Vector2(x, y);
        }

        private void RemoveTooltipPanel()
        {
            if (tooltipPanel == null)
                return;

            if (headerLabel != null)
            {
                tooltipPanel.Components.Remove(headerLabel);
                headerLabel = null;
            }

            for (int i = 0; i < lineLabels.Count; i++)
            {
                TextLabel label = lineLabels[i];
                if (label != null)
                    tooltipPanel.Components.Remove(label);
            }

            lineLabels.Clear();

            if (tooltipPanel.Parent != null)
                tooltipPanel.Parent.Components.Remove(tooltipPanel);

            tooltipPanel = null;
        }

        private struct ImpactLine
        {
            public string Text;
            public Color Color;

            public ImpactLine(string text, Color color)
            {
                Text = text;
                Color = color;
            }
        }
    }
}
