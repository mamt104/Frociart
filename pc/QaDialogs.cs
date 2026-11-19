// QA adapter only. Never compiled into the application. Confirms only dialogs
// owned by the isolated test process; it does not enumerate desktop windows.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using Frociart;
internal static class QaDialogs {
 static Timer timer;
 internal static void Start(){if(timer!=null)return;timer=new Timer{Interval=30};timer.Tick+=delegate{
  foreach(var dialog in Application.OpenForms.Cast<Form>().OfType<FlowMessageDialog>().ToArray()){
   if(!dialog.Visible)continue;var buttons=All(dialog).OfType<Button>().Where(x=>x.Visible&&x.Enabled).ToArray();var choice=buttons.FirstOrDefault(x=>x.DialogResult==DialogResult.Yes)??buttons.FirstOrDefault(x=>x.DialogResult==DialogResult.OK);if(choice!=null)choice.PerformClick();
  }
 };timer.Start();}
 static IEnumerable<Control> All(Control c){yield return c;foreach(Control child in c.Controls)foreach(var nested in All(child))yield return nested;}
}
