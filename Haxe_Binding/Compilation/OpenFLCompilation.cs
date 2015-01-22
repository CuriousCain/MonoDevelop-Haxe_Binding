using System;
using System.Text.RegularExpressions;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.Text;
using MonoDevelop.Core.Execution;
using MonoDevelop.Components.Commands;
using System.IO;
using System.Diagnostics;
using NUnit.Framework.Constraints;
using MonoDevelop.Core.ProgressMonitoring;
using System.Configuration;

namespace Haxe_Binding
{
    public static class OpenFLCompilation
    {
        // From JGranick's original plugin
        private static Regex errorFull = new Regex (@"^(?<file>.+)\((?<line>\d+)\):\s(col:\s)?(?<column>\d*)\s?(?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:11: character 7 : Unterminated string
        private static Regex errorFileChar = new Regex (@"^(?<file>.+):(?<line>\d+):\s(character\s)(?<column>\d*)\s:\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:11: characters 0-5 : Unexpected class
        private static Regex errorFileChars = new Regex (@"^(?<file>.+):(?<line>\d+):\s(characters\s)(?<column>\d+)-(\d+)\s:\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:10: lines 10-28 : Class not found : Sprite
        private static Regex errorFile = new Regex (@"^(?<file>.+):(?<line>\d+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        private static Regex errorCmdLine = new Regex (@"^command line: (?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static Regex errorSimple = new Regex (@"^(?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        private static Regex errorIgnore = new Regex (@"^(Updated|Recompile|Reason|Files changed):.*", RegexOptions.Compiled);

        public static BuildResult Compile(IProgressMonitor monitor, OpenFLProjectConfiguration config, OpenFLProject project) 
        {
            string command_string = string.Format ("run openfl build {0} {1}", project.XMLBuildFile, config.Platform.ToLower ());
            string error = "";

            if(config.DebugMode)
                command_string += " -debug";

            if (project.AdditionalArgs != "")
                command_string += string.Format (" {0}", project.AdditionalArgs);

            if (config.AdditionalArgs != "")
                command_string += string.Format (" {0}", config.AdditionalArgs);

            int exitCode = RunCompilation ("haxelib", command_string, project.BaseDirectory, monitor, ref error);

            BuildResult result = GetCompilationResult (project, error);

            if (result.CompilerOutput.Trim ().Length != 0)
                monitor.Log.WriteLine (result.CompilerOutput);

            if (result.ErrorCount == 0 && exitCode != 0) {
                string errorMessage = File.ReadAllText (error);

                if(!string.IsNullOrEmpty(errorMessage)) {
                    result.AddError (errorMessage);
                } else {
                    result.AddError ("The project failed to build. See the 'Build Output' pad for more information");
                }
            }

            FileService.DeleteFile (error);
            return result;
        }

        public static void Run(IProgressMonitor monitor, ExecutionContext context, OpenFLProjectConfiguration config, OpenFLProject project)
        {
            ExecutionCommand command = CreateExecutionCommand (project, config);

            if (command is NativeExecutionCommand) {
                IConsole console = (config.ExternalConsole) ? context.ExternalConsoleFactory.CreateConsole (false) : context.ConsoleFactory.CreateConsole (false);

                AggregatedOperationMonitor opMonitor = new AggregatedOperationMonitor (monitor);

                try {
                    if(!context.ExecutionHandler.CanExecute(command)) {
                        monitor.ReportError(string.Format("Could not execute {0}", command), null);
                        return;
                    }

                    IProcessAsyncOperation operation = context.ExecutionHandler.Execute(command, console);
                    opMonitor.AddOperation(operation);
                    operation.WaitForCompleted();

                    monitor.Log.WriteLine("Execution exited. Exit code: {0}", operation.ExitCode);
                } catch (Exception) {
                    monitor.ReportError (string.Format ("Error during execution of '{0}'", command), null);
                } finally {
                    opMonitor.Dispose ();
                    console.Dispose ();
                }
            } else {
                context.ExecutionHandler.Execute (command, context.ConsoleFactory.CreateConsole(false));
            }
        }

        static NativeExecutionCommand CreateExecutionCommand (OpenFLProject project, OpenFLProjectConfiguration config)
        {
            string cmd = "haxelib";
            string args = string.Format ("run openfl run {0} {1}", project.XMLBuildFile, config.Platform.ToLower());

            if(config.DebugMode)
                args += " -debug";

            if (project.AdditionalArgs != "")
                args += string.Format (" {0}", project.AdditionalArgs);

            if (config.AdditionalArgs != "")
                args += string.Format (" {0}", config.AdditionalArgs);

            NativeExecutionCommand command = new NativeExecutionCommand (cmd);
            command.Arguments = args;
            command.WorkingDirectory = project.BaseDirectory.FullPath;

            return command;
        }

        public static bool CanRun(ExecutionContext context, OpenFLProjectConfiguration config, OpenFLProject project)
        {
            ExecutionCommand command = CreateExecutionCommand (project, config);
            if (command == null) {
                return false;
            } 

            return context.ExecutionHandler.CanExecute (command);
        }

        static int RunCompilation(string command, string args, string workingDirectory, IProgressMonitor monitor, ref string error) //TODO: Extract?
        {
            int exitCode = 0;
            error = Path.GetTempFileName ();
            StreamWriter writer = new StreamWriter (error);

            ProcessStartInfo processInfo = new ProcessStartInfo (command, args);
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;
            processInfo.WorkingDirectory = workingDirectory;

            using(ProcessWrapper procWrapper = Runtime.ProcessService.StartProcess(processInfo, monitor.Log, writer, null)) {
                procWrapper.WaitForOutput ();
                exitCode = procWrapper.ExitCode;
            }
            writer.Close ();

            return exitCode;
        }

        public static void Clean(IProgressMonitor monitor, OpenFLProjectConfiguration config, OpenFLProject project)
        {
            ProcessStartInfo info = new ProcessStartInfo ();
            info.FileName = "haxelib";
            info.Arguments = string.Format("run openfl clean {0} {1}", project.XMLBuildFile, config.Platform.ToLower());
            info.UseShellExecute = false;
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.WorkingDirectory = project.BaseDirectory;
            info.CreateNoWindow = true;

            using (Process process = Process.Start(info)) {
                process.WaitForExit ();
            }
        }

        static BuildResult GetCompilationResult(OpenFLProject project, string error) //TODO: Maybe extract to a helper class - haxecompilation has this same function, too
        {
            BuildResult result = new BuildResult ();
            StringBuilder output = new StringBuilder();

            CompilationOutput (project, result, output, error);

            result.CompilerOutput = output.ToString ();

            return result;
        }

        static void CompilationOutput(OpenFLProject project, BuildResult result, StringBuilder output, string filename) //TODO: Extract to helper class as above?
        {
            StreamReader reader = File.OpenText (filename);

            string line;
            while((line = reader.ReadLine()) != null) {
                output.AppendLine (line);

                line = line.Trim ();
                if(line.Length == 0 || line.StartsWith("\t"))
                    continue;

                BuildError error = CreateError (project, line);
                if (error != null)
                    result.Append (error);
            }

            reader.Close ();
        }

        static BuildError CreateError(OpenFLProject project, string textLine) //TODO: Extract?
        {
            Match match = errorIgnore.Match (textLine);
            if (match.Success)
                return null;

            //TODO: Check errors from an array instead of individually

            match = errorFull.Match (textLine);
            if (!match.Success)
                match = errorCmdLine.Match (textLine);
            if (!match.Success)
                match = errorFileChar.Match (textLine);
            if (!match.Success)
                match = errorFileChars.Match (textLine);
            if (!match.Success)
                match = errorFile.Match (textLine);

            if (!match.Success)
                match = errorSimple.Match (textLine);
            if (!match.Success)
                return null;

            int errorLine;
            int errorColumn;

            BuildError error = new BuildError ();
            error.FileName = match.Result("${file}") ?? "";
            error.IsWarning = match.Result("${level}").ToLower() == "warning";
            error.ErrorText = match.Result ("${message}");

            if(error.FileName == "${file}") {
                error.FileName = "";
            } else {
                if(!File.Exists(error.FileName)) {
                    error.FileName = File.Exists (Path.GetFullPath (error.FileName)) ? Path.GetFullPath (error.FileName) : Path.Combine (project.BaseDirectory, error.FileName);
                }
            }

            if (int.TryParse(match.Result("${line}"), out errorLine)) {
                error.Line = errorLine;
            } else {
                error.Line = 0;
            }

            if (int.TryParse(match.Result("${column}"), out errorColumn)) {
                error.Column = errorColumn + 1;
            } else {
                error.Column = -1;
            }

            return error;
        }
    }
}

