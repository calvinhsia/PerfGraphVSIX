using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Microsoft.Test.Stress")]
[assembly: AssemblyDescription("Memory analsysis and leak detection")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Microsoft")]
[assembly: AssemblyProduct("Microsoft.Test.Stress")]
[assembly: AssemblyCopyright("Copyright © Microsoft 2019")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("a9794ff3-c487-4801-b634-8de8c2b7c7ec")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]


[assembly: SuppressMessage("Compiler","CS1591",Justification ="")]

[assembly: AssemblyVersion("1.1.1.427")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: InternalsVisibleTo("Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c93d2644dcec6c893c44629eb99ab447dc9a84a3629c72c7860a4a1d825a60ca105646c9e42326a706b0e82207c0d16994d66af42c899f978143fd8a5ff8f2bf2af88767e381ea780f95731316004b48738a17c59cda505ec2bba4cd4aa7d21eb73ad10bd4389ffc8d8d71bbf5c645afb0564e87ef592814e69296d25e8117c1")]
[assembly: InternalsVisibleTo("TestStress, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c93d2644dcec6c893c44629eb99ab447dc9a84a3629c72c7860a4a1d825a60ca105646c9e42326a706b0e82207c0d16994d66af42c899f978143fd8a5ff8f2bf2af88767e381ea780f95731316004b48738a17c59cda505ec2bba4cd4aa7d21eb73ad10bd4389ffc8d8d71bbf5c645afb0564e87ef592814e69296d25e8117c1")]
[assembly: InternalsVisibleTo("PerfGraphVSIX, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c93d2644dcec6c893c44629eb99ab447dc9a84a3629c72c7860a4a1d825a60ca105646c9e42326a706b0e82207c0d16994d66af42c899f978143fd8a5ff8f2bf2af88767e381ea780f95731316004b48738a17c59cda505ec2bba4cd4aa7d21eb73ad10bd4389ffc8d8d71bbf5c645afb0564e87ef592814e69296d25e8117c1")]
[assembly: InternalsVisibleTo("TestStressDll, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c93d2644dcec6c893c44629eb99ab447dc9a84a3629c72c7860a4a1d825a60ca105646c9e42326a706b0e82207c0d16994d66af42c899f978143fd8a5ff8f2bf2af88767e381ea780f95731316004b48738a17c59cda505ec2bba4cd4aa7d21eb73ad10bd4389ffc8d8d71bbf5c645afb0564e87ef592814e69296d25e8117c1")]
//[assembly: InternalsVisibleTo("PerfGraphVSIX, PublicKey=0024000004800000940000000602000000240000525341310004000001000100c93d2644dcec6c893c44629eb99ab447dc9a84a3629c72c7860a4a1d825a60ca105646c9e42326a706b0e82207c0d16994d66af42c899f978143fd8a5ff8f2bf2af88767e381ea780f95731316004b48738a17c59cda505ec2bba4cd4aa7d21eb73ad10bd4389ffc8d8d71bbf5c645afb0564e87ef592814e69296d25e8117c1")]
