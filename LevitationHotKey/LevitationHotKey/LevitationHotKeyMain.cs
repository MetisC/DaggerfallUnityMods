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
        static KeyCode toggleKey = KeyCode.V;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            GameObject go = new GameObject(mod.Title);
            instance = go.AddComponent<LevitationHotKeyMain>();
            DontDestroyOnLoad(go); // Persistencia entre escenas
        }

        private void Awake()
        {
            mod.LoadSettingsCallback = LoadSettings;
            mod.LoadSettings();
            mod.IsReady = true;
        }

        private void LoadSettings(ModSettings settings, ModSettingsChange change)
        {
            // Lectura de la tecla desde el archivo modsettings.json
            toggleKey = settings.GetValue<KeyCode>("General", "HotKey");
        }

        private void Update()
        {
            // Intercepta la pulsación si el juego no está en pausa
            if (InputManager.Instance.GetKeyDown(toggleKey) && !GameManager.Instance.IsPlayerPaused)
            {
                ToggleLevitation();
            }
        }

        private void ToggleLevitation()
        {
            EntityEffectManager playerEffectManager = GameManager.Instance.PlayerEntityBehaviour.GetComponent<EntityEffectManager>();
            if (playerEffectManager == null) return;

            // Busca el efecto nativo de levitación en el gestor de efectos
            LiveEffectBundle levitateBundle = playerEffectManager.FindBundle(new Levitate().Key);

            if (levitateBundle != null)
            {
                playerEffectManager.RemoveBundle(levitateBundle); // Desactiva y aterriza
                DaggerfallUI.AddHUDText("Levitación DESACTIVADA.");
            }
            else
            {
                // Crea un nuevo bundle de efecto para el jugador
                EffectBundleSettings bundleSettings = new EffectBundleSettings();
                bundleSettings.TargetType = TargetTypes.CasterOnly;
                bundleSettings.Name = "Levitación de Emergencia";

                EffectEntry entry = new EffectEntry();
                entry.Key = new Levitate().Key;
                entry.Settings = new EffectSettings();
                entry.Settings.DurationBase = 60; // Duración: 60 rounds (~1 minuto)

                bundleSettings.Effects = new EffectEntry[] { entry };

                // Aplica el efecto ignorando resistencias (BypassResistances)
                playerEffectManager.AssignBundle(bundleSettings, AssignBundleFlags.BypassResistances);
                DaggerfallUI.AddHUDText("Levitación ACTIVADA (1 min).");
            }
        }
    }
}
