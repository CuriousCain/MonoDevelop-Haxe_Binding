using System;
using MonoDevelop.Projects;
using MonoDevelop.Core;

namespace Haxe_Binding
{
	public class HaxeLanguageBinding:ILanguageBinding
	{
		public bool IsSourceCodeFile (FilePath fileName)
		{
			return fileName.Extension == "hx";
		}

		public FilePath GetFileName (FilePath fileNameWithoutExtension)
		{
			return fileNameWithoutExtension + ".hx";
		}

		public string Language {
			get {
				return "Haxe";
			}
		}

		public string SingleLineCommentTag {
			get {
				return "//";
			}
		}

		public string BlockCommentStartTag {
			get {
				return "/*";
			}
		}

		public string BlockCommentEndTag {
			get {
				return "*/";
			}
		}
	}
}

