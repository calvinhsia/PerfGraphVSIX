

#include "pch.h"
#include <cor.h>
#include <corprof.h>
#include "atlsafe.h"
#include "atlcom.h"



//see https://blogs.msdn.microsoft.com/calvin_hsia/2014/01/30/create-your-own-clr-profiler/



#include <initguid.h>

// {B9A7CD1D-6780-420E-9987-1D013F41910F}
DEFINE_GUID(CLSID_ClrListener,
	0xb9a7cd1d, 0x6780, 0x420e, 0x99, 0x87, 0x1d, 0x1, 0x3f, 0x41, 0x91, 0xf);



// Set COR_ENABLE_PROFILING=1
// Set COR_PROFILER={B9A7CD1D-6780-420E-9987-1D013F41910F}
// Set COR_PROFILER_PATH= C:\Users\calvinh\Source\Repos\PerfGraphVSIX\PerfGraphVSIX\bin\Debug\clrlistener.dll



#define PASTE2(x,y) x##y
#define PASTE(x,y) PASTE2(x,y)
#define __WFUNCDNAME__ PASTE(L, __FUNCDNAME__)

#define PROF_NOT_IMP(methodName, ...) \
	STDMETHOD(methodName) (__VA_ARGS__) \
{ \
if (m_fLoggingOn)  LogOutput(__WFUNCDNAME__);  \
	return S_OK;  \
} \

CComQIPtr<ICorProfilerInfo2 > g_pCorProfilerInfo; // can be null



class ClrListener :
	public ICorProfilerCallback9,
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<ClrListener, &CLSID_ClrListener>
{
public:
	BEGIN_COM_MAP(ClrListener)
		COM_INTERFACE_ENTRY_IID(CLSID_ClrListener, ClrListener)
		COM_INTERFACE_ENTRY(ICorProfilerCallback2)
		COM_INTERFACE_ENTRY(ICorProfilerCallback3)
		COM_INTERFACE_ENTRY(ICorProfilerCallback4)
	END_COM_MAP()
	DECLARE_NOT_AGGREGATABLE(ClrListener)
	DECLARE_NO_REGISTRY()

	ClrListener()
	{
		LogOutput(L"ClrListener Constructor");
	}
	bool m_fLoggingOn;
	void LogOutput(LPCWSTR wszFormat, ...)
	{
		if (IsDebuggerPresent())
		{
			SYSTEMTIME st;
			GetLocalTime(&st);
			WCHAR buf[1000];
			swprintf_s(buf, L"%2d/%02d/%02d %2d:%02d:%02d:%03d thrd=%d ", st.wMonth, st.wDay, st.wYear - 2000, st.wHour,
				st.wMinute, st.wSecond, st.wMilliseconds, GetCurrentThreadId());
			OutputDebugStringW(buf);
			va_list insertionArgs;
			va_start(insertionArgs, wszFormat);
			_vsnwprintf_s(buf, _countof(buf), wszFormat, insertionArgs);
			va_end(insertionArgs);
			OutputDebugStringW(buf);
			OutputDebugStringW(L"\r\n");
		}
	}

	// ICorProfilerCallback2 >>

	// STARTUP/SHUTDOWN EVENTS
	STDMETHOD(Initialize)(IUnknown* pICorProfilerInfoUnk)
	{
		m_fLoggingOn = true;
		// tell clr which events we want to be called for
		DWORD dwEventMask = 
			//COR_PRF_ENABLE_STACK_SNAPSHOT
			COR_PRF_ENABLE_OBJECT_ALLOCATED
			//| COR_PRF_MONITOR_GC //GarbageCollectionStarted, GarbageCollectionFinished, MovedReferences, SurvivingReferences, ObjectReferences, ObjectsAllocatedByClass, RootReferences, HandleCreated, HandleDestroyed, and FinalizeableObjectQueued callbacks.
			| COR_PRF_MONITOR_OBJECT_ALLOCATED // Object
			////      | COR_PRF_MONITOR_ENTERLEAVE // method enter/leave
			//| COR_PRF_MONITOR_CLASS_LOADS // ClassLoad and ClassUnload 
			//| COR_PRF_MONITOR_MODULE_LOADS // ModuleLoad, ModuleUnload, and ModuleAttachedToAssembly callbacks.
			//| COR_PRF_MONITOR_ASSEMBLY_LOADS // AssemblyLoad and AssemblyUnload callbacks
			//| COR_PRF_MONITOR_APPDOMAIN_LOADS // ModuleLoad, ModuleUnload, and ModuleAttachedToAssembly callbacks.
			//| COR_PRF_MONITOR_SUSPENDS //Controls the RuntimeSuspend, RuntimeResume, RuntimeThreadSuspended, and RuntimeThreadResumed callbacks.
			//| COR_PRF_MONITOR_THREADS // Controls the ThreadCreated, ThreadDestroyed, ThreadAssignedToOSThread, and ThreadNameChanged callbacks
			;
		g_pCorProfilerInfo->SetEventMask(dwEventMask);
		return S_OK;
	}
	STDMETHOD(ObjectAllocated)(ObjectID objectID, ClassID classID)
	{ // gets called whenever a managed obj is created
		HRESULT hr = S_OK;


		return S_OK;
	}

	STDMETHOD(Shutdown)()
	{
		return S_OK;
	}
	// APPLICATION DOMAIN EVENTS
	PROF_NOT_IMP(AppDomainCreationStarted, AppDomainID appDomainId);
	PROF_NOT_IMP(AppDomainCreationFinished, AppDomainID appDomainId, HRESULT hr);
	PROF_NOT_IMP(AppDomainShutdownStarted, AppDomainID);
	PROF_NOT_IMP(AppDomainShutdownFinished, AppDomainID appDomainId, HRESULT hr);

	// ASSEMBLY EVENTS
	PROF_NOT_IMP(AssemblyLoadStarted, AssemblyID);
	PROF_NOT_IMP(AssemblyLoadFinished, AssemblyID, HRESULT);
	PROF_NOT_IMP(AssemblyUnloadStarted, AssemblyID);
	PROF_NOT_IMP(AssemblyUnloadFinished, AssemblyID assemblyID, HRESULT hr);

	// MODULE EVENTS
	PROF_NOT_IMP(ModuleLoadStarted, ModuleID);
	PROF_NOT_IMP(ModuleLoadFinished, ModuleID moduleID, HRESULT hr);
	PROF_NOT_IMP(ModuleUnloadStarted, ModuleID moduleId);
	PROF_NOT_IMP(ModuleUnloadFinished, ModuleID, HRESULT);
	PROF_NOT_IMP(ModuleAttachedToAssembly, ModuleID moduleID, AssemblyID assemblyID);

	// CLASS EVENTS
	PROF_NOT_IMP(ClassLoadStarted, ClassID classId);
	PROF_NOT_IMP(ClassLoadFinished, ClassID classId, HRESULT hr);
	PROF_NOT_IMP(ClassUnloadStarted, ClassID classId);
	PROF_NOT_IMP(ClassUnloadFinished, ClassID, HRESULT);
	PROF_NOT_IMP(FunctionUnloadStarted, FunctionID);

	// JIT EVENTS
	PROF_NOT_IMP(JITCompilationStarted, FunctionID functionID, BOOL fIsSafeToBlock);
	PROF_NOT_IMP(JITCompilationFinished, FunctionID functionID, HRESULT hrStatus, BOOL fIsSafeToBlock);
	PROF_NOT_IMP(JITCachedFunctionSearchStarted, FunctionID functionId, BOOL* pbUseCachedFunction);
	PROF_NOT_IMP(JITCachedFunctionSearchFinished, FunctionID, COR_PRF_JIT_CACHE);
	PROF_NOT_IMP(JITFunctionPitched, FunctionID);
	PROF_NOT_IMP(JITInlining, FunctionID, FunctionID, BOOL*);

	// THREAD EVENTS
	PROF_NOT_IMP(ThreadCreated, ThreadID);
	PROF_NOT_IMP(ThreadDestroyed, ThreadID);
	PROF_NOT_IMP(ThreadAssignedToOSThread, ThreadID, DWORD);

	// REMOTING EVENTS
	// Client-side events
	PROF_NOT_IMP(RemotingClientInvocationStarted);
	PROF_NOT_IMP(RemotingClientSendingMessage, GUID*, BOOL);
	PROF_NOT_IMP(RemotingClientReceivingReply, GUID*, BOOL);
	PROF_NOT_IMP(RemotingClientInvocationFinished);
	// Server-side events
	PROF_NOT_IMP(RemotingServerReceivingMessage, GUID*, BOOL);
	PROF_NOT_IMP(RemotingServerInvocationStarted);
	PROF_NOT_IMP(RemotingServerInvocationReturned);
	PROF_NOT_IMP(RemotingServerSendingReply, GUID*, BOOL);

	// CONTEXT EVENTS
	PROF_NOT_IMP(UnmanagedToManagedTransition, FunctionID, COR_PRF_TRANSITION_REASON);
	PROF_NOT_IMP(ManagedToUnmanagedTransition, FunctionID, COR_PRF_TRANSITION_REASON);

	// SUSPENSION EVENTS
	PROF_NOT_IMP(RuntimeSuspendStarted, COR_PRF_SUSPEND_REASON);
	PROF_NOT_IMP(RuntimeSuspendFinished);
	PROF_NOT_IMP(RuntimeSuspendAborted);
	PROF_NOT_IMP(RuntimeResumeStarted);
	PROF_NOT_IMP(RuntimeResumeFinished);
	PROF_NOT_IMP(RuntimeThreadSuspended, ThreadID);
	PROF_NOT_IMP(RuntimeThreadResumed, ThreadID);

	// GC EVENTS
	PROF_NOT_IMP(MovedReferences, ULONG cmovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]);

	PROF_NOT_IMP(ObjectsAllocatedByClass, ULONG classCount, ClassID classIDs[], ULONG objects[]);
	PROF_NOT_IMP(ObjectReferences, ObjectID objectID, ClassID classID, ULONG cObjectRefs, ObjectID objectRefIDs[]);
	PROF_NOT_IMP(RootReferences, ULONG cRootRefs, ObjectID rootRefIDs[]);

	// Exception creation
	PROF_NOT_IMP(ExceptionThrown, ObjectID);

	// Exception Caught
	PROF_NOT_IMP(ExceptionCatcherEnter, FunctionID, ObjectID);
	PROF_NOT_IMP(ExceptionCatcherLeave);

	// Search phase
	PROF_NOT_IMP(ExceptionSearchFunctionEnter, FunctionID);
	PROF_NOT_IMP(ExceptionSearchFunctionLeave);
	PROF_NOT_IMP(ExceptionSearchFilterEnter, FunctionID);
	PROF_NOT_IMP(ExceptionSearchFilterLeave);
	PROF_NOT_IMP(ExceptionSearchCatcherFound, FunctionID);

	// Unwind phase
	PROF_NOT_IMP(ExceptionUnwindFunctionEnter, FunctionID);
	PROF_NOT_IMP(ExceptionUnwindFunctionLeave);
	PROF_NOT_IMP(ExceptionUnwindFinallyEnter, FunctionID);
	PROF_NOT_IMP(ExceptionUnwindFinallyLeave);

	PROF_NOT_IMP(ExceptionCLRCatcherFound); // Deprecated in .Net 2.0
	PROF_NOT_IMP(ExceptionCLRCatcherExecute); // Deprecated in .Net 2.0

	PROF_NOT_IMP(ExceptionOSHandlerEnter, FunctionID); // Not implemented
	PROF_NOT_IMP(ExceptionOSHandlerLeave, FunctionID); // Not implemented

	// IID_ICorProfilerCallback2 EVENTS
	PROF_NOT_IMP(ThreadNameChanged, ThreadID threadId, ULONG cchName, __in WCHAR name[]);
	PROF_NOT_IMP(GarbageCollectionStarted, int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason);
	PROF_NOT_IMP(SurvivingReferences, ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]);
	PROF_NOT_IMP(GarbageCollectionFinished);
	PROF_NOT_IMP(FinalizeableObjectQueued, DWORD finalizerFlags, ObjectID objectID);
	PROF_NOT_IMP(RootReferences2, ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]);
	PROF_NOT_IMP(HandleCreated, GCHandleID handleId, ObjectID initialObjectId);
	PROF_NOT_IMP(HandleDestroyed, GCHandleID handleId);

	// COM CLASSIC VTable
	PROF_NOT_IMP(COMClassicVTableCreated, ClassID wrappedClassID, REFGUID implementedIID, void* pVTable, ULONG cSlots);

	PROF_NOT_IMP(COMClassicVTableDestroyed, ClassID wrappedClassID, REFGUID implementedIID, void* pVTable);
	// ICorProfilerCallback2 <<

	PROF_NOT_IMP(InitializeForAttach, IUnknown* punk, void* data, UINT datasize);
	PROF_NOT_IMP(ProfilerAttachComplete);
	PROF_NOT_IMP(ProfilerDetachSucceeded);


	// ICorProfilerCallback4
	PROF_NOT_IMP(ReJITCompilationStarted, FunctionID * functionId, ReJITID rejitId, BOOL fIsSafeToBlock);
};

OBJECT_ENTRY_AUTO(CLSID_ClrListener, ClrListener)

// define a class that represents this module
class CClrProfModule : public ATL::CAtlDllModuleT< CClrProfModule >
{
#if _DEBUG
public:
	CClrProfModule()
	{
		int x = 0; // set a bpt here
	}
	~CClrProfModule()
	{
		int x = 0; // set a bpt here
	}
#endif _DEBUG
};


// instantiate a static instance of this class on module load
CClrProfModule _AtlModule;
// this gets called by CLR due to env var settings
STDAPI DllGetClassObject(__in REFCLSID rclsid, __in REFIID riid, __deref_out LPVOID FAR* ppv)
{
	HRESULT hr = E_FAIL;
	hr = AtlComModuleGetClassObject(&_AtlComModule, rclsid, riid, ppv);
	//  hr= CComModule::GetClassObject();
	return hr;
}
//tell the linker to export the function
#pragma comment(linker, "/EXPORT:DllGetClassObject=_DllGetClassObject@12,PRIVATE")