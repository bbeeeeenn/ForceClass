using Newtonsoft.Json;
using TShockAPI;

namespace ForceClass;

public class Config
{
    public bool Enabled = true;
    public bool SameAll = false;
    public string ClassAll = "warrior";
    public int PunishDuration = 5;

    public Config Load()
    {
        if (!File.Exists(TShockPlugin.ConfigPath))
        {
            File.WriteAllText(
                TShockPlugin.ConfigPath,
                JsonConvert.SerializeObject(this, Formatting.Indented)
            );
            return new Config();
        }

        Config? deserialized = JsonConvert.DeserializeObject<Config>(
            File.ReadAllText(TShockPlugin.ConfigPath)
        );

        if (deserialized == null)
        {
            TShock.Log.ConsoleError(
                "[ForceClass] Failed to load Config file\n[ForceClass] Now using the default settings"
            );
            return new Config();
        }

        return deserialized;
    }
}
