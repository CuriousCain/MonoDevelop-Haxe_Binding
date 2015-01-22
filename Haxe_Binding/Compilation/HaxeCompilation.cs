using System;
using System.Linq;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using System.IO;
using MonoDevelop.Core.Execution;
using System.Collections.Generic;
using MonoDevelop.Core.ProgressMonitoring;

namespace Haxe_Binding
{
	public static class HaxeCompilation
	{
        static string[] haxeBuildTargets = { "-js", "-swf", "-neko", "-cpp", "-as3", "-php", "-cs", "-java" };
        const string haxeExecute = "haxe";

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

            int exitCode = CompilationHelper.RunCompilation (haxeExecute, args, project.BaseDirectory, monitor, ref error);

            BuildResult result = CompilationHelper.GetCompilationResult (project, error);

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

                var opMonitor = new AggregatedOperationMonitor (monitor);

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

            var targets = new List<string> ();
            var targetOutputs = new List<string> ();
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

                if(!File.Exists(Path.GetFullPath(output)))
                    output = Path.Combine (project.BaseDirectory, output);

                string exe;
                string args = "";

                if(target == "cpp") {
                    exe = output;
                } else {
                    exe = "neko";
                    args = "\"" + output + "\"";
                }

                var command = new NativeExecutionCommand (exe);
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
                        cmd = "xdg-open"; //TODO: Assign flash debugger path?
                        break;
                    case PlatformID.MacOSX:
                        cmd = "open \"" + output + "\"";
                        break;
                    default:
                        cmd = output;
                        break;
                }

                var command = new NativeExecutionCommand ();
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

            return command != null && context.ExecutionHandler.CanExecute (command);
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