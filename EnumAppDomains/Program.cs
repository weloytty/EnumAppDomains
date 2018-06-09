using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Text;
using System.Threading.Tasks;
using ClrUtils;
using CommandLine;
using CommandLine.Text;
using Microsoft.Diagnostics.Runtime;


namespace EnumAppDomains
{
	class Program
	{
		static void Main(string[] args)
		{


			var parser = new Parser(with =>
			{
				with.EnableDashDash = true;
				with.HelpWriter = Console.Out;
			});
			var commandLineOptions =
				parser.ParseArguments<CommandLineOptions>(args).WithParsed(opts => RunApplication(opts)).WithNotParsed(erropts => ShowHelp(erropts));

		}

		static void RunApplication(CommandLineOptions opts)
		{


			if (opts.PID != 0)
			{
				try
				{
					bool currProc64 = CLRUtility.Is64BitProcess();
					bool remoteProc64 = CLRUtility.Is64BitProcess(opts.PID);

					if (currProc64 != remoteProc64)
					{
						Console.WriteLine("Process Bitness must match. This process is {0}, {1} is {2}", currProc64 ? "64-bit" : "32-bit",
							opts.PID, remoteProc64 ? "64-bit" : "32-bit");
						return;

					}



					using (DataTarget d = DataTarget.AttachToProcess(opts.PID, 5000, AttachFlag.NonInvasive))
					{
						Console.WriteLine($"Dumping all CLRversions for process {opts.PID}");
						foreach (ClrInfo currInfo in d.ClrVersions)
						{

							ClrRuntime runtime = currInfo.CreateRuntime();
							Console.WriteLine($"CLR: {currInfo.Version} GC: {(runtime.ServerGC? "Server      " : "Workstation")} HeapSize: 0:{runtime.Heap.GetSizeByGen(0),12} 1:{runtime.Heap.GetSizeByGen(1),12} L:{runtime.Heap.GetSizeByGen(2),12}", "");
							foreach (ClrAppDomain domain in runtime.AppDomains)
							{

								IEnumerable<ClrThread> threadList = runtime.Threads.Where(x => (x.AppDomain == domain.Address));
								string domainInfo = $"App Domain: {domain.Name.PadRight(40)} ID: {domain.Id} Threads: {threadList.Count()} Address: {domain.Address,12:X8}";
								Console.WriteLine(domainInfo);

								Console.WriteLine($"Modules: {domain.Modules.Count.ToString("D3")} (showing GAC? {opts.GAC})");
								foreach (ClrModule currMod in domain.Modules)
								{
									bool printMe = (!String.IsNullOrEmpty(currMod.AssemblyName)) && (!currMod.AssemblyName.Contains("\\GAC_") | opts.GAC);
									if (printMe) Console.WriteLine($"{currMod.AssemblyName}");
								}

								Console.WriteLine("");
								Console.WriteLine("Threads:");

								foreach (ClrThread clrt in threadList)
								{
									string threadInfo = string.Format("  Thread: {0,3:G} NT: {1,12:X8} GC: {2} {3}", clrt.ManagedThreadId,
										clrt.OSThreadId, clrt.IsGC, clrt.IsAlive ? "    " : "DEAD");

									Console.WriteLine(threadInfo);
									int frameNum = 0;
									foreach (ClrStackFrame frame in clrt.StackTrace)
									{

										string frameInfo = String.Format("    Frame: {0,2:G} IP: {1,12:X} {2}", frameNum, frame.InstructionPointer,
											frame.DisplayString);
										Console.WriteLine(frameInfo);
										frameNum++;

									}

								}

								//Console.WriteLine("");
								//Console.WriteLine($"Memory By Segment ({domain.Name})");
								


								//foreach (var region in (from r in runtime.EnumerateMemoryRegions()
								//	where r.Type != ClrMemoryRegionType.ReservedGCSegment
								//		  && (r.AppDomain == domain)
								//	group r by r.Type into g
								//	let total = g.Sum(p => (uint)p.Size)
								//	orderby total descending
								//	select new
								//	{
								//		TotalSize = total,
								//		Count = g.Count(),
								//		Type = g.Key
								//	}))
								//{
								//	//Console.WriteLine("{0,6:n0} {1,12:n0} {2}", region.Count, region.TotalSize, region.Type.ToString());
								//	Console.WriteLine("{0,6:n0} {1,12:n0} {2}", region.Count, region.TotalSize, region.Type.ToString());
								//}




								if (opts.Heap)
								{

									Console.WriteLine("");
									Console.WriteLine("Heap Segments:");
									Console.WriteLine("{0,12} {1,12} {2,12} {3,12} {4,4} {5}", "Start", "End", "Committed", "Reserved", "Proc","Type");
									foreach (ClrSegment thisSeg in runtime.Heap.Segments)
									{
										string type;
										if (thisSeg.IsEphemeral)
											type = "Ephemeral";
										else if (thisSeg.IsLarge)
											type = "Large";
										else
											type = "Gen2";

										Console.WriteLine("{0,12:X} {1,12:X} {2,12:X} {3,12:X} {4,4} {5}", thisSeg.Start, thisSeg.End, thisSeg.CommittedEnd, thisSeg.ReservedEnd, thisSeg.ProcessorAffinity,type);
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
											foreach (ClrSegment thisSeg in runtime.Heap.Segments.Where(x=>x.IsLarge))
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


									//foreach (ClrObject thisHeapObj in runtime.Heap.EnumerateObjects())
									//{

									//	string heapInfo = String.Format("{0}:{1}:{2}", thisHeapObj.HexAddress, thisHeapObj.Size, thisHeapObj.Type.Name);

									//	Console.WriteLine(heapInfo);
									//	if (opts.Recurse)
									//	{
									//		ulong childSize = 0;
									//		uint childCount = 0;

									//		ObjSize(runtime.Heap, thisHeapObj.Address, out childCount, out childSize);

									//		heapInfo = String.Format("{0}:{1}:{2} (child size:{3} count: {4})", thisHeapObj.HexAddress, thisHeapObj.Type.Name, thisHeapObj.Size, childCount, childSize);
									//	}

									//	Console.WriteLine(heapInfo);


									//}
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
				catch (ApplicationException _applicationException)
				{
					Console.WriteLine(_applicationException.Message);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.Message);
					Console.WriteLine(ex.GetType());
					Console.WriteLine(ex.StackTrace);
					Console.WriteLine("Cannot get the process. "
									  + " Make sure the process exists and it's a managed process and if it's running as admin, you need to be too.");
				}
			}
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
					string outputString = String.Format("{0} corrupt!", objId);
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


		static void ShowHelp(IEnumerable<CommandLine.Error> opts)
		{
			Console.WriteLine("EnumAppDomains.exe:  Shows what appdomains are running in a process");
			Console.WriteLine("	(c) 2018 Bill Loytty");
			Console.WriteLine("	USAGE:  EnumAppDomains --pid xxxx [-gac] [-heap] [-recurse]");
			Console.WriteLine(" Shows all appdomains running in process xxxx");

			foreach (var error in opts)
			{
				Console.WriteLine("Error : {0}", error.ToString());
			}
		}

		class CommandLineOptions
		{
			//[Option("pid", HelpText = "Process ID to enumerate")]
			[Option('p', "pid", Default = false, HelpText = "Process Id to enumerate")]
			public int PID { get; set; }


			[Option('g', "gac", Default = false,
				HelpText = "Display GACd assemblies.")]
			public bool GAC { get; set; }

			[Option('h', "heap", Default = false, HelpText = "Display Heap Information.")]
			public bool Heap { get; set; }

			[Option('r', "recurse", Default = false, HelpText = "used with -heap, recurses each object.")]
			public bool Recurse { get; set; }


		}
	}
}
