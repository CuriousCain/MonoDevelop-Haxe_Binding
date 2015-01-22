using System;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using MonoDevelop.SourceEditor;
using System.Xml;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

namespace Haxe_Binding
{
    [DataInclude(typeof(OpenFLProjectConfiguration))]
    public class OpenFLProject:Project
    {
        private const string ADDITIONAL_ARGS = "AdditionalArgs";
        private const string XML_BUILD_FILE = "XmlBuildFile";
        private string[] targets = {"Android", "Blackberry", "iOS", "HTML5", "Flash",
            "Windows", "Linux", "Mac", "Neko", "Tizen"
        };

        [ItemProperty("AdditionalArgs", DefaultValue="")]
        string additionalArgs = string.Empty;

        public string AdditionalArgs {
            get { return additionalArgs; }
            set { additionalArgs = value; }
        }

        [ItemProperty("XmlBuildFile", DefaultValue="")]
        string xmlBuildFile = string.Empty;

        public string XMLBuildFile {
            get { return xmlBuildFile; }
            set { xmlBuildFile = value; }
        }

        public OpenFLProject():base ()
        {
        }

        public OpenFLProject(ProjectCreateInformation info, XmlElement projectOptions):base()
        {
            if (projectOptions.Attributes[ADDITIONAL_ARGS] != null) {
                AdditionalArgs = ProjectHelpers.GetAttributeFromOptions (info, projectOptions, ADDITIONAL_ARGS);
            }

            if(projectOptions.Attributes[XML_BUILD_FILE] != null) {
                XMLBuildFile = ProjectHelpers.GetAttributeFromOptions (info, projectOptions, XML_BUILD_FILE);
            }

            OpenFLProjectConfiguration config;

            foreach(string target in targets) {
                config = (OpenFLProjectConfiguration)CreateConfiguration ("Debug");
                config.DebugMode = true;
                config.Platform = target;

                if(target == "iOS")
                    config.AdditionalArgs = "-simulator";

                Configurations.Add (config);
            }

            foreach(string target in targets) {
                config = (OpenFLProjectConfiguration)CreateConfiguration ("Release");
                config.DebugMode = false;
                config.Platform = target;

                if(target == "iOS")
                    config.AdditionalArgs = "-simulator";

                Configurations.Add (config);
            }
        }

        public override SolutionItemConfiguration CreateConfiguration (string name)
        {
            var conf = new OpenFLProjectConfiguration ();
            conf.Name = name;
            return conf;
        }

        protected override BuildResult DoBuild (MonoDevelop.Core.IProgressMonitor monitor, ConfigurationSelector configuration)
        {
            OpenFLProjectConfiguration openflConfig = (OpenFLProjectConfiguration)GetConfiguration (configuration);
            return OpenFLCompilation.Compile (monitor, openflConfig, this);
        }

        protected override void DoClean (MonoDevelop.Core.IProgressMonitor monitor, ConfigurationSelector configuration)
        {
            OpenFLProjectConfiguration openflConfig = (OpenFLProjectConfiguration)GetConfiguration (configuration);
            OpenFLCompilation.Clean (monitor, openflConfig, this);
        }

        protected override void DoExecute (MonoDevelop.Core.IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
        {
            OpenFLProjectConfiguration openflConfig = (OpenFLProjectConfiguration)GetConfiguration (configuration);
            OpenFLCompilation.Run (monitor, context, openflConfig, this);
        }

        public override IEnumerable<string> GetProjectTypes ()
        {
            yield return "OpenFL";
        }

        public override bool IsCompileable (string fileName)
        {
            return true;
        }

        protected override bool OnGetCanExecute (ExecutionContext context, ConfigurationSelector configuration)
        {
            OpenFLProjectConfiguration openflConfig = (OpenFLProjectConfiguration)GetConfiguration (configuration);
            return OpenFLCompilation.CanRun (context, openflConfig, this);
        }

        public override string[] SupportedLanguages {
            get {
                return new string[] { "", "Haxe", "XML" };
            }
        }
    }
}

