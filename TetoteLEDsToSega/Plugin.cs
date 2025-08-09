using System;
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

    private void Awake() {
        // Plugin startup logic
        Logger = base.Logger;

        configLedPort = Config.Bind("General", "LED COM Port", 9, "COM Port on which the LED board is connected to.");

        Log.LogMessageWritten += LogOnLogMessageWritten;
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

    private static void LogOnLogMessageWritten(LogEntry obj) {
        if (obj.Color == ConsoleColor.Yellow) {
            Logger.LogWarning("SegaLib: " + obj.Message);
        } else if (obj.Color == ConsoleColor.Red) {
            Logger.LogError("SegaLib: " + obj.Message);
        } else {
            Logger.LogInfo("SegaLib: " + obj.Message);
        }
    }

    private static void UpdateLeds(byte[] buf) {
        if (Leds == null) {
            return;
        }

        Color[] leds = Enumerable
            .Repeat(Color.FromArgb(buf[0], buf[1], buf[2]), 11) // left upper
            .Concat(Enumerable.Repeat(Color.FromArgb(buf[3], buf[4], buf[5]), 11)) // left center
            .Concat(Enumerable.Repeat(Color.FromArgb(buf[6], buf[7], buf[8]), 11)) // left bottom
            .Concat(Enumerable.Repeat(Color.FromArgb(buf[12], buf[13], buf[17]), 11)) // right upper
            .Concat(Enumerable.Repeat(Color.FromArgb(buf[18], buf[19], buf[20]), 11)) // right center
            .Concat(Enumerable.Repeat(Color.FromArgb(buf[21], buf[22], buf[23]), 11)) // right bottom
            .ToArray();

        DeviceStatus ret = Leds.SetLEDs(leds);
        if (ret != DeviceStatus.OK) {
            Logger.LogError("Failed to set LEDs! " + ret);
            Leds = null;
        }
    }

    public class Patches {
        [HarmonyPostfix, HarmonyPatch(typeof(JVS_IO), "Update")]
        static void Update(JVS_IO __instance) {
            Plugin.UpdateLeds(__instance.buf.OutPWM);
        }
    }
}