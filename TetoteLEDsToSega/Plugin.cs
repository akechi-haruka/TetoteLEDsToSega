using System;
using System.Collections;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Haruka.Arcade.SEGA835Lib.Debugging;
using Haruka.Arcade.SEGA835Lib.Devices;
using Haruka.Arcade.SEGA835Lib.Devices.LED._837_15093;
using Haruka.Arcade.SEGA835Lib.Misc;
using Lod.TypeX4;

namespace TetoteLEDsToSega;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public new static ManualLogSource Logger;
    public static LED_837_15093_06 Leds;

    private ConfigEntry<int> configLedPort;
    private ConfigEntry<bool> configSegaLibLog;

    private void Awake() {
        // Plugin startup logic
        Logger = base.Logger;

        configLedPort = Config.Bind("General", "LED COM Port", 9, "COM Port on which the LED board is connected to.");
        configSegaLibLog = Config.Bind("Debugging", "SegaLib Logging", false, "Enables Sega835Lib debug logs");

        if (!configSegaLibLog.Value) {
            Haruka.Arcade.SEGA835Lib.Debugging.Log.Mute = true;
        }

        Leds = new LED_837_15093_06(configLedPort.Value);
        DeviceStatus ret = Leds.Connect();
        if (ret != DeviceStatus.OK) {
            Logger.LogError("Failed to initialize LEDs! " + ret);
            Leds = null;
        }

        string bn = null, cn = null;
        CheckLedAction(Leds?.SetTimeout(3000));
        CheckLedAction(Leds?.GetBoardInfo(out bn, out cn, out byte _));
        if (bn != null && cn != null) {
            Logger.LogInfo("LED Board Number: " + bn);
            Logger.LogInfo("LED Chip Number: " + cn);
        }

        CheckLedAction(Leds?.SetResponseDisabled(true));

        Harmony.CreateAndPatchAll(typeof(Patches));

        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static void CheckLedAction(DeviceStatus? ret) {
        if (ret != null && ret != DeviceStatus.OK) {
            Logger.LogError("Failed to communicate with LED board! " + ret);
            Leds = null;
        }
    }

    private static Color[] prev_leds = new Color[18];
    private static void UpdateLeds(JVS_IO.Output.LEDAnalog rd, JVS_IO.Output.LEDAnalog rc, JVS_IO.Output.LEDAnalog ru, JVS_IO.Output.LEDAnalog lu, JVS_IO.Output.LEDAnalog lc, JVS_IO.Output.LEDAnalog ld) {
        if (Leds == null) {
            return;
        }

        Color[] leds = Enumerable
            .Repeat(Color.FromArgb(rd.Red, rd.Green, rd.Blue), 11) // right bottom
            .Concat(Enumerable.Repeat(Color.FromArgb(rc.Red, rc.Green, rc.Blue), 11)) // right center
            .Concat(Enumerable.Repeat(Color.FromArgb(ru.Red, ru.Green, ru.Blue), 11)) // right upper
            .Concat(Enumerable.Repeat(Color.FromArgb(lu.Red, lu.Green, lu.Blue), 11)) // left upper
            .Concat(Enumerable.Repeat(Color.FromArgb(lc.Red, lc.Green, lc.Blue), 11)) // left center
            .Concat(Enumerable.Repeat(Color.FromArgb(ld.Red, ld.Green, ld.Blue), 11)) // left bottom
            .ToArray();

        if (StructuralComparisons.StructuralEqualityComparer.Equals(prev_leds, leds)) {
            return;
        }

        DeviceStatus ret = Leds.SetLEDs(leds);
        if (ret != DeviceStatus.OK) {
            Logger.LogError("Failed to set LEDs! " + ret);
            Leds = null;
        }

        prev_leds = leds;
    }

    public class Patches {
        [HarmonyPostfix, HarmonyPatch(typeof(JVS_IO), "Update")]
        static void Update(JVS_IO __instance) {
            UpdateLeds(__instance.output.TapeLED_R_DOWN, __instance.output.TapeLED_R_CENTER, __instance.output.TapeLED_R_UP, __instance.output.TapeLED_L_UP, __instance.output.TapeLED_L_CENTER, __instance.output.TapeLED_L_DOWN);
        }

    }
}