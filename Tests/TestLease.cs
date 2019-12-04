using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tests
{
    [TestClass]
    public class TestLease: BaseTestClass
    {
        public class Worker : MarshalByRefObject
        {
            public void PrintDomain()
            {
                Debug.WriteLine("Object is executing in AppDomain \"{0}\"",
                    AppDomain.CurrentDomain.FriendlyName);
            }
        }

        [TestMethod]
        [Ignore]
        public void TestMarshalByRef()
        {
            // Create an ordinary instance in the current AppDomain
            Worker localWorker = new Worker();
            localWorker.PrintDomain();

            // Create a new application domain, create an instance
            // of Worker in the application domain, and execute code
            // there.
            AppDomain ad = AppDomain.CreateDomain("New domain");
            Worker remoteWorker = (Worker)ad.CreateInstanceAndUnwrap(
                typeof(Worker).Assembly.FullName,
                "Worker");
            remoteWorker.PrintDomain();
        }
    }
}
