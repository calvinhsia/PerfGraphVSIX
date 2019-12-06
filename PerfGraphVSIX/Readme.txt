PerfGraphVSIX

Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




// make sure to build release:
// Bump versions of both vsixmanifest and Microsoft.Test.Stress assemblies to be same: 1.1.1.425
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
then zip them into "C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer\ClrObjExplorer.zip"


//deploy
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public




https://docs.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package-using-visual-studio-net-framework

use a valid dev cmd prompt:

C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\Tools\VsDevCmd.bat


C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility>

***!!!Bump version # in project properties->Assembly Information
nuget pack StressTestUtility.csproj -prop Configuration=Release -OutputDirectory bin\release
***!!! replace versionnumber:
rem nuget push bin\release\StressTestUtility.1.0.0.121.nupkg oy2kaerby7bconfmf5qazrjif7b7tyuenmlppplvt2qefy -src https://api.nuget.org/v3/index.json





Problem: 

Putting the current stress code in the validation repo

•	There are NO dependencies between existing Validation code and Stress code.
•	The Validation Solution has 88 projects. Mine will add 6 projs to the solution, resulting in dozens more tests in test explorer
•	Because mine includes a VSIX it will require everyone using that repo to add Workload to build VS Extensions.
•	Every checkin will require code review. Every commit, branch, pr, history, etc will be applied to both Val code and Stress code
•	Need 5 gig space for Validation repo
•	Much of my code is used for dev desktop execution analysis as well as unattended lab execution
•	When I make changes to the desktop execution, I need to check into the Validation repo, triggering a needless CI build
•	Solution wide style cop rules, etc.
•	I work on 3 different machines: Syncing my changes from one machine to another requires syncing entire Validation repo

