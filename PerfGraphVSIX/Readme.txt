Microsoft.Test.Stress

Stress test code to automatically detect memory leaks

The code in the repo:
•	The test framework code so authors can create tests similar to DDRits, RPS that would detect leaks and other code stress problems (like fuzzing APIs, timing, etc)
		The ability to run the same code on the Dev desktop
•	The code of a VS Extension (currently called PerfGraphVSIX) that allows you to examine memory for leaks. 
•	The library shared between the two. 

Wiki Page https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/3829/Show-VS-memory-use-in-a-graph-PerfGraphVSIX




Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




To update ClrObjExplorer:
// make sure to build release:
// Bump versions of both vsixmanifest and Microsoft.Test.Stress assemblies to be same: 1.1.1.425
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
then zip them into "C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer\ClrObjExplorer.zip"


//deploy VSIX
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public




