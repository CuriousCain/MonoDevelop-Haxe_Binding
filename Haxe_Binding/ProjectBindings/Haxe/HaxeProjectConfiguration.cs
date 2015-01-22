using System;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;

namespace Haxe_Binding
{
	public class HaxeProjectConfiguration:ProjectConfiguration
	{
		[ItemProperty("AdditionalArgs", DefaultValue="")]
		string additionalArgs = string.Empty;

		public string AdditionalArgs {
			get { return additionalArgs; }
			set { additionalArgs = value; }
		}

		public override void CopyFrom (ItemConfiguration conf)
		{
			base.CopyFrom (conf);

			HaxeProjectConfiguration configuration = (HaxeProjectConfiguration)conf;
			additionalArgs = configuration.additionalArgs;
		}
	}
}

