using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Safety008;

internal static class ProcessTree
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct Entry {internal uint Size,Usage,Pid;internal IntPtr Heap;internal uint Module,Threads,Parent;internal int Priority;internal uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]internal string Exe;}
    [DllImport("kernel32.dll",SetLastError=true)]private static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool Process32First(IntPtr handle,ref Entry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern bool Process32Next(IntPtr handle,ref Entry entry);
    [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr handle);
    internal static Dictionary<int,int> Parents()
    {
        if(!OperatingSystem.IsWindows())throw new InvalidOperationException("Windows process identity required");
        var handle=CreateToolhelp32Snapshot(2,0);if(handle==new IntPtr(-1))throw new InvalidOperationException("Process inventory unavailable");
        try{var e=new Entry{Size=(uint)Marshal.SizeOf<Entry>(),Exe=""};var result=new Dictionary<int,int>();if(!Process32First(handle,ref e))throw new InvalidOperationException("Process inventory unavailable");do{result.Add((int)e.Pid,(int)e.Parent);}while(Process32Next(handle,ref e));return result;}finally{CloseHandle(handle);}
    }
    internal static ProcessIdentity[] CaptureTree(int root,int expectedParent)
    {
        var parents=Parents();if(parents.GetValueOrDefault(root)!=expectedParent)throw new InvalidOperationException("Browser root parent mismatch");
        var ids=new HashSet<int>{root};int before;do{before=ids.Count;foreach(var (pid,parent) in parents)if(ids.Contains(parent))ids.Add(pid);}while(before!=ids.Count);
        var result=new List<ProcessIdentity>();foreach(var pid in ids.Order()){using var p=Process.GetProcessById(pid);result.Add(ProcessIdentity.Capture(p,parents[pid]));}return result.ToArray();
    }
    internal static void RequireAbsent(IEnumerable<ProcessIdentity> identities)
    {
        foreach(var id in identities){try{using var p=Process.GetProcessById(id.Pid);if(!p.HasExited)throw new InvalidOperationException(p.StartTime.ToUniversalTime()==id.StartedUtc?"Owned process still running":"PID reused; identity uncertain");}catch(ArgumentException){}}
    }
}
