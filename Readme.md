# PerfGraphVSIX

A Visual Studio extension that allows rapid prototyping: developing/running code inside the same instance of VS,
accessing services from that instance of VS
Works with VS 2019 and a new 64 bit version for VS 2022 +

Code Samples of memory leaks, including Event Handlers (both normal event handlers and WPF RoutedEventHandlers), CancellationTokenSource leaks

To use to show a graph of the current instance memory use:
	On the Options Pane, choose UpdateInterval to e.g. 1000, to take a measurement every 1000 milliseconds
	Choose the counters you want to measure. Typically GCBytesInAllHeaps (managed memory) and ProcessorPrivateBytes (native and managed)
	Then choose the Graph Pane to watch the memory consumption of VS.
	(if you open/close multiple instances of VS other than the current one, you may get an InvalidOperationException. This is a limitation of
	Performance Counters. You can just reselect the desired counters if you like)


You can:
Develop and run code in the VS instance from within the same VS process with:
1.	No cloning, no enlistment, no Init.cmd, no command line build, no MSBuild, no Nuget
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
Choose the Options pane, select a CodeSamples file, dbl-click to open it, and click the ExecCodeButton to run it.
Samples include:
	Leak Demos showing detecting and identifying leaks
		CancellationTokenSource leak
		EvenhtHandler leak: demonstrates leaking eventhandlers and how to find all the subscribers of an event
		EventHandler (non-WPF) leak: similarly for WPF RoutedEventHandlers
	Iterate Visaul Studio operations, monitoring memory use
		opening/closing a VS solution
		building a VS solution
		opening/closing files
	Practical samples
		ThreadPool Starvation Demo Shows how to detect and cause ThreadPool starvation
		MapFileDict: Store the huge contents of a System.Collections.Generic.Dictionary in a temporary file, rather than main memory
	Fun Samples
		Fish, Logo, Cartoon

A button click will create a process dump of the current VS process and open the dump in ClrObjectExplorer, so individual classes and objects
can be examined, aggregated, references can be viewed, and leaks can be easily found.

See also https://docs.microsoft.com/en-us/archive/blogs/calvin_hsia/how-to-monitor-and-respond-to-memory-use

