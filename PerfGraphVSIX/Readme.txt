Microsoft.Test.Stress

Stress test code to automatically detect memory leaks

The code in the repo:
•	The test framework code so authors can create tests similar to DDRits, RPS that would detect leaks and other code stress problems (like fuzzing APIs, timing, etc)
		The ability to run the same code on the Dev desktop
•	The code of a VS Extension (currently called PerfGraphVSIX) that allows you to examine memory for leaks. 
•	The library shared between the two. 

Wiki Page https://devdiv.visualstudio.com/DevDiv/_wiki/wikis/DevDiv.wiki/3829/Show-VS-memory-use-in-a-graph-PerfGraphVSIX


Repo:
https://devdiv.visualstudio.com/Engineering/_git/DevDivStress

Enginering feed:
https://devdiv.visualstudio.com/Engineering/_packaging?_a=feed&feed=Engineering
e.g.
	https://devdiv.visualstudio.com/Engineering/_packaging?_a=package&feed=Engineering&package=Microsoft.Test.Stress&protocolType=NuGet&version=1.1.6

Tools->Options:
	https://devdiv.pkgs.visualstudio.com/_packaging/Engineering/nuget/v3/index.json



Note: this will cause significant delay every UpdateInterval because it does Full GC and tracking of newly referenced objects




To update ClrObjExplorer:
// make sure to build release:
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.exe C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrobjexplorer.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.dll C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
xcopy /dy C:\Users\calvinh\Source\repos\VSDbg\out\release\VsDbg\bin\x86\ClrObjExplorer\Clrlib.pdb C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer
then zip them into "C:\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\ClrObjExplorer\ClrObjExplorer.zip"


//deploy VSIX: Bump version of source.extension.vsixmanifest so end user clicks, and it updates 1.1.1.425
// else user has to uninstall old, reinstall
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public



When PR build succeeds, wait for rolling build, then check Artifacts-> filter to "stress" to get version # like "1.1.30"

To change VS repo: change .corext\Configs\default.config

    <package id="Microsoft.Test.Stress" version="1.1.30" link="src\ExternalAPIs\Microsoft.Test.Stress" tags="exapis" />



// deploy to diff machine with VS enlistment
xcopy /dy \\calvinh2\c$\Users\calvinh\Source\repos\PerfGraphVSIX\Microsoft.Test.Stress\Microsoft.Test.Stress\bin\Release\Microsoft.Test.Stress.dll \vs\src\ExternalAPIs\Microsoft.Test.Stress\lib\net461

this repo is in 2 places: 
	https://github.com/calvinhsia/PerfGraphVSIX.git
	https://devdiv.visualstudio.com/DefaultCollection/Engineering/_git/DevDivStress

git push GitHub --all
