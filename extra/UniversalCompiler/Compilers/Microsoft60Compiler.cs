﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal class Microsoft60Compiler : Compiler
{
	public override string Name => "Microsoft C# 6.0";
	public override bool NeedsPdb2MdbConversion => true;

	public Microsoft60Compiler(Logger logger, string directory)
		: base(logger, Path.Combine(directory, "csc.exe"), Path.Combine(directory, "pdb2mdb.exe")) { }

	public static bool IsAvailable(string directory) => File.Exists(Path.Combine(directory, "csc.exe")) &&
														File.Exists(Path.Combine(directory, "pdb2mdb.exe"));

	protected override Process CreateCompilerProcess(Platform platform, string monoProfile, string unityEditorDataDir, string responseFile)
	{
		var systemDllPath = GetMonoDllPath(unityEditorDataDir, monoProfile, "System.dll");
		var systemCoreDllPath = GetMonoDllPath(unityEditorDataDir, monoProfile, "System.Core.dll");
		var systemXmlDllPath = GetMonoDllPath(unityEditorDataDir, monoProfile, "System.Xml.dll");
		var mscorlibDllPath = GetMonoDllPath(unityEditorDataDir, monoProfile, "mscorlib.dll");

		string processArguments = "-nostdlib+ -noconfig -nologo "
								  + $"-r:\"{mscorlibDllPath}\" "
								  + $"-r:\"{systemDllPath}\" "
								  + $"-r:\"{systemCoreDllPath}\" "
								  + $"-r:\"{systemXmlDllPath}\" " + responseFile;

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, compilerPath, processArguments, unityEditorDataDir);
		return process;
	}

	public override void ConvertDebugSymbols(Platform platform, string libraryPath, string unityEditorDataDir)
	{
		outputLines.Clear();

		var process = new Process();
		process.StartInfo = CreateOSDependentStartInfo(platform, ProcessRuntime.CLR40, pbd2MdbPath, libraryPath, unityEditorDataDir);
		process.OutputDataReceived += (sender, e) => outputLines.Add(e.Data);

		logger?.Append($"Process: {process.StartInfo.FileName}");
		logger?.Append($"Arguments: {process.StartInfo.Arguments}");

		process.Start();
		process.BeginOutputReadLine();
		process.WaitForExit();
		logger?.Append($"Exit code: {process.ExitCode}");

		var pdbPath = Path.Combine("Temp", Path.GetFileNameWithoutExtension(libraryPath) + ".pdb");
		File.Delete(pdbPath);
	}

	public override void PrintCompilerOutputAndErrors()
	{
		// Microsoft's compiler writes all warnings and errors to the standard output channel,
		// so move them to the error channel

		errorLines.AddRange(outputLines);
		outputLines.Clear();

		base.PrintCompilerOutputAndErrors();
	}

	public override void PrintPdb2MdbOutputAndErrors()
	{
		var lines = (from line in outputLines
					 let trimmedLine = line?.Trim()
					 where string.IsNullOrEmpty(trimmedLine) == false
					 select trimmedLine).ToList();

		logger?.Append($"- pdb2mdb.exe output ({lines.Count} {(lines.Count == 1 ? "line" : "lines")}):");

		for (int i = 0; i < lines.Count; i++)
		{
			Console.Out.WriteLine(lines[i]);
			logger?.Append($"{i}: {lines[i]}");
		}
	}
}
