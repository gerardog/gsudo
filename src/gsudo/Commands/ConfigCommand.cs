using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    [Verb("config")]
    class ConfigCommand : ICommand
    {
        [Value(0)]
        public string key { get; set; }

        [Value(1)]
        public string value { get; set; }

        IDictionary<string, RegistrySetting> AllKeys => new Dictionary<string, RegistrySetting>(StringComparer.OrdinalIgnoreCase)
        {
            [GlobalSettings.CredentialsCacheDuration.Name] = GlobalSettings.CredentialsCacheDuration,
            [GlobalSettings.LogLevel.Name] = GlobalSettings.LogLevel,
            [GlobalSettings.RootPrompt.Name] = GlobalSettings.RootPrompt,
            [GlobalSettings.ForceRawConsole.Name] = GlobalSettings.ForceRawConsole,
            [GlobalSettings.ForceVTConsole.Name] = GlobalSettings.ForceVTConsole, 
        };

        public Task<int> Execute()
        {
            RegistrySetting setting = null;

            if (key == null)
            {
                foreach ( var k in AllKeys)
                    Console.WriteLine($"{k.Value.Name} = { Newtonsoft.Json.JsonConvert.SerializeObject(k.Value.GetStringValue()).ToString()}");

                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }

            //            key = key.ToUpperInvariant();

            AllKeys.TryGetValue(key, out setting);

            if (setting == null)
            {
                Console.WriteLine($"Invalid Setting '{key}'.", LogLevel.Error);
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }
            
            if (!string.IsNullOrEmpty(value))
            {
                // SAVE 
                setting.Save($"\"{value}\"");
            }

            // READ
            Console.WriteLine($"{setting.Name} = { Newtonsoft.Json.JsonConvert.SerializeObject(setting.GetStringValue()).ToString()}");
            return Task.FromResult(0);
        }
    }
}
