using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

using Cloo;
using System.Diagnostics;


namespace NQueens
{
	public struct QueenInit
	{
		public char blah; // TODO
	}

	public struct QueenResult
	{
		public long count;
	}

	class MainClass
	{
		public const int NumQueens = 8;
		public const int Spread = 44;

		public static void Main (string[] args)
		{
			var platforms = ComputePlatform.Platforms;

			if (platforms.Count > 1) 
				Console.WriteLine("More than one platform available, maybe branch out...");

			var platform = platforms[0];
			var properties = new ComputeContextPropertyList (platform);
			var context = new ComputeContext(platform.Devices, properties, null, IntPtr.Zero);
		
			var init = new QueenInit[Spread];
			var initBuffer = new ComputeBuffer<QueenInit>(context, 
				                                          ComputeMemoryFlags.ReadOnly | ComputeMemoryFlags.CopyHostPointer, 
				                                          init);

			var results = new QueenResult[Spread];
			var resultsBuffer = new ComputeBuffer<QueenResult>(context, 
				                                               ComputeMemoryFlags.WriteOnly, 
			                                                   results.Length);

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
			kernel.SetMemoryArgument(1, resultsBuffer);


			ComputeEventList eventList = new ComputeEventList();

			var sw = new Stopwatch();
			sw.Start();

			var commands = new ComputeCommandQueue(context, context.Devices[0], ComputeCommandQueueFlags.None);
			commands.Execute(kernel, null, new long[] { Spread }, null, eventList);
			commands.ReadFromBuffer(resultsBuffer, ref results, false, eventList);
			commands.Finish();

			sw.Stop();

			Console.WriteLine(sw.ElapsedMilliseconds / 1000.0);

			foreach (var r in results) {
				Console.WriteLine(r.count);
			}
		}

		public static string GetQueenKernelSource()
		{
			var assembly = Assembly.GetExecutingAssembly();
			var resourceName = "NQueens.queen_kernel.c";

			using (var stream = assembly.GetManifestResourceStream (resourceName))
			using (var reader = new StreamReader(stream)) 
			{
				return reader.ReadToEnd();
			}
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