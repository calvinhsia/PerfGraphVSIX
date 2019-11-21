PerfGraphVSIX

Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




// make sure to build release:

xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\ClrObjExplorer

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