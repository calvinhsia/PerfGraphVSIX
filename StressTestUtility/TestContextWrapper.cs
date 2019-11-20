using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Test.Stress
{
    /// <summary>
    /// Apex uses an older version:  Assembly Microsoft.VisualStudio.QualityTools.UnitTestFramework, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
    /// we're using                  Assembly Microsoft.VisualStudio.TestPlatform.TestFramework.Extensions, Version=14.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a

    /// </summary>
    public class TestContextWrapper
    {
        readonly object _testContext;

        // +		$exception	{"Cannot bind to the target method because its signature or security transparency is not compatible with that of the delegate type."}	System.ArgumentException
        //delegate void DelWriteLine(string str, object[] args);
        //readonly DelWriteLine delWriteLine;
        readonly MethodInfo methodInfoWriteLine;
        readonly MethodInfo methodInfoTestName;

        public TestContextWrapper(object testContext)
        {
            this._testContext = testContext;
            // we'll cache the common ones
            methodInfoWriteLine = _testContext.GetType().GetMethods().Where(m => m.Name == nameof(WriteLine) && m.GetParameters().Length == 2).First();
            methodInfoTestName = _testContext.GetType().GetMethod($"get_{nameof(TestName)}");
        }
        public string TestName
        {
            get
            {
                return methodInfoTestName?.Invoke(_testContext, null) as string;
            }
        }
        public IDictionary Properties
        {
            get
            {
                var meth = _testContext.GetType().GetMethod($"get_{nameof(Properties)}");
                return meth?.Invoke(_testContext, null) as IDictionary;
            }
        }
        public string TestRunDirectory
        {
            get
            {
                var meth = _testContext.GetType().GetMethod($"get_{nameof(TestRunDirectory)}");
                return meth?.Invoke(_testContext, null) as string;
            }
        }
        public string TestResultsDirectory
        {
            get
            {
                var meth = _testContext.GetType().GetMethod($"get_{nameof(TestResultsDirectory)}");
                return meth?.Invoke(_testContext, null) as string;
            }
        }
        public string TestRunResultsDirectory
        {
            get
            {
                var meth = _testContext.GetType().GetMethod($"get_{nameof(TestRunResultsDirectory)}");
                return meth?.Invoke(_testContext, null) as string;
            }
        }

        public string TestDeploymentDir
        {
            get
            {
                var meth = _testContext.GetType().GetMethod($"get_{nameof(TestDeploymentDir)}");
                return meth?.Invoke(_testContext, null) as string;
            }
        }

        public void WriteLine(string str)
        {
            this.WriteLine(str, new object[] { null });
        }

        public void WriteLine(string str, object[] args)
        {
            methodInfoWriteLine?.Invoke(_testContext, new object[] { str, args });
        }
        public void AddResultFile(string filename)
        {
            var meth = _testContext.GetType().GetMethod(nameof(AddResultFile));
            meth?.Invoke(_testContext, new object[] { filename});
        }

    }
}
