using System;using System.IO;
namespace Frociart {
    internal sealed partial class MainForm {
        FlowTemplateStore templateStore;
        internal FlowTemplateStore TemplateStore {get{return templateStore??(templateStore=new FlowTemplateStore(Path.Combine(Environment.GetEnvironmentVariable("FROCIART_DATA_DIR")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"),"code-templates")));}}
        internal void ShowCodeTemplates(bool live=false){CommitInspector();PauseRun();Canvas.CancelConnection();using(var library=new CodeTemplateLibrary(TemplateStore,DiagramIO.Clone(Project))){var result=library.ShowDialog(this);if(livePseudocode.SelectedTarget!=null&&livePseudocode.SelectedTarget.TemplateId!=null)try{var entry=TemplateStore.Load(livePseudocode.SelectedTarget.TemplateId);livePseudocode.SetTarget(entry.Definition==null?null:FlowSourceTarget.FromTemplate(entry.Id,entry.Definition));}catch(Exception error){if(!(error is IOException)&&!(error is UnauthorizedAccessException))throw;SetStatus("Modello non aggiornato: "+CodeTemplateUi.Problem(error));}if(result==System.Windows.Forms.DialogResult.OK&&library.SelectedTarget!=null){if(live){livePseudocode.SetTarget(library.SelectedTarget);SetStatus("Anteprima: "+library.SelectedTarget.Name);}else ShowSourceTarget(library.SelectedTarget);}}}
    }
}
