using System;
using MonoDevelop.Projects;
using MonoDevelop.Core;

namespace Haxe_Binding
{
	public class HxmlLanguageBinding:ILanguageBinding
	{
		public bool IsSourceCodeFile (FilePath fileName)
		{
			return fileName.Extension == "hxml";
		}

		public FilePath GetFileName (FilePath fileNameWithoutExtension)
		{
			return fileNameWithoutExtension + ".hxml";
		}

		public string Language {
			get {
				return "Hxml";
			}
		}

		public string SingleLineCommentTag {
			get {
				return "#";
			}
		}

		public string BlockCommentStartTag {
			get {
				return null;
			}
		}

		public string BlockCommentEndTag {
			get {
				return null;
			}
		}
	}
}

