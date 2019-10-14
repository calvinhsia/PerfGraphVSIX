using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class NativeTests : BaseTestClass
    {
        [TestMethod]
        public void TestRegex()
        {
            NativeMethods.TestRegEx(1, "^Microsoft.VisualStudio.Text.Implementation.TextBuffer", "Microsoft.VisualStudio.Text.Implementation.TextBuffer", IsCaseSensitive: true, out string res);
            Assert.AreEqual(res, "Match");
            NativeMethods.TestRegEx(1, "Microsoft.VisualStudio.Text.Implementation.TextBuffer", "Microsoft.VisualStudio.Text.Implementation.TextBuffer", IsCaseSensitive: true, out res);
            Assert.AreEqual(res, "Match");

            NativeMethods.TestRegEx(1, ".*TextBuffer.*", "Microsoft.VisualStudio.Text.Implementation.TextBuffer", IsCaseSensitive: true, out res);
            Assert.AreEqual(res, "Match");

            NativeMethods.TestRegEx(1, ".*zzzTextBuffer.*", "Microsoft.VisualStudio.Text.Implementation.TextBuffer", IsCaseSensitive: true, out res);
            Assert.AreEqual(res, "No Match");
        }

        [TestMethod]
        [Ignore]
        public void TestRegexManyStrings()
        {
            var testStrings = new[]
            {
                "Microsoft.VisualStudio.Text.Implementation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemsentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemfentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemdentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemgentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemaentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemasdentation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implemenatation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementsfasfddation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementationasdfaa.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementasdfation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementatiasdfon.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementatioadfn.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementatioasdfn.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementatioasdfn.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementation.TextBuffer",
                "Microsoft.VisualStudio.Text.Implementation.TextBuffer",
            };
            var testPats = new[]
            {
                "Microsoft.VisualStudio.Text.Implementation.TextBuffer",
                ".*TextBuffer.*",
            };
            var sw = new Stopwatch();
            foreach (var testPat in testPats)
            {
                sw.Restart();
                foreach (var testStr in testStrings)
                {
                    NativeMethods.TestRegEx(100000, testPat, testStr, IsCaseSensitive: true, out _);
                }
                sw.Stop();
                var el = sw.Elapsed;
                LogMessage($"{el.TotalMilliseconds:n0} {testPat}");
                //                var xx = NativeMethods.TestRegEx(".*TextBox.*", "Microsoft.VisualStudio.Text.Implementation.TextBuffer", out res);
            }

        }
    }

    public static class NativeMethods
    {
        public const string DllName = "ClrListener.dll";
        [DllImport(DllName, CharSet = CharSet.Unicode)]
        public static extern int TestRegEx(
            int numIterations,
            string pattern,
            string strData,
            bool IsCaseSensitive,
            [MarshalAs(UnmanagedType.BStr)]
            out String strFullPath
            );
    }

}
