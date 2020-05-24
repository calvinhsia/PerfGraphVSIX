# PerfGraphVSIX


A Visual Studio extension that allows rapid prototyping: developing/running code inside the same instance of VS,
accessing services from that instance of VS
Code Samples of memory leaks, including Event Handlers (both normal event handlers and WPF RoutedEventHandlers), CancellationTokenSource leaks

You can:
Develop and run code in the VS instance from within the same VS process with:
1.	No cloning, no enlistment, no Init.cmd, no command line build, no MSBuild,
2.	No patching of VS install with built binaries
3.	No solution, no project required
4.	Full VS Editor, rename, some Intellisense
5.	Supports async programming, JoinableTaskFactory, multiple files
6.	Access VS services, JoinableTaskFactory
7.	Monitor memory use in a graph for leak detection
8.	Can launch ClrObjectExplorer with a push of a button to see how many instances of MyType are in memory
9.	Allows rapid prototyping, exploration of VS services, threading

Stress Testing for leak detection is also supported:
1. Create automatic tests that are run from the Test Window or from within the current instance of VS
2. These tests automatically iterate code, take memory measurements, take 2 dumps to compare object counts between iterations for leak detection


Install from https://github.com/calvinhsia/PerfGraphVSIX/releases/latest
View Menu->Other Windows->PerfGraphToolWindow
Choose the Options pane, select the LeakWpfEventHandler.cs file, dbl-click to open it, and click the ExecCodeButton to run it.
Other samples: CancellationTokenSource leak, EventHandler (non-WPF) leak, ThreadPool Starvation Demo

A button click will create a process dump of the current VS process and open the dump in ClrObjectExplorer, so individual classes and objects
can be examined, aggregated, references can be viewed, and leaks can be easily found.

See also https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/how-to-monitor-and-respond-to-memory-use

