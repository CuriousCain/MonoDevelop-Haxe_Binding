using MonoDevelop.Projects;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using MonoDevelop.Core;
using System.Diagnostics;
using MonoDevelop.Core.Execution;

namespace Haxe_Binding
{
    public static class CompilationHelper
    {
        static readonly Regex errorFull = new Regex (@"^(?<file>.+)\((?<line>\d+)\):\s(col:\s)?(?<column>\d*)\s?(?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:11: character 7 : Unterminated string
        static readonly Regex errorFileChar = new Regex (@"^(?<file>.+):(?<line>\d+):\s(character\s)(?<column>\d*)\s:\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:11: characters 0-5 : Unexpected class
        static readonly Regex errorFileChars = new Regex (@"^(?<file>.+):(?<line>\d+):\s(characters\s)(?<column>\d+)-(\d+)\s:\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        // example: test.hx:10: lines 10-28 : Class not found : Sprite
        static readonly Regex errorFile = new Regex (@"^(?<file>.+):(?<line>\d+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        static readonly Regex errorCmdLine = new Regex (@"^command line: (?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        static readonly Regex errorSimple = new Regex (@"^(?<level>\w+):\s(?<message>.*)\.?$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        static readonly Regex errorIgnore = new Regex (@"^(Updated|Recompile|Reason|Files changed):.*", RegexOptions.Compiled);

        public static int RunCompilation(string command, string args, string workingDirectory, IProgressMonitor monitor, ref string error)
        {
            int exitCode;
            error = Path.GetTempFileName ();
            var writer = new StreamWriter (error);

            var processInfo = new ProcessStartInfo (command, args);
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

        public static void CompilationOutput(Project project, BuildResult result, StringBuilder output, string filename)
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

        public static BuildResult GetCompilationResult(Project project, string error)
        {
            var result = new BuildResult ();
            var output = new StringBuilder();

            CompilationHelper.CompilationOutput (project, result, output, error);

            result.CompilerOutput = output.ToString ();

            return result;
        }

        static BuildError CreateError(Project project, string textLine)
        {
            Match match = errorIgnore.Match (textLine);
            if (match.Success)
                return null;

            //TODO: Check errors from an array instead of individually?

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

            var error = new BuildError ();
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

            error.Line = int.TryParse (match.Result ("${line}"), out errorLine) ? errorLine : 0;

            if (int.TryParse(match.Result("${column}"), out errorColumn)) {
                error.Column = errorColumn + 1;
            } else {
                error.Column = -1;
            }

            return error;
        }
    }
}

