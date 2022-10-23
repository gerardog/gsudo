using gsudo.Commands;
using gsudo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace gsudo.Tests
{
    [TestClass]
    public class CommandLineParserTests
    {
        [TestMethod]
        public void CmdLine_Flags1()
        {
            Action<ICommand> validate = (ICommand result) =>
            {
                var runCmd = result as RunCommand;
                Assert.IsNotNull(runCmd);
                Assert.AreEqual("\"C:\\Windows\\system32\\cmd.EXE\" /c echo 1 2 3 4", string.Join(" ", runCmd.UserCommand));

                Assert.IsTrue(InputArguments.NewWindow);
                Assert.IsTrue(InputArguments.Wait);
                Assert.IsTrue(InputArguments.Direct);
                Assert.IsTrue(InputArguments.KillCache);
                Assert.IsFalse(InputArguments.TrustedInstaller);
                Assert.IsFalse(InputArguments.RunAsSystem);
                Assert.AreEqual(InputArguments.IntegrityLevel, null);
                Assert.AreEqual(InputArguments.GetIntegrityLevel(), IntegrityLevel.High);
            };

            validate(new CommandLineParser("-n -w -d -k cmd /c echo 1 2 3 4").Parse());
            validate(new CommandLineParser("-nw -d -k cmd /c echo 1 2 3 4").Parse());
            validate(new CommandLineParser("-nw -dk cmd /c echo 1 2 3 4").Parse());
            validate(new CommandLineParser("-nwdk cmd /c echo 1 2 3 4").Parse());
            validate(new CommandLineParser("-NWDK cmd /c echo 1 2 3 4").Parse());
            validate(new CommandLineParser("-KDWN cmd /c echo 1 2 3 4").Parse());
        }

        [TestMethod]
        public void CmdLine_Flags_System()
        {
            var p = new CommandLineParser(ArgumentsHelper.SplitArgs("-n -ks cmd /c echo 1 2 3 4"));
            var ret = p.Parse();

            var runCmd = ret as RunCommand;
            Assert.IsNotNull(runCmd);
            Assert.AreEqual("\"C:\\Windows\\system32\\cmd.EXE\" /c echo 1 2 3 4", string.Join(" ", runCmd.UserCommand));

            Assert.IsTrue(InputArguments.NewWindow);
            Assert.IsFalse(InputArguments.Wait);
            Assert.IsFalse(InputArguments.Direct);
            Assert.IsTrue(InputArguments.KillCache);
            Assert.IsTrue(InputArguments.RunAsSystem);
            Assert.IsFalse(InputArguments.TrustedInstaller);
            Assert.AreEqual(InputArguments.IntegrityLevel, null);
            Assert.AreEqual(InputArguments.GetIntegrityLevel(), IntegrityLevel.System);

        }

        [TestMethod]
        public void CmdLine_TrustedInstaller()
        {
            var p = new CommandLineParser(ArgumentsHelper.SplitArgs("--ti"));
            var ret = p.Parse();

            var runCmd = ret as RunCommand;
            Assert.IsNotNull(runCmd);
            Assert.AreEqual("", string.Join(" ", runCmd.UserCommand));

            Assert.IsFalse(InputArguments.NewWindow);
            Assert.IsFalse(InputArguments.Wait);
            Assert.IsFalse(InputArguments.Direct);
            Assert.IsFalse(InputArguments.KillCache);
            Assert.IsTrue(InputArguments.RunAsSystem);
            Assert.IsTrue(InputArguments.TrustedInstaller);
            Assert.AreEqual(InputArguments.IntegrityLevel, null);
            Assert.AreEqual(InputArguments.GetIntegrityLevel(), IntegrityLevel.System);
        }

        [TestMethod]
        public void CmdLine_OptionWithArguments()
        {
            Action<ICommand> validate = (ICommand result) =>
            {
                var runCmd = result as RunCommand;
                Assert.IsNotNull(runCmd);
                CollectionAssert.AreEqual(new string[] { "\"C:\\windows\\system32\\notepad.exe\"", "\"1 2 3 4\"" }, runCmd.UserCommand.ToArray(), StringComparer.OrdinalIgnoreCase);

                Assert.IsTrue(InputArguments.NewWindow);
                Assert.IsTrue(InputArguments.Wait);
                Assert.IsFalse(InputArguments.Direct);
                Assert.IsFalse(InputArguments.KillCache);
                Assert.IsFalse(InputArguments.TrustedInstaller);
                Assert.IsFalse(InputArguments.RunAsSystem);
                Assert.AreEqual(InputArguments.IntegrityLevel, IntegrityLevel.MediumPlus);
                Assert.AreEqual(InputArguments.GetIntegrityLevel(), IntegrityLevel.MediumPlus);
            };

            validate(new CommandLineParser("-i MediumPlus --new --wait notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-i MediumPlus -n --wait notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-i MediumPlus --new -w notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-i MediumPlus -n -w notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-i MediumPlus -nw notepad \"1 2 3 4\"").Parse());

            validate(new CommandLineParser("-nw -i=MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw -i MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw -iMediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-n -w -i=MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-n -w -i MediumPlus notepad \"1 2 3 4\"").Parse());                    
            validate(new CommandLineParser("-n -w -iMediumPlus notepad \"1 2 3 4\"").Parse());

            validate(new CommandLineParser("-nw -I=MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw -I MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw -IMediumPlus notepad \"1 2 3 4\"").Parse()); 

            validate(new CommandLineParser("--integrity MediumPlus --new -w notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("--integrity=MediumPlus --new -w notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("--integrity MediumPlus -nw notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("--integrity=MediumPlus -nw notepad \"1 2 3 4\"").Parse());

            validate(new CommandLineParser("--new --wait --integrity MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("--new --wait --integrity=MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw      --integrity   MediumPlus notepad \"1 2 3 4\"").Parse());
            validate(new CommandLineParser("-nw      --integrity=MediumPlus notepad \"1 2 3 4\"").Parse());
        }

        [TestMethod]
        public void CmdLine_Help()
        {
            Action<ICommand> validate = (ICommand result) =>
            {
                var runCmd = result as HelpCommand;
                Assert.IsNotNull(runCmd);
            };

            validate(new CommandLineParser("/h").Parse());
            validate(new CommandLineParser("-h").Parse());
            validate(new CommandLineParser("-?").Parse());
            validate(new CommandLineParser("/?").Parse());
            validate(new CommandLineParser("help").Parse());
            validate(new CommandLineParser("--help").Parse());
            validate(new CommandLineParser("-nwskh").Parse());            
        }
    }
}

