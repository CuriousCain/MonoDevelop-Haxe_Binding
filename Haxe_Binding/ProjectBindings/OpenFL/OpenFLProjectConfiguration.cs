using System;
using MonoDevelop.Projects;
using MonoDevelop.Core.Serialization;

namespace Haxe_Binding
{
    public class OpenFLProjectConfiguration:ProjectConfiguration
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

            OpenFLProjectConfiguration configuration = (OpenFLProjectConfiguration)conf;
            additionalArgs = configuration.additionalArgs;
        }
    }
}

