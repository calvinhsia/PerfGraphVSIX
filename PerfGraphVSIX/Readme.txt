PerfGraphVSIX

Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




// make sure to build release:

xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\DumperViewer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\DumperViewer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\DumperViewer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\DumperViewer

//deploy
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public
