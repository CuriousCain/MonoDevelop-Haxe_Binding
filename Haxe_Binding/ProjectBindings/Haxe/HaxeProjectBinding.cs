using System;
using System.Xml;
using MonoDevelop.Projects;

namespace Haxe_Binding
{
	public class HaxeProjectBinding:IProjectBinding
	{
		public Project CreateProject (ProjectCreateInformation info, XmlElement projectOptions)
		{
			return new HaxeProject (info, projectOptions);
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
				return "Haxe";
			}
		}
	}
}

