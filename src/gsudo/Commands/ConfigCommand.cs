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
                foreach (var k in GlobalSettings.AllKeys)
                    Console.WriteLine($"{k.Value.Name} = \"{ k.Value.GetStringValue().ToString()}\"");

                return Task.FromResult(0);
            }

            GlobalSettings.AllKeys.TryGetValue(key, out setting);

            if (setting == null)
            {
                Console.WriteLine($"Invalid Setting '{key}'.", LogLevel.Error);
                return Task.FromResult(Constants.GSUDO_ERROR_EXITCODE);
            }
            
            if (value!=null && value.Any())
            {
                if (value.FirstOrDefault().In("--reset"))
                    setting.Reset(); // reset to default value
                else if (value.Count()==1)
                    setting.Save(ArgumentsHelper.UnQuote(value.FirstOrDefault()).Replace("\\%","%"));
                else
                    setting.Save(string.Join(" ", value.ToArray()));
            }

            // READ
            Console.WriteLine($"{setting.Name} = \"{ setting.GetStringValue().ToString()}\"");
            return Task.FromResult(0);
        }
    }
}
