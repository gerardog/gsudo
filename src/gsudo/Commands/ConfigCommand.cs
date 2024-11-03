using gsudo.AppSettings;
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

            if (key.In("-h", "/h", "/?", "-?", "--help", "help"))
            {
                return new HelpCommand().Execute();
            }

            if (key != null && key.Contains('=', StringComparison.Ordinal))
            {
                var list = new List<string>{ key.Substring(key.IndexOf("=", StringComparison.Ordinal) + 1) };
                list.AddRange(value);
                value = list; // in net7 => value.Prepend(key.Substring(key.IndexOf("=", StringComparison.Ordinal) + 1))
                key = key.Substring(0, key.IndexOf("=", StringComparison.Ordinal));
            }

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
                throw new ApplicationException($"Invalid Setting '{key}'.");

            if (value != null && value.Any()) // Write Setting
            {
                /*if (value.Any(v => v.In("--global")))
                {
                    InputArguments.Global = true;
                    value = value.Where(v => !v.In("--global"));
                }*/

                if (value.FirstOrDefault() == "=")
                    value = value.Skip(1);

                bool reset = value.Any(v => v.In("--reset"));
                value = value.Where(v => !v.In("--reset"));

                string unescapedValue =
                    (value.Count() == 1)
                    ? ArgumentsHelper.UnQuote(value.FirstOrDefault()).ReplaceOrdinal("\\%", "%")
                    : string.Join(" ", value.ToArray());

                if (!reset) _ = setting.Parse(unescapedValue);

                if (!InputArguments.Global && setting.Scope == RegistrySettingScope.GlobalOnly)
                {
                    Logger.Instance.Log($"Config Setting for '{setting.Name}' will be set as global system setting.", LogLevel.Info);
                    // InputArguments.Global = true;
                }

                if (InputArguments.Global && !SecurityHelper.IsAdministrator())
                {
                    Logger.Instance.Log($"Global system settings requires elevation. Elevating...", LogLevel.Info);
                    InputArguments.Direct = true;
                    return new RunCommand(commandToRun: new string[]
                            { $"\"{ProcessHelper.GetOwnExeName()}\"", "--global", "config", key, reset ? "--reset" : $"\"{unescapedValue}\""}
                    ).Execute();
                }

                if (reset)
                    setting.Reset(InputArguments.Global); // reset to default value
                else 
                    setting.Save(unescapedValue, InputArguments.Global);

                if (setting.Name == Settings.CacheMode.Name && unescapedValue.In(CredentialsCache.CacheMode.Disabled.ToString()))
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
