PerfGraphVSIX

Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




// make sure to build release:

xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility

//deploy
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public


https://docs.microsoft.com/en-us/nuget/quickstart/create-and-publish-a-package-using-visual-studio-net-framework

C:\Users\calvinh\Source\repos\PerfGraphVSIX\StressTestUtility>nuget pack StressTestUtility.nuspec -OutputDirectory bin\release



nuget push StressTestUtility.1.1.0.nupkg oy2kaerby7bconfmf5qazrjif7b7tyuenmlppplvt2qefy -src https://api.nuget.org/v3/index.json

dotnet nuget push DictionaryLib_Calvin_Hsia.1.0.0.nupkg -k oy2kaerby7bconfmf5qazrjif7b7tyuenmlppplvt2qefy -s https://api.nuget.org/v3/index.json