using System;
using MonoDevelop.Projects;
using MonoDevelop.Core;
using MonoDevelop.Core.Execution;
using System.IO;
using System.Diagnostics;
using MonoDevelop.Core.ProgressMonitoring;
using Haxe_Binding;

namespace Haxe_Binding
{
    public static class OpenFLCompilation
    {
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

            int exitCode = CompilationHelper.RunCompilation ("haxelib", command_string, project.BaseDirectory, monitor, ref error);

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

        public static void Run(IProgressMonitor monitor, ExecutionContext context, OpenFLProjectConfiguration config, OpenFLProject project)
        {
            ExecutionCommand command = CreateExecutionCommand (project, config);

            if (command is NativeExecutionCommand) {
                IConsole console = (config.ExternalConsole) ? context.ExternalConsoleFactory.CreateConsole (false) : context.ConsoleFactory.CreateConsole (false);

                var opMonitor = new AggregatedOperationMonitor (monitor);

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
            const string haxelib_cmd = "haxelib";
            string args = string.Format ("run openfl run {0} {1}", project.XMLBuildFile, config.Platform.ToLower());

            if(config.DebugMode)
                args += " -debug";

            if (project.AdditionalArgs != "")
                args += string.Format (" {0}", project.AdditionalArgs);

            if (config.AdditionalArgs != "")
                args += string.Format (" {0}", config.AdditionalArgs);

            var command = new NativeExecutionCommand (haxelib_cmd);
            command.Arguments = args;
            command.WorkingDirectory = project.BaseDirectory.FullPath;

            return command;
        }

        public static bool CanRun(ExecutionContext context, OpenFLProjectConfiguration config, OpenFLProject project)
        {
            ExecutionCommand command = CreateExecutionCommand (project, config);

            return command != null && context.ExecutionHandler.CanExecute (command); 
        }

        public static void Clean(OpenFLProjectConfiguration config, OpenFLProject project)
        {
            var info = new ProcessStartInfo ();
            info.FileName = "haxelib";
            info.Arguments = string.Format("run openfl clean {0} {1} {2} {3}", project.XMLBuildFile, config.Platform.ToLower(),
                project.AdditionalArgs, config.AdditionalArgs);
            
            info.UseShellExecute = false;
            info.RedirectStandardError = true;
            info.RedirectStandardOutput = true;
            info.WorkingDirectory = project.BaseDirectory;
            info.CreateNoWindow = true;

            using (Process process = Process.Start(info)) {
                process.WaitForExit ();
            }
        }
    }
}

