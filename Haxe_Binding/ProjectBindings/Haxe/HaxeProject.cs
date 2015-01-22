using System;
using System.Xml;
using System.Collections.Generic;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;
using System.Diagnostics;

namespace Haxe_Binding
{
	[DataInclude(typeof(HaxeProjectConfiguration))]
	public sealed class HaxeProject:Project
	{
		private const string ADDITIONAL_ARGS = "AdditionalArgs";
		private const string HXML_BUILD_FILE = "HXMLBuildFile";

		[ItemProperty("AdditionalArgs", DefaultValue="")]
		string additionalArgs = string.Empty;

		public string AdditionalArgs {
			get { return additionalArgs; }
			set { additionalArgs = value; }
		}

		[ItemProperty("HXMLBuildFile", DefaultValue="")]
		string hxmlBuildFile = string.Empty;

		public string HXMLBuildFile {
			get { return hxmlBuildFile; }
			set { hxmlBuildFile = value; }
		}

		public HaxeProject():base()
		{
		}

		public HaxeProject (ProjectCreateInformation info, XmlElement projectOptions):base()
		{
			if (projectOptions.Attributes [ADDITIONAL_ARGS] != null) { 
                AdditionalArgs = ProjectHelpers.GetAttributeFromOptions (info, projectOptions, ADDITIONAL_ARGS);
			}

			if (projectOptions.Attributes [HXML_BUILD_FILE] != null) {
				HXMLBuildFile = ProjectHelpers.GetAttributeFromOptions (info, projectOptions, HXML_BUILD_FILE);
			}

			HaxeProjectConfiguration config;

			config = (HaxeProjectConfiguration)CreateConfiguration ("Debug");
			config.DebugMode = true;
			Configurations.Add (config);

			config = (HaxeProjectConfiguration)CreateConfiguration ("Release");
			config.DebugMode = false;
			Configurations.Add (config);
		}

		public override SolutionItemConfiguration CreateConfiguration (string name)
		{
			var conf = new HaxeProjectConfiguration ();
			conf.Name = name;
			return conf;
		}

		protected override BuildResult DoBuild (MonoDevelop.Core.IProgressMonitor monitor, ConfigurationSelector configuration)
		{
			HaxeProjectConfiguration haxeConfiguration = (HaxeProjectConfiguration)GetConfiguration (configuration);
            return HaxeCompilation.Compile (monitor, haxeConfiguration, this);
		}

        protected override void DoExecute (MonoDevelop.Core.IProgressMonitor monitor, ExecutionContext context, ConfigurationSelector configuration)
        {
            HaxeProjectConfiguration haxeConfiguration = (HaxeProjectConfiguration)GetConfiguration (configuration);
            HaxeCompilation.Run (monitor, context, haxeConfiguration, this);
        }

		public override IEnumerable<string> GetProjectTypes ()
		{
			yield return "Haxe";
		}

        public override bool IsCompileable (string fileName)
        {
            return true;
        }

        protected override bool OnGetCanExecute (ExecutionContext context, ConfigurationSelector configuration)
        {
            HaxeProjectConfiguration haxeConfiguration = (HaxeProjectConfiguration)GetConfiguration (configuration);
            return HaxeCompilation.CanRun (context, haxeConfiguration, this);
        }

		public override string[] SupportedLanguages {
			get {
				return new string[] { "", "Haxe", "HXML" };
			}
		}
	}
}

