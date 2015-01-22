using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly:Addin (
	"Haxe_Binding", 
	Namespace = "Haxe_Binding",
	Version = "1.0"
)]

[assembly:AddinName ("Haxe Binding")]
[assembly:AddinCategory ("Haxe Binding")]
[assembly:AddinDescription ("Haxe language binding")]
[assembly:AddinAuthor ("Joel Lord")]

[assembly:AddinDependency("::MonoDevelop.Core", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency ("::MonoDevelop.Ide", MonoDevelop.BuildInfo.Version)]
[assembly:AddinDependency("::MonoDevelop.SourceEditor2", MonoDevelop.BuildInfo.Version)]