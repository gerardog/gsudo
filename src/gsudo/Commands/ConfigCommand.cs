using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class ConfigCommand : ICommand
    {
        public string key { get; set; }

        public IEnumerable<string> value { get; set; }

        public Task<int> Execute()
        {
            RegistrySetting setting = null;

            if (key == null)
            {
                // print all configs
                foreach (var k in Settings.AllKeys)
                {                    
                    var scope = k.Value.HasGlobalValue() ? "(global)" : 
                                    (k.Value.HasLocalValue() ? "(user)" : "(default)");
                    Console.WriteLine($"{k.Value.Name} = \"{ k.Value.GetStringValue().ToString()}\" ".PadRight(50) + scope);
                }

                return Task.FromResult(0);
            }

            Settings.AllKeys.TryGetValue(key, out setting);

            if (setting == null)
            {
                Logger.Instance.Log($"Invalid Setting '{key}'.", LogLevel.Error);
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }

            if (value != null && value.Any()) // Write Setting
            {
                if (value.Any(v => v.In("--global")))
                {
                    InputArguments.Global = true;
                    value = value.Where(v => !v.In("--global"));
                }

                bool reset = value.Any(v => v.In("--reset"));
                value = value.Where(v => !v.In("--reset"));

                string unescapedValue =
                    (value.Count() == 1)
                    ? ArgumentsHelper.UnQuote(value.FirstOrDefault()).Replace("\\%", "%")
                    : string.Join(" ", value.ToArray());

                if (!reset) _ = setting.Parse(unescapedValue);

                if (!InputArguments.Global && setting.Scope == RegistrySettingScope.GlobalOnly)
                {
                    Logger.Instance.Log($"Config Setting for '{setting.Name}' will be set as global system setting.", LogLevel.Info);
                    InputArguments.Global = true;
                }

                if (InputArguments.Global && !ProcessHelper.IsAdministrator())
                {
                    Logger.Instance.Log($"Global system settings requires elevation. Elevating...", LogLevel.Info);
                    InputArguments.Direct = true;
                    return new RunCommand()
                    {
                        CommandToRun = new string[]
                            { $"\"{ProcessHelper.GetOwnExeName()}\"", "--global", "config", key, reset ? "--reset" : $"\"{unescapedValue}\""}
                    }.Execute();
                }

                if (reset)
                    setting.Reset(InputArguments.Global); // reset to default value
                else 
                    setting.Save(unescapedValue, InputArguments.Global);

                if (setting.Name == Settings.CacheMode.Name && unescapedValue.In(Enums.CacheMode.Disabled.ToString()))
                    new KillCacheCommand().Execute();
                if (setting.Name.In (Settings.CacheDuration.Name, Settings.SecurityEnforceUacIsolation.Name))
                    new KillCacheCommand().Execute();
            }

            // READ
            setting.ClearRunningValue();
            Console.WriteLine($"{setting.Name} = \"{ setting.GetStringValue().ToString()}\"");
            return Task.FromResult(0);
        }
    }
}
