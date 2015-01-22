using System;
using MonoDevelop.Projects;
using System.Xml;

namespace Haxe_Binding
{
    public static class ProjectHelpers
    {
        public static string GetAttributeFromOptions(ProjectCreateInformation info, XmlElement projectOptions, string attribute)
        {
            string value = projectOptions.Attributes [attribute].InnerText;
            value = value.Replace ("${ProjectName}", info.ProjectName);
            return value;
        }
    }
}

