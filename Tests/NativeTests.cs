using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class NativeTests: BaseTestClass
    {
        [TestMethod]
        public void TestRegex()
        {
            var res = string.Empty;
            for (int i = 0; i < 10000000; i++)
            {
                var xx = NativeMethods.TestRegEx(".*TextBox.*", "Microso.TextBoxtwo", out res);
            }
//            Assert.Fail($"ran {xx} {res}");

        }
    }

    public static class NativeMethods
    {
        public const string DllName = "ClrListener.dll";
        [DllImport(DllName, CharSet = CharSet.Unicode)]
        public static extern int TestRegEx(
            string pattern,
            string strData,
            [MarshalAs(UnmanagedType.BStr)]
            out String strFullPath
            );
    }

}
