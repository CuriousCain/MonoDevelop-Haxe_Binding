using System;
using Gtk;
using MonoDevelop.Ide.Gui.Dialogs;
using MonoDevelop.Core;

namespace Haxe_Binding
{
	public class HaxeOptionsPanel:OptionsPanel
	{
		HaxeOptionsWidget haxeOptionsWidget;

		public override Widget CreatePanelWidget ()
		{
			return haxeOptionsWidget = new HaxeOptionsWidget ();
		}

		public override void ApplyChanges ()
		{
			haxeOptionsWidget.Store ();
		}
	}

	[System.ComponentModel.Category("Haxe_Binding")]
	[System.ComponentModel.ToolboxItem(true)]
	public partial class HaxeOptionsWidget : Gtk.Bin
	{
		public HaxeOptionsWidget()
		{
			this.Build();
		}

		public bool Store()
		{
			return true;
		}
	}
}