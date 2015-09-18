using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Cloo;

namespace NQueens
{		
	class MainClass
	{
		public const int NumQueens = 15;
		public const int Spread = 2048;

		public enum Step : byte { Place = 0, Remove = 1, Done = 2 }

		/// <summary>
		/// This struct matches the one used in the OpenCL C code, and represents
		/// a unit of work to be carried out in searching the solutinon tree.
		/// It also includes information about the portion of the computation
		/// that has already been carried out.
		/// 
		/// Using "pack" layout ensures there are no memory gaps between
		/// the struct members. We use the same strategy for the corresponding
		/// OpenCL struct, preventing alignment issues.
		/// </summary>
		[StructLayoutAttribute(LayoutKind.Sequential, Pack = 1)]
		unsafe struct QueenTask
		{
			public int id;
			public fixed long masks[NumQueens];
			public ulong solutions; // Number of solutinos found so far.
			public Step step;
			public byte col;
			public byte startCol; // First column in which this individual computation was tasked with filling.
			public long mask;
			public long rook;
			public long add;
			public long sub;
		}

		public static void Main(string[] args)
		{
			ComputeContext context = GetContext();
		
			PrintContextDetails(context);
				
			ComputeProgram program = GetBuiltProgram(context);

			ComputeKernel kernel = GetKernel(program);

			ConductSearch(context, kernel);
		}

		private static void ConductSearch(ComputeContext context, ComputeKernel kernel)
		{
			var todos = GetQueenTaskPartition(NumQueens, 4);
			var done = new List<QueenTask>();

			ComputeEventList eventList = new ComputeEventList();

			var commands = new ComputeCommandQueue(context, context.Devices[1], ComputeCommandQueueFlags.None);

			Console.WriteLine("Starting {0} tasks, and working {1} at a time.", todos.Count, Spread);

			QueenTask[] inProgress = GetNextAssignment(new QueenTask[] {}, todos, done);

			var sw = new Stopwatch();
			sw.Start();

			while (inProgress.Any())
			{
				var taskBuffer = 
					new ComputeBuffer<QueenTask>(context,  
						ComputeMemoryFlags.ReadWrite | ComputeMemoryFlags.CopyHostPointer, 
						inProgress);

				kernel.SetMemoryArgument(0, taskBuffer);
				commands.WriteToBuffer(inProgress, taskBuffer, false, null);

				for (int i = 0; i < 12; i++)
					commands.Execute(kernel, null, new long[] { inProgress.Length }, null, eventList);

				commands.ReadFromBuffer(taskBuffer, ref inProgress, false, eventList);
				commands.Finish();

				inProgress = GetNextAssignment(inProgress, todos, done);
			}

			sw.Stop();

			Console.WriteLine(sw.ElapsedMilliseconds / 1000.0);

			ulong sum = done.Select(state => state.solutions)
				            .Aggregate((total, next) => total + next);

			Console.WriteLine("Q({0})={1}", NumQueens, sum);
		}

		private static ComputeContext GetContext()
		{
			if (ComputePlatform.Platforms.Count > 1) 
				Console.WriteLine("More than one platform available, maybe branch out...");

			var platform = ComputePlatform.Platforms[0];
			var properties = new ComputeContextPropertyList(platform);
			var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero);

			return context;
		}

		private static ComputeProgram GetBuiltProgram(ComputeContext context)
		{
			string queenKernelSource = GetQueenKernelSource();

			var program = new ComputeProgram(context, queenKernelSource);

			try 
			{ 
				program.Build(null, null, null, IntPtr.Zero);
			}
			catch 
			{
				string log = program.GetBuildLog(context.Platform.Devices[0]);
				Console.WriteLine(log);
				throw;
			}

			return program;
		}

		private static ComputeKernel GetKernel(ComputeProgram program)
		{
			try 
			{
				return program.CreateKernel("place");
			}
			catch 
			{
				string log = program.GetBuildLog(program.Context.Platform.Devices[0]);
				Console.WriteLine(log);
				throw;
			}
		}

		private static QueenTask[] GetNextAssignment(QueenTask[] inProgress, Queue<QueenTask> todos, IList<QueenTask> done)
		{
			var nextAssignment = new List<QueenTask>();

			foreach (var task in inProgress) 
			{
				if (task.step == Step.Done) 
				{
					done.Add(task);
				} 
				else
				{
					nextAssignment.Add(task);
				}
			}

			while (nextAssignment.Count < Spread && todos.Any())
				nextAssignment.Add(todos.Dequeue());

			return nextAssignment.ToArray();
		}

		private static string GetQueenKernelSource()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "NQueens.queen_kernel.c";

			string code = "";
			using (var stream = assembly.GetManifestResourceStream (resourceName))
			using (var reader = new StreamReader(stream)) 
			{
				code = reader.ReadToEnd();
			}

			// Turn off GCC mode by removing #define
			code = code.Replace("#define GCC_STYLE", "");
			code = "#define NUM_QUEENS " + NumQueens + "\n" + code;

			return code;
		}

		private static void PrintContextDetails(ComputeContext context)
		{
			Console.WriteLine("[HOST]");
			Console.WriteLine(Environment.OSVersion);

			Console.WriteLine();
			Console.WriteLine("[OPENCL PLATFORM]");

			ComputePlatform platform = context.Platform;

			Console.WriteLine("Name: " + platform.Name);
			Console.WriteLine("Vendor: " + platform.Vendor);
			Console.WriteLine("Version: " + platform.Version);
			Console.WriteLine("Profile: " + platform.Profile);
			Console.WriteLine("Extensions:");

			foreach (string extension in platform.Extensions)
				Console.WriteLine(" + " + extension);

			Console.WriteLine();

			Console.WriteLine("Devices:");

			foreach (ComputeDevice device in context.Devices)
			{
				Console.WriteLine("\tName: " + device.Name);
				Console.WriteLine("\tVendor: " + device.Vendor);
				Console.WriteLine("\tDriver version: " + device.DriverVersion);
				Console.WriteLine("\tOpenCL version: " + device.Version);
				Console.WriteLine("\tCompute units: " + device.MaxComputeUnits);
				Console.WriteLine("\tGlobal memory: " + device.GlobalMemorySize + " bytes");
				Console.WriteLine("\tLocal memory: " + device.LocalMemorySize + " bytes");
				Console.WriteLine("\tImage support: " + device.ImageSupport);
				Console.WriteLine("\tExtensions:");

				foreach (string extension in device.Extensions)
					Console.WriteLine("\t + " + extension);
			}
		}

		// For an instnace of the N-Queens problem of size n, get a list of QueenTask
		// tasks that covers the entire problem. That is, when each task is complete
		// the number of solutions found by each task can be summed to determine the
		// total number of solutions.
		private static Queue<QueenTask> GetQueenTaskPartition(int numQueens, int splitDepth)
		{
			var wholeProblemTask = new QueenTask();
			wholeProblemTask.mask = (1 << numQueens) - 1;

			var taskQueue = new Queue<QueenTask>();

			SplitTask(wholeProblemTask, numQueens, splitDepth, taskQueue);

			return taskQueue;
		}

		private static void SplitTask(QueenTask baseTask, int numQueens, int levels, Queue<QueenTask> taskQueue)
		{
			// Base case for recursion.
			if (levels == 1)
			{
				baseTask.id = taskQueue.Count;
				taskQueue.Enqueue(baseTask);
				return;
			}

			for (int i = 0; i < numQueens; i++) 
			{
				long queen = 1 << i;

				var subTask = baseTask;
				subTask.id = 0; // Will be set later.
				// No need to set masks[] as it is scratch for the task code.
				subTask.solutions = 0;
				subTask.step = Step.Place;
				subTask.col = (byte)(baseTask.col + 1);
				subTask.startCol = (byte)(baseTask.col + 1);
				subTask.rook = subTask.rook | queen;
				subTask.add |= queen << baseTask.col;
				subTask.sub |= queen << (numQueens - 1 - baseTask.col);
				subTask.mask = ((1 << numQueens) - 1) & ~(subTask.rook | (subTask.add >> subTask.col) | (subTask.sub >> ((NumQueens - 1) - subTask.col)));

				if (Pop(subTask.rook) == subTask.col &&
					Pop(subTask.add) == subTask.col &&
					Pop(subTask.sub) == subTask.col)
				{
					SplitTask(subTask, numQueens, levels - 1, taskQueue);
				}
			}
		}

		private static byte Pop(long input)
		{
			byte count;

			for (count = 0; input != 0; count++)
				input &= input - 1;

			return count;
		}
	}
}