using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeyPressTester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            while(true)
            {
                var key = Console.ReadKey(true);

                bool IsControl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
                bool IsShift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
                bool IsAlt = key.Modifiers.HasFlag(ConsoleModifiers.Alt);

                char modifier = '1';
                if (IsShift) modifier = (char)(modifier + 1);
                if (IsAlt) modifier = (char)(modifier + 2);
                if (IsControl) modifier = (char)(modifier + 4);

                Console.Write(    $"KeyPressTester: Modifier={modifier} Key={key.Key.ToString()} keyChar={key.KeyChar} => ");

                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    Console.Write($"Control + ");
                if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
                    Console.Write($"Alt + ");
                if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                    Console.Write($"Shift + ");

                Console.WriteLine(key.Key.ToString());

            }
        }
    }
}
