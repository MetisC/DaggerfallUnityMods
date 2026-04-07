using System;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;

namespace LevitationHotKey
{
    public class LevitationHotKeyMain : MonoBehaviour
    {
        static Mod mod;
        static LevitationHotKeyMain instance;
        static KeyCode toggleKey = KeyCode.X;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject(mod.Title);
            instance = go.AddComponent<LevitationHotKeyMain>();
            DontDestroyOnLoad(go); // Persist across scenes
        }

        private void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            mod.IsReady = true;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            // Read the configured hotkey as a string to prevent UI crashing
            string keyString = settings.GetValue<string>("General", "HotKey");
            try
            {
                // Parse the string into a valid Unity KeyCode
                toggleKey = (KeyCode)System.Enum.Parse(typeof(KeyCode), keyString, true);
            }
            catch
            {
                Debug.LogWarning("[LevitationHotKey] Invalid key specified in settings, defaulting to X.");
                toggleKey = KeyCode.X;
            }
        }

        private void Update()
        {
            // Check if key is pressed and game is not paused
            if (InputManager.Instance.GetKeyDown(toggleKey) && !GameManager.IsGamePaused)
            {
                ToggleLevitation();
            }
        }

        private void ToggleLevitation()
        {
            EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEntityBehaviour.GetComponent<EntityEffectManager>();
            if (playerEffectManager == null) return;

            // Find the active effect directly by its native type
            Levitate levitateEffect = playerEffectManager.FindIncumbentEffect<Levitate>() as Levitate;

            // If the effect exists and has a parent bundle, remove it
            if (levitateEffect != null && levitateEffect.ParentBundle != null)
            {
                playerEffectManager.RemoveBundle(levitateEffect.ParentBundle);
                DaggerfallUI.AddHUDText("Levitation DISABLED.");
            }
            else
            {
                // If it doesn't exist, setup the 60-round effect
                EffectBundleSettings bundleSettings = new EffectBundleSettings();
                bundleSettings.TargetType = TargetTypes.CasterOnly;
                bundleSettings.Name = "Emergency Levitation";

                EffectEntry entry = new EffectEntry();
                entry.Key = new Levitate().Key;
                entry.Settings = new EffectSettings();
                entry.Settings.DurationBase = 900; 

                // --- EL PARCHE ANTI PANTALLAZO AZUL ---
                // Le damos valor a esta variable para que el motor no divida por cero
                entry.Settings.DurationPerLevel = 1;

                bundleSettings.Effects = new EffectEntry[] { entry };

                // Pack it into a valid EntityEffectBundle
                EntityEffectBundle finalBundle = new EntityEffectBundle(bundleSettings, GameManager.Instance.PlayerEntityBehaviour);

                // Apply it bypassing saving throws
                playerEffectManager.AssignBundle(finalBundle, AssignBundleFlags.BypassSavingThrows);
                DaggerfallUI.AddHUDText("Levitation ENABLED");
            }
        }
    }
}