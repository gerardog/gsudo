using System;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    public class HelpCommand : ICommand
    {
        public virtual Task<int> Execute()
        {
            ShowHelp();
            return Task.FromResult(0);
        }

        internal static void ShowHelp()
        {
            Console.WriteLine("UniGetUI Elevator - https://github.com/marticliment/GSudo-for-UniGetUI/");
        }
    }

    class ShowVersionHelpCommand : HelpCommand
    {
        public override Task<int> Execute()
        {
            return Task.FromResult(0);
        }

    }
}
