using ClrUtils;
using System.Diagnostics;
using CommandLine;
using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;


namespace EnumAppDomains
{
    internal class Program
    {
        private static void Main(string[] args)
        {

            ParserResult<CommandLineOptions> commandLineOptions =
                Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(options => RunApplication(options)).WithNotParsed(erropts => ShowHelp(erropts));

        }

        private static void RunApplication(CommandLineOptions opts)
        {
            if (opts.PID == 0 && String.IsNullOrEmpty(opts.ProcessName)) {
                ShowHelp(null);
                return; } ;
            List<int> pids = new List<int>();
            if (opts.PID != 0) {
                pids.Add( opts.PID);
            }
            if(! String.IsNullOrEmpty(opts.ProcessName)){
                var o = Process.GetProcessesByName(opts.ProcessName);              
                foreach(var s in o)
                {
                    pids.Add(s.Id);
                }
            }

            if( pids.Any()){
                Console.WriteLine($"Dumping {pids.Count} process ids.");
                foreach (int thisPid in pids) {
                    try
                    {
                        bool currProc64 = CLRUtility.Is64BitProcess();
                        bool remoteProc64 = CLRUtility.Is64BitProcess(thisPid);

                        Console.WriteLine("");
                        Console.WriteLine($"Process ID: {thisPid}");

                        if (currProc64 != remoteProc64)
                        {
                            Console.WriteLine("Process Bitness must match. This process is {0}, {1} is {2}", currProc64 ? "64-bit" : "32-bit",
                                thisPid, remoteProc64 ? "64-bit" : "32-bit");
                            return;

                        }

                        using (DataTarget d = DataTarget.AttachToProcess(thisPid, 5000, AttachFlag.NonInvasive))
                        {
                            Console.WriteLine($"Dumping all CLRversions for process {thisPid}");
                            foreach (ClrInfo currInfo in d.ClrVersions)
                            {

                                ClrRuntime runtime = currInfo.CreateRuntime();
                                Console.WriteLine($"CLR: {currInfo.Version} GC: {(runtime.ServerGC ? "Server      " : "Workstation")} HeapSize: 0:{runtime.Heap.GetSizeByGen(0),12} 1:{runtime.Heap.GetSizeByGen(1),12} L:{runtime.Heap.GetSizeByGen(2),12}", "");
                                foreach (ClrAppDomain domain in runtime.AppDomains)
                                {

                                    IEnumerable<ClrThread> threadList = runtime.Threads.Where(x => (x.AppDomain == domain.Address));
                                    string domainInfo = $"App Domain: {domain.Name.PadRight(40)} ID: {domain.Id} Threads: {threadList.Count()} Address: {domain.Address,12:X8}";
                                    Console.WriteLine(domainInfo);

                                    Console.WriteLine($"Modules: {domain.Modules.Count.ToString("D3")} (showing GAC? {opts.GAC})");
                                    foreach (ClrModule currMod in domain.Modules)
                                    {
                                        bool printMe = (!string.IsNullOrEmpty(currMod.AssemblyName)) && (!currMod.AssemblyName.Contains("\\GAC_") | opts.GAC);
                                        if (printMe) Console.WriteLine($"{currMod.AssemblyName}");
                                    }

                                    if (!opts.HideThreads)
                                    {
                                        Console.WriteLine("");
                                        Console.WriteLine("Threads:");

                                        foreach (ClrThread clrt in threadList)
                                        {
                                            string threadInfo = string.Format("  Thread: {0,3:G} NT: {1,12:X8} GC: {2} {3}",
                                                clrt.ManagedThreadId,
                                                clrt.OSThreadId, clrt.IsGC, clrt.IsAlive ? "    " : "DEAD");

                                            Console.WriteLine(threadInfo);
                                            int frameNum = 0;
                                            if (opts.ShowFrames)
                                            {
                                                foreach (ClrStackFrame frame in clrt.StackTrace)
                                                {

                                                    string frameInfo =
                                                        $"    Frame: {frameNum,2:G} IP: {frame.InstructionPointer,12:X} {frame.DisplayString}";
                                                    Console.WriteLine(frameInfo);
                                                    frameNum++;

                                                }
                                            }

                                        }
                                    }


                                    if (opts.Heap)
                                    {

                                        Console.WriteLine("");
                                        Console.WriteLine("Heap Segments:");
                                        Console.WriteLine("{0,12} {1,12} {2,12} {3,12} {4,4} {5}", "Start", "End", "Committed", "Reserved", "Proc", "Type");
                                        foreach (ClrSegment thisSeg in runtime.Heap.Segments)
                                        {
                                            string type;
                                            if (thisSeg.IsEphemeral)
                                                type = "Ephemeral";
                                            else if (thisSeg.IsLarge)
                                                type = "Large";
                                            else
                                                type = "Gen2";

                                            Console.WriteLine("{0,12:X} {1,12:X} {2,12:X} {3,12:X} {4,4} {5}", thisSeg.Start, thisSeg.End, thisSeg.CommittedEnd, thisSeg.ReservedEnd, thisSeg.ProcessorAffinity, type);
                                        }

                                        if (opts.Recurse)
                                        {
                                            if (!runtime.Heap.CanWalkHeap)
                                            {
                                                Console.WriteLine("Can't walk heap!");

                                            }
                                            else
                                            {
                                                Console.WriteLine("Dumping LOH");
                                                foreach (ClrSegment thisSeg in runtime.Heap.Segments.Where(x => x.IsLarge))
                                                {
                                                    for (ulong objId = thisSeg.FirstObject; objId != 0; objId = thisSeg.NextObject(objId))
                                                    {
                                                        ClrType thisType = runtime.Heap.GetObjectType(objId);
                                                        ulong thisSize = thisType.GetSize(objId);
                                                        Console.WriteLine("{0,12:X} {1,8:n0} {2,1:n0} {3}", objId, thisSize, thisSeg.GetGeneration(objId), thisType.Name);
                                                    }
                                                }
                                            }
                                        }

                                    }

                                    Console.WriteLine("");

                                }
                            }
                        }

                    }
                    catch (ArgumentException ae)
                    {
                        Console.WriteLine(ae.Message);
                    }
                    catch (ApplicationException aex)
                    {
                        Console.WriteLine(aex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.GetType());
                        Console.WriteLine(ex.StackTrace);
                        Console.WriteLine("Cannot get the process. "
                                          + " Make sure the process exists and it's a managed process and if it's running as admin, you need to be too.");
                    }
                } //foreach (int thisPid in pids) {
            }
            else { Console.WriteLine($"No PIDS to process. Input Name: {opts.ProcessName} pid: {opts.PID}"); };

         
        }


        private static void ObjSize(ClrHeap thisHeap, ulong objId, out uint count, out ulong size)
        {
            // Evaluation stack
            Stack<ulong> eval = new Stack<ulong>();

            // To make sure we don't count the same object twice, we'll keep a set of all objects
            // we've seen before.  Note the ObjectSet here is basically just "HashSet<ulong>".
            // However, HashSet<ulong> is *extremely* memory inefficient.  So we use our own to
            // avoid OOMs.
            ObjectSet considered = new ObjectSet(thisHeap);

            count = 0;
            size = 0;
            eval.Push(objId);

            while (eval.Count > 0)
            {
                // Pop an object, ignore it if we've seen it before.
                objId = eval.Pop();
                if (considered.Contains(objId))
                    continue;

                considered.Add(objId);

                // Grab the type. We will only get null here in the case of heap corruption.
                ClrType type = thisHeap.GetObjectType(objId);
                if (type == null)
                {
                    string outputString = string.Format("{0} corrupt!", objId);
                    Console.WriteLine(outputString);
                    continue;
                }


                count++;
                size += type.GetSize(objId);

                // Now enumerate all objects that this object points to, add them to the
                // evaluation stack if we haven't seen them before.
                type.EnumerateRefsOfObject(objId, delegate (ulong child, int offset)
                {
                    if (child != 0 && !considered.Contains(child))
                        eval.Push(child);
                });
            }
        }

        private static void ShowHelp(IEnumerable<CommandLine.Error> opts)
        {
            Console.WriteLine("EnumAppDomains.exe");
            Console.WriteLine($"(c) 2018-{DateTime.Now.Year} Bill Loytty");
            Console.WriteLine("USAGE:  EnumAppDomains [--pid xxxx|--processname whatever] [-gac] [-hidethreads] [-heap] [-recurse]");
            Console.WriteLine("Shows all appdomains running in process xxxx");

            if (opts != null && opts.Any())
            {
                foreach (Error thisErr in opts)
                {

                    Console.WriteLine("Error : {0}", thisErr.ToString());

                }
            }
        }

        private class CommandLineOptions
        {
            
            [Option('p', "pid", HelpText = "Process Id to enumerate",Default = 0)]
            public int PID { get; set; }


            [Option('g', "gac", HelpText = "Display GACd assemblies.")]
            public bool GAC { get; set; }

            [Option('h', "heap", HelpText = "Display Heap Information.")]
            public bool Heap { get; set; }

            [Option('n', "processname", HelpText = "Instead of -p, use this to find process name.")]
            public string ProcessName { get; set; }

            [Option('r', "recurse", HelpText = "used with -heap, recurses each object.")]
            public bool Recurse { get; set; }

            [Option('t', "hidethreads", HelpText = "Don't show threads for each appdomain")]
            public bool HideThreads { get; set; }


            [Option('f', "showframes", HelpText = "Show threads stacks for each appdomain")]
            public bool ShowFrames { get; set; }


        }
    }
}
