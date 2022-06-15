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





To update ClrObjExplorer:
// make sure to build release: (with stress repo named "Stress" and not "PerfGraphVSIX")
xcopy /fsdy C:\Users\calvinh\source\repos\VSDbg\out\Release\ClrObjExplorer64\bin\*.* C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\Resources\
xcopy /fsdy C:\Users\calvinh\source\repos\VSDbg\out\Release\ClrObjExplorer\bin\*.* C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\Resources\

then open "C:\Users\calvinh\source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\Resources\ClrObjExplorer.zip", delete all contents,
then move the files into the zip. That resources folder should have 3 zips: VSHandler32/64 and ClrobjExplorer.zip
		


//deploy VSIX: Bump version of source.extension.vsixmanifest so end user clicks, and it updates 1.1.1.425
// else user has to uninstall old, reinstall
xcopy /dy c:\Users\calvinh\Source\repos\PerfGraphVSIX\PerfGraphVSIX\bin\release\PerfGraphVSIX.vsix \\calvinh6\public



When PR build succeeds, wait for rolling build, 
The build def: https://dev.azure.com/devdiv/Engineering/_build?definitionId=12376

Verify the new stress version:
	https://devdiv.visualstudio.com/DevDiv/_artifacts/feed/VS
	https://devdiv.visualstudio.com/DevDiv/_packaging?_a=feed&feed=Engineering%40Local   the "VS"" feed, filter to "Stress"

Or create a new Wpf sln, use Sln->Manage NugetPackages for solution, choose the VS feed, the new version should show up)


Ensure the version is a public release: the # does not have a hyphenated git commit suffix.

https://github.com/dotnet/Nerdbank.GitVersioning/blob/master/doc/public_vs_stable.md

To change VS repo to update stress lib: change .corext\Configs\default.config

    <package id="Microsoft.Test.Stress" version="1.1.30" link="src\ExternalAPIs\Microsoft.Test.Stress" tags="exapis" />
	similarly /src/ConfigData/Packages/TestSupport.props
	see https://devdiv.visualstudio.com/DevDiv/_git/VS/pullrequest/384299



// deploy to diff machine with VS enlistment
xcopy /dy c:\Users\calvinh\Source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\bin\Release\*.dll \vs\src\ExternalAPIs\Microsoft.Test.Stress\lib\net461
xcopy /dy c:\Users\calvinh\Source\repos\Stress\Microsoft.Test.Stress\Microsoft.Test.Stress\bin\Release\VSHandler64\*.dll \vs\src\ExternalAPIs\Microsoft.Test.Stress\lib\net461\VSHandler64\

this repo is in 2 places: 
	https://github.com/calvinhsia/PerfGraphVSIX.git
	https://devdiv.visualstudio.com/DefaultCollection/Engineering/_git/DevDivStress

git push GitHub --all
