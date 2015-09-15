using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;

using Cloo;

namespace NQueens
{		
	class MainClass
	{
		public const int NumQueens = 14;
		public const int Spread = 44;

		// Usint "pack" layout ensures there are no memory gaps between
		// the struct members. We use the same strategy for the corresponding
		// OpenCL struct, preventing alignment issues.
		[StructLayoutAttribute(LayoutKind.Sequential, Pack = 1)]
		unsafe struct QueenState
		{
			public fixed long masks[NumQueens];
			public long solutions; // Number of solutinos found so far.
			public byte step;
			public byte col;
			public byte startCol; // First column in which this individual computation was tasked with filling.
			public long mask;
			public long rook;
			public long add;
			public long sub;
		}

		public static void Main (string[] args)
		{
			var platforms = ComputePlatform.Platforms;

			if (platforms.Count > 1) 
				Console.WriteLine("More than one platform available, maybe branch out...");

			var platform = platforms[0];
			var properties = new ComputeContextPropertyList (platform);
			var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero);
		
			PrintContextDetails(context);

			var init = new QueenState[Spread];

			for (int i = 0 ; i < init.Length; i++) 
			{
				init[i].mask = (1 << NumQueens) - 1;
			}

			var initBuffer = new ComputeBuffer<QueenState>(context,  
			                                               ComputeMemoryFlags.ReadWrite | ComputeMemoryFlags.CopyHostPointer, 
				                                           init);

			string queenKernelSource = GetQueenKernelSource();
			var program = new ComputeProgram(context, queenKernelSource);

			try 
			{ 
				program.Build(null, null, null, IntPtr.Zero);
			}
			catch 
			{
				string log = program.GetBuildLog(platform.Devices[0]);
				Console.WriteLine (log);
			}

			ComputeKernel kernel = null;

			try 
			{
				kernel = program.CreateKernel("place");
			}
			catch 
			{
				string log = program.GetBuildLog(platform.Devices[0]);
				Console.WriteLine (log);
			}


			kernel.SetMemoryArgument(0, initBuffer);

			ComputeEventList eventList = new ComputeEventList();

			var sw = new Stopwatch();
			sw.Start();

			var commands = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);
			commands.Execute(kernel, null, new long[] { Spread }, null, eventList);
			commands.ReadFromBuffer(initBuffer, ref init, false, eventList);
			commands.Finish();

			sw.Stop();

			Console.WriteLine(sw.ElapsedMilliseconds / 1000.0);

			int n = 0;
			foreach (var r in init) {
				Console.Write (n + ": ");
				Console.WriteLine(r.solutions);
				n++;
			}
		}

		public static string GetQueenKernelSource()
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

		public static void PrintContextDetails(ComputeContext context)
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
	}
}