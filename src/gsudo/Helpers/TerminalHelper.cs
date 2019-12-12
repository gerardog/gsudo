using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Helpers
{
    class TerminalHelper
    {
        public static bool TerminalHasBuiltInVTSupport()
        {
            return (IsWindowsTerminal()
                || IsConEmu())
                && !Console.IsOutputRedirected;                
        }

        public static bool IsConEmu() // or Cmder
        {
            return (Environment.GetEnvironmentVariable("ConEmuANSI") ?? string.Empty)
                .Equals("ON", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWindowsTerminal()
        {
            // the new Windows Terminal, not to be confused with old ConHost.
            // https://github.com/microsoft/terminal/issues/1040#issuecomment-496691842
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));
        }

        public static byte[] GetSequenceFromConsoleKey(ConsoleKeyInfo key, bool debug = false)
        {
            var res = new List<char>(15);
            const char ESC = '\x1B';

            Func<List<char>> PrependAltIfPressed = () =>
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
                    res.Add(ESC);
                return res;
            };

            Action<char, char, char, char> AddWithCtrl2 = (b1, b2, b3, b4) =>
            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    res.AddMany(b1, b2, b3, ';', '5', b4);
                else
                    res.AddMany(b1, b2, b3, b4);
            };

            bool IsControl = key.Modifiers.HasFlag(ConsoleModifiers.Control);
            bool IsShift = key.Modifiers.HasFlag(ConsoleModifiers.Shift);
            bool IsAlt = key.Modifiers.HasFlag(ConsoleModifiers.Alt);


            // http://www.xfree86.org/4.7.0/ctlseqs.html
            char modifier = '1';
            if (IsShift) modifier = (char)(modifier + 1);
            if (IsAlt) modifier = (char)(modifier + 2);
            if (IsControl) modifier = (char)(modifier + 4);

            if (debug) // hard code debug mode
            {
                // Test mode:
                // run from cmder: gsudo --debug KeyPressTester.exe
                // you may also compare with Windows Terminal and report any issue as a Windows Terminal Bug.

                Console.Write($"gsudo received. Modifier={modifier} Key={key.Key.ToString()} keyChar={key.KeyChar} => ");
                if (IsControl) Console.Write($"Control + ");
                if (IsAlt) Console.Write($"Alt + ");
                if (IsShift) Console.Write($"Shift + ");

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(key.Key.ToString());
                Console.ResetColor();
            }

            bool applicationMode = true;

            // DECCKM	Disable Cursor Keys Application Mode (use Normal Mode) 
            //res.AddMany(ESC, '[', '?', '1', 'h');
            //res.AddMany(ESC, '[', '2', 'h');
            //applicationMode = false;

            //if (key.Key == ConsoleKey.Spacebar && IsControl)
            //    res.Add((char)0);
            //else 

            if (key.Key == ConsoleKey.UpArrow)
                res.AddMany(ESC, '[', '1', ';', modifier, 'A');
            else if (key.Key == ConsoleKey.DownArrow)
                res.AddMany(ESC, '[', '1', ';', modifier, 'B');
            else if (key.Key == ConsoleKey.RightArrow)
                res.AddMany(ESC, '[', '1', ';', modifier, 'C');
            else if (key.Key == ConsoleKey.LeftArrow)
                res.AddMany(ESC, '[', '1', ';', modifier, 'D');
            else if (key.Key == ConsoleKey.Home)
                res.AddMany(ESC, '[', '1', ';', modifier, 'H');
            else if (key.Key == ConsoleKey.End)
                res.AddMany(ESC, '[', '1', ';', modifier, 'F');
            else if (key.Key == ConsoleKey.PageUp)
                res.AddMany(ESC, '[', '5', ';', modifier, '~');
            else if (key.Key == ConsoleKey.PageDown)
                res.AddMany(ESC, '[', '6', ';', modifier, '~');
            else if (key.Key == ConsoleKey.Delete)
                res.AddMany(ESC, '[', '3', ';', modifier, '~');
            else if (key.Key == ConsoleKey.Insert)
                res.AddMany(ESC, '[', '2', ';', modifier, '~');

            else if (key.Key == ConsoleKey.F1)
                if (applicationMode)
                    res.AddMany(ESC, 'O', 'P');
                else
                    res.AddMany(ESC, '[', '1', '1', ';', modifier, '~');

            else if (key.Key == ConsoleKey.F2)
                if (applicationMode)
                    res.AddMany(ESC, 'O', 'Q');
                else
                    res.AddMany(ESC, '[', '1', '2', ';', modifier, '~');

            else if (key.Key == ConsoleKey.F3)
                if (applicationMode)
                    res.AddMany(ESC, 'O', 'R');
                else
                    res.AddMany(ESC, '[', '1', '3', ';', modifier, '~');

            else if (key.Key == ConsoleKey.F4)
                if (applicationMode)
                    PrependAltIfPressed().AddMany(ESC, 'O', 'S');
                else
                    res.AddMany(ESC, '[', '1', '4', ';', modifier, '~');

            else if (key.Key == ConsoleKey.F5)
                res.AddMany(ESC, '[', '1', '5', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F6)
                res.AddMany(ESC, '[', '1', '7', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F7)
                res.AddMany(ESC, '[', '1', '8', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F8)
                res.AddMany(ESC, '[', '1', '9', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F9)
                res.AddMany(ESC, '[', '2', '0', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F10)
                res.AddMany(ESC, '[', '2', '1', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F11)
                res.AddMany(ESC, '[', '2', '3', ';', modifier, '~');
            else if (key.Key == ConsoleKey.F12)
                res.AddMany(ESC, '[', '2', '4', ';', modifier, '~');
            else

            {
                if (key.Modifiers.HasFlag(ConsoleModifiers.Alt))
                    res.Add(ESC);

                if (IsControl && ConsoleKey.A <= key.Key && key.Key <= ConsoleKey.Z)
                    res.Add((char)(key.Key - ConsoleKey.A + 1));
                else
                    res.Add(key.KeyChar);
            }

            return res.Select(c => (byte)c).ToArray();
        }

    }
}
