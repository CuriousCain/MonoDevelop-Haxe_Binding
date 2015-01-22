using System;
using MonoDevelop.Projects;
using System.Xml;

namespace Haxe_Binding
{
    public class OpenFLProjectBinding:IProjectBinding
    {
        public Project CreateProject (ProjectCreateInformation info, XmlElement projectOptions)
        {
            return new OpenFLProject (info, projectOptions);
        }

        public Project CreateSingleFileProject (string sourceFile)
        {
            return null;
        }

        public bool CanCreateSingleFileProject (string sourceFile)
        {
            return false;
        }

        public string Name {
            get {
                return "OpenFL";
            }
        }
    }
}

