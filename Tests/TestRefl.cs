using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Test.Stress;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PerfGraphVSIX;

namespace Tests
{
    [TestClass]
    public class TestRefl : BaseTestClass
    {

        [TestMethod]
        [Ignore]
        public void TestImportRegFile()
        {
            var regFileData = @"Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\VisualStudio\RemoteSettings\LocalTest]

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\VisualStudio\RemoteSettings\LocalTest\HeapAllocs]
""HeapAllocStacksEnabled""=dword:00000001
""StackFrames""=dword:00000010
""HeapAllocMinValue""=dword:00100000
""HeapAllocSizes""=""48:0""
""HeapAllocStackMaxSize""=dword:00080000

";
            var regfileAdminData = @"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\14.0]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\14.0\VC]

[HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes]

";

            var tmpRegFileName = System.IO.Path.ChangeExtension( System.IO.Path.GetTempFileName(),".reg");
            System.IO.File.WriteAllText(tmpRegFileName, regFileData);

            var sb = new StringBuilder();
            Trace.WriteLine(System.IO.File.ReadAllText(tmpRegFileName));
            using (var proc = Utility.CreateProcess(@"reg", $"import {tmpRegFileName}", sb))
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();
            }
            Trace.WriteLine(sb.ToString());
            Assert.IsTrue(sb.ToString().Contains("The operation completed successfully."));
        }




        //[TestMethod]
        //public void InfiniteRecursion()
        //{
        //    InfiniteRecursion();

        //}


        Action<object> _AddObjectAction;
        [TestMethod]
        public void TestReflection()
        {

            var addedObjects = new List<string>();
            _AddObjectAction = (object o) =>
            {
                addedObjects.Add($"added {o.ToString()}");
            };

            var objectTracker = new ObjTracker(new PerfGraphToolWindowControl());
            AddObject(this);

            LogMessage($"done scanning");
            foreach (var itm in addedObjects)
            {
                LogMessage($"{itm}");
            }

        }

        readonly BindingFlags bFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
        void AddObject(object textView)
        {
            var hashVisitedObjs = new HashSet<object>();
            bool TryAddObjectVisited(object obj)
            {
                var fDidAdd = false;
                if (!hashVisitedObjs.Contains(obj))
                {
                    hashVisitedObjs.Add(obj);
                    _AddObjectAction(obj);
                    fDidAdd = true;
                }
                return fDidAdd;
            }
            void AddMemsOfObject(object obj, int nLevel = 1)
            {
                var objTyp = obj.GetType();
                if (obj != null && objTyp.IsClass && objTyp.FullName != "System.String")
                {
                    if (objTyp.IsArray)
                    {
                        var elemtyp = objTyp.GetElementType();
                        if (elemtyp.IsPrimitive || elemtyp.Name == "String")
                        {
                            return;
                        }
                        else
                        {
                            LogMessage($"AAAAAAAAAarray {elemtyp}");
                        }
                    }
                    if (TryAddObjectVisited(obj) && nLevel < 1000)
                    {
                        var members = objTyp.GetMembers(bFlags);
                        foreach (var mem in members.Where(m => m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property))
                        {
                            if (mem is FieldInfo fldInfo)
                            {
                                var valFld = fldInfo.GetValue(obj);
                                if (valFld != null)
                                {
                                    var valFldType = valFld.GetType();
                                    if (valFld.GetType().IsClass) // delegate or class (not value type or interfacer
                                    {
                                        var name = objTyp.FullName;
                                        switch (name)
                                        {
                                            case "System.Reflection.Pointer":
                                            case "System.String":
                                                break;
                                            default:
                                                LogMessage($"{new string(' ', nLevel)} {nLevel} {objTyp.Name} {fldInfo.Name} {fldInfo.FieldType.BaseType?.Name}  {valFld.GetType().Name}");
                                                if (valFld is EventHandler evHandler)
                                                {
                                                    "".ToString();
                                                }
                                                if (valFld is Object)
                                                {
                                                    AddMemsOfObject(valFld, nLevel + 1);
                                                }
                                                break;

                                        }
                                    }
                                }
                            }
                            else if (mem is PropertyInfo propInfo)
                            {
                                "".ToString();
                                try
                                {
                                    var valProp = propInfo.GetValue(obj); // dictionary.item[]
                                    AddMemsOfObject(valProp);
                                }
                                catch (Exception) { }
                            }
                        }
                    }
                }
            }
            AddMemsOfObject(textView);
        }
    }
}
