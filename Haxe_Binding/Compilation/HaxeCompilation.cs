using System;
using System.Linq;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.IO;
using System.Diagnostics;
using MonoDevelop.Core.Execution;
using Gtk;
using System.Text;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using GLib;
using System.Collections.Generic;
using MonoDevelop.Core.ProgressMonitoring;
using System.Runtime.Remoting.Contexts;
using System.Collections;

namespace Haxe_Binding
{
	public static class HaxeCompilation
	{
        private static string[] haxeBuildTargets = { "-js", "-swf", "-neko", "-cpp", "-as3", "-php", "-cs", "-java" };
        private const string haxeExecute = "haxe";

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

        public static BuildResult Compile(IProgressMonitor monitor, HaxeProjectConfiguration config, HaxeProject project)
        {
            string[] hxArgs = GetHxmlArgsFromProject (project);
            bool createNext = false;
            string error = "";

            foreach(string hxArg in hxArgs) {
                if(createNext) {
                    if(!hxArg.StartsWith("-")) {
                        string buildPath = Path.GetFullPath (Path.Combine(project.BaseDirectory, "bin"));

                        if(!Directory.Exists(buildPath)) {
                            Directory.CreateDirectory (buildPath);
                        }
                    }
                    createNext = false;
                }
                    
                createNext |= haxeBuildTargets.Contains (hxArg);
            }

            string args = String.Join (" ", hxArgs);

            if (config.DebugMode)
                args += " -debug";

            if (project.AdditionalArgs != "")
                args += " " + project.AdditionalArgs;

            if (config.AdditionalArgs != "")
                args += " " + config.AdditionalArgs;

            int exitCode = RunCompilation (haxeExecute, args, project.BaseDirectory, monitor, ref error);

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

        public static void Run (IProgressMonitor monitor, ExecutionContext context, HaxeProjectConfiguration config, HaxeProject project)
        {
            ExecutionCommand command = CreateExecutionCommand (project, config);

            if (command is NativeExecutionCommand) {
                IConsole console = (config.ExternalConsole) ? context.ExternalConsoleFactory.CreateConsole (false) : context.ConsoleFactory.CreateConsole (false);

                AggregatedOperationMonitor opMonitor = new AggregatedOperationMonitor (monitor);

                try {
                    if(!context.ExecutionHandler.CanExecute(command)) {
                        monitor.ReportError(string.Format("Could not execute '{0}'", command), null);
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

        static NativeExecutionCommand CreateExecutionCommand (HaxeProject project, HaxeProjectConfiguration config)
        {
            string[] hxArgs = GetHxmlArgsFromProject (project);

            List<string> targets = new List<string> ();
            List<string> targetOutputs = new List<string> ();
            string mainClass = "";

            bool addNext = false;
            bool nextIsMainClass = false;

            foreach(string hxArg in hxArgs) {
                if (addNext) {
                    if (!hxArg.StartsWith("-")) {
                        if (nextIsMainClass) {
                            mainClass = hxArg;
                            nextIsMainClass = false;
                        } else {
                            targetOutputs.Add (hxArg);
                        }
                    } else {
                        if (!nextIsMainClass) {
                            targets.RemoveAt (targets.Count - 1);
                        }
                    }
                }

                addNext = true;

                if (haxeBuildTargets.Contains(hxArg)) {
                    targets.Add (hxArg.Substring (1));
                } else if (hxArg == "-main") {
                    nextIsMainClass = true;
                } else {
                    addNext = false;
                }
            }

            string target = targets [0];
            string output = targetOutputs [0];
            string cmd = "";

            if(target == "cpp" || target == "neko") {
                if(target == "cpp") {
                    output = Path.Combine (output, mainClass);
                    if(config.DebugMode) {
                        output += "-debug";
                    }
                }

                if(!File.Exists(Path.GetFullPath(output))) {
                    output = Path.Combine (project.BaseDirectory, output);
                }

                string exe = "";
                string args = "";

                if(target == "cpp") {
                    exe = output;
                } else {
                    exe = "neko";
                    args = "\"" + output + "\"";
                }

                NativeExecutionCommand command = new NativeExecutionCommand (exe);
                command.Arguments = args;
                command.WorkingDirectory = Path.GetDirectoryName (output);

                if(config.DebugMode) {
                    command.EnvironmentVariables.Add ("HXCPP_DEBUG_HOST", "gdb");
                    command.EnvironmentVariables.Add ("HXCPP_DEBUG", "1");
                }

                return command;
            }

            if (target == "swf" || target == "js") {
                if (!File.Exists(Path.GetFullPath (output))) {
                    output = Path.Combine (project.BaseDirectory, output);
                }

                if(target == "js") {
                    output = Path.Combine (Path.GetDirectoryName (output), "index.html"); //TODO: Implement js project and add html to template
                }

                //TODO: Does this work on windows?
                switch(Environment.OSVersion.Platform) 
                {
                    case PlatformID.Unix:
                        cmd = "xdg-open"; //TODO: Assign flash debugger path
                        break;
                    case PlatformID.MacOSX:
                        cmd = "open \"" + output + "\"";
                        break;
                    default:
                        cmd = output;
                        break;
                }

                NativeExecutionCommand command = new NativeExecutionCommand (); //Use NativeExecutionCommand as a fix for file not opening
                command.Command = cmd;
                command.Arguments = output;
                return command;
            }

            return null;
        }

        public static bool CanRun(ExecutionContext context, HaxeProjectConfiguration config, HaxeProject project)
        {
            //TODO: Cache this for optimization (From Joshua Granick)
            //Idea: Possible add to a settings file? Or store in project settings

            ExecutionCommand command = CreateExecutionCommand (project, config);
            if(command == null) {
                return false;
            } else {
                return context.ExecutionHandler.CanExecute (command);
            }
        }

        static int RunCompilation(string command, string args, string workingDirectory, IProgressMonitor monitor, ref string error)
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

        static BuildResult GetCompilationResult(HaxeProject project, string error)
        {
            BuildResult result = new BuildResult ();
            StringBuilder output = new StringBuilder();

            CompilationOutput (project, result, output, error);

            result.CompilerOutput = output.ToString ();

            return result;
        }

        static void CompilationOutput(HaxeProject project, BuildResult result, StringBuilder output, string filename)
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

        static BuildError CreateError(HaxeProject project, string textLine)
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

        static string[] GetHxmlArgsFromProject(HaxeProject project)
        {
            var tempHxmlPath = Path.GetFullPath (project.HXMLBuildFile);
            if(!File.Exists(tempHxmlPath)) {
                tempHxmlPath = Path.Combine (project.BaseDirectory, project.HXMLBuildFile);
            }
            string hxml = File.ReadAllText (tempHxmlPath).Replace(Environment.NewLine, " ");
            return hxml.Split(' ');
        }
	}
}