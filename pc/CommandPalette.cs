using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Frociart {
    internal static class FlowCommandSearch {
        internal const int MaxQuery=128;
        internal static string Fold(string value){var b=new StringBuilder();bool space=true;foreach(char c in (value??"").Normalize(NormalizationForm.FormD)){if(CharUnicodeInfo.GetUnicodeCategory(c)==UnicodeCategory.NonSpacingMark)continue;if(Char.IsLetterOrDigit(c)||c=='+'||c=='#'){b.Append(Char.ToLowerInvariant(c));space=false;}else if(!space){b.Append(' ');space=true;}}return b.ToString().Trim();}
        internal static int Rank(string query,string label,string path,string aliases){string wanted=Fold(query);if(wanted.Length==0)return 100;string title=Fold(label),place=Fold(path),words=title+" "+place+" "+Fold(aliases);int rank=title==wanted?0:title.StartsWith(wanted+" ",StringComparison.Ordinal)?1:10;foreach(string word in wanted.Split(' ')){if(words.IndexOf(word,StringComparison.Ordinal)<0)return -1;if((" "+title+" ").Contains(" "+word+" "))rank-=2;else if(title.IndexOf(word,StringComparison.Ordinal)>=0)rank--;else rank++;}return title==wanted?0:title.StartsWith(wanted+" ",StringComparison.Ordinal)?1:Math.Max(2,rank);}
        internal static string Aliases(string label){string s=Fold(label);var b=new StringBuilder();if(s.Contains("apri progetto"))b.Append(" importa carica flowgorithm fprg frociart ");if(s.Contains("esporta"))b.Append(" immagine pdf svg png condividi ");if(s.Contains("salva"))b.Append(" file progetto archivia ");if(s.Contains("inquadra"))b.Append(" tutto mostra centra adatta zoom ");if(s.Contains("riordina"))b.Append(" allinea disponi ordine spaziatura ");if(s.Contains("variabili e chiamate"))b.Append(" watch memoria valori stack ispeziona ");if(s.Contains("cerca nel progetto"))b.Append(" trova testo blocco variabile ricerca ");if(s.Contains("pseudocodice"))b.Append(" testo algoritmo importa leggi stampa ");if(s.Contains("modelli di codice"))b.Append(" template fpgt frotemplate ");if(s.Contains("ciclo guidato"))b.Append(" for per while mentre repeat ripeti until loop ");if(s.Contains("stile dei diagrammi"))b.Append(" font carattere dimensione colori aspetto ");if(s.Contains("comandi rapidi"))b.Append(" tastiera shortcut scorciatoie ");return b.ToString();}
    }
    internal sealed class FlowQuickCommand {
        internal readonly string Label,Path,Aliases;
        internal readonly Func<bool> Available;
        internal readonly Action Execute;
        internal bool Enabled {get{return Available==null||Available();}}
        internal FlowQuickCommand(string label,string path,Func<bool> available,Action execute,string searchText=null){Label=label;Path=path;Available=available;Execute=execute;Aliases=FlowCommandSearch.Aliases(label)+" "+searchText;}
        public override string ToString(){return Path+" · "+Label+(Enabled?"":" · Non disponibile adesso");}
    }
    internal sealed class CommandPaletteDialog:Form {
        internal readonly TextBox Query=Ui.TextBox();
        internal readonly ListBox Results=new ListBox();
        internal readonly Label Status=Ui.Label("",9,Ui.Muted);
        internal readonly FlowButton Open;
        private readonly IList<FlowQuickCommand> commands;
        private readonly Action refresh;
        private readonly Timer motion=new Timer{Interval=16};
        private DateTime motionStarted;private double closeOpacity=1;
        private bool closing,allowClose,chosen;
        private DialogResult closingResult;
        internal FlowQuickCommand Selected {get;private set;}
        internal CommandPaletteDialog(IList<FlowQuickCommand> commands,Action refresh,string initial){
            this.commands=commands;this.refresh=refresh;Name="CommandPaletteDialog";Text="Cerca un comando — Frociart";StartPosition=FormStartPosition.CenterParent;Size=new Size(700,530);MinimumSize=new Size(500,360);ShowInTaskbar=false;BackColor=Ui.Surface;Font=Ui.Font();
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=5};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int height in new[]{28,44,30})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
            var hint=Ui.Label("Trova una funzione nei menu · Ctrl+K",10,Ui.Ink,true);hint.Dock=DockStyle.Fill;layout.Controls.Add(hint,0,0);
            Query.Name="CommandQuery";Query.AccessibleName="Comando da cercare";Query.AccessibleDescription="Per esempio: riordina, variabili, Java, PDF";Query.MaxLength=FlowCommandSearch.MaxQuery;Query.Dock=DockStyle.Fill;Query.Margin=new Padding(0,2,0,8);layout.Controls.Add(Query,0,1);Status.Dock=DockStyle.Fill;Status.Name="CommandStatus";layout.Controls.Add(Status,0,2);
            Results.Name="CommandResults";Results.AccessibleName="Comandi trovati nei menu";Results.Dock=DockStyle.Fill;Results.BorderStyle=BorderStyle.None;Results.IntegralHeight=false;Results.DrawMode=DrawMode.OwnerDrawFixed;Results.ItemHeight=68;Results.DrawItem+=Draw;layout.Controls.Add(Results,0,3);
            var footer=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,10,0,0)};var close=Ui.Button("Chiudi",100,()=>{DialogResult=DialogResult.Cancel;});close.Dock=DockStyle.Left;footer.Controls.Add(close);Open=Ui.Button("Apri comando",150,Choose,true);Open.Name="CommandOpen";Open.Dock=DockStyle.Right;footer.Controls.Add(Open);layout.Controls.Add(footer,0,4);Controls.Add(layout);AcceptButton=Open;CancelButton=close;
            Query.TextChanged+=delegate{Filter();};Results.SelectedIndexChanged+=delegate{Open.Enabled=Results.SelectedItem is FlowQuickCommand&&((FlowQuickCommand)Results.SelectedItem).Enabled;};Results.DoubleClick+=delegate{Choose();};
            Query.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){MoveSelection(e.KeyCode==Keys.Down?1:-1);e.Handled=e.SuppressKeyPress=true;}};
            AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;FlowFormMotion.UseCustomAnimation(this);Ui.ApplyTheme(this);Query.Text=initial??"";Filter();
            Shown+=delegate{Results.ItemHeight=(int)Math.Ceiling(68*NativeTheme.DpiScale(this));Query.Focus();Query.SelectAll();if(UiMotion.Enabled){Opacity=0;motionStarted=DateTime.UtcNow;motion.Start();FlowTransitions.Enter(layout);}};
            motion.Tick+=delegate{double t=Math.Min(1,(DateTime.UtcNow-motionStarted).TotalMilliseconds/180);Opacity=closing?closeOpacity*(1-UiMotion.Ease((float)t)):UiMotion.Ease((float)t);if(t>=1||!UiMotion.Enabled){motion.Stop();Opacity=closing?0:1;if(closing){allowClose=true;DialogResult=closingResult;Close();}}};
            FormClosing+=(s,e)=>{if(allowClose||!UiMotion.Enabled||e.CloseReason!=CloseReason.UserClosing)return;if(closing){e.Cancel=true;return;}closingResult=DialogResult==DialogResult.None?DialogResult.Cancel:DialogResult;DialogResult=DialogResult.None;e.Cancel=true;closeOpacity=Opacity;closing=true;Query.Enabled=Results.Enabled=Open.Enabled=false;motionStarted=DateTime.UtcNow;motion.Start();};
            FormClosed+=delegate{motion.Stop();};if(UiMotion.Enabled)Opacity=0;
        }
        private void Filter(){if(closing)return;var rows=commands.Select((c,i)=>new{Command=c,Order=i,Rank=FlowCommandSearch.Rank(Query.Text,c.Label,c.Path,c.Aliases)}).Where(c=>c.Rank>=0).OrderBy(c=>c.Rank).ThenBy(c=>c.Order).Select(c=>(object)c.Command).ToArray();Results.BeginUpdate();try{Results.Items.Clear();Results.Items.AddRange(rows);if(rows.Length>0)Results.SelectedIndex=0;}finally{Results.EndUpdate();}Status.Text=rows.Length==0?"Nessun comando trovato. Prova un’altra parola.":rows.Length+(rows.Length==1?" comando":" comandi")+" · ↑ ↓ per scegliere, Invio per aprire";Open.Enabled=Results.SelectedItem is FlowQuickCommand&&((FlowQuickCommand)Results.SelectedItem).Enabled;}
        private void MoveSelection(int delta){if(Results.Items.Count>0)Results.SelectedIndex=Math.Max(0,Math.Min(Results.Items.Count-1,Results.SelectedIndex+delta));}
        private void Choose(){if(chosen||closing)return;if(refresh!=null)refresh();var command=Results.SelectedItem as FlowQuickCommand;if(command==null)return;if(!command.Enabled){Status.Text="Questo comando non è disponibile adesso.";Results.Invalidate();Open.Enabled=false;return;}chosen=true;Selected=command;DialogResult=DialogResult.OK;}
        private void Draw(object sender,DrawItemEventArgs e){if(e.Index<0||e.Index>=Results.Items.Count)return;var command=(FlowQuickCommand)Results.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;float scale=e.Graphics.DpiX/96f;using(var b=new SolidBrush(selected?Ui.Tint:Ui.InputBackground))e.Graphics.FillRectangle(b,e.Bounds);int x=e.Bounds.Left+(int)(12*scale),y=e.Bounds.Top+(int)(8*scale),w=e.Bounds.Width-(int)(24*scale);using(var font=Ui.Font(10,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,command.Label,font,new Rectangle(x,y,w,(int)(24*scale)),command.Enabled?Ui.Ink:Ui.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);TextRenderer.DrawText(e.Graphics,command.Path+(command.Enabled?"":" · Non disponibile adesso"),Font,new Rectangle(x,y+(int)(27*scale),w,(int)(23*scale)),Ui.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(e.Bounds,-2,-2),Ui.Blue,selected?Ui.Tint:Ui.InputBackground);}
        protected override bool ProcessCmdKey(ref Message msg,Keys key){if(key==(Keys.Control|Keys.K)){DialogResult=DialogResult.Cancel;return true;}return base.ProcessCmdKey(ref msg,key);}
        protected override void Dispose(bool disposing){if(disposing){motion.Stop();motion.Dispose();}base.Dispose(disposing);}
    }
    internal sealed partial class MainForm {
        private static void GroupCommands(ContextMenuStrip menu,string label,string name,params string[] prefixes){
            var group=new ToolStripMenuItem(label){Name=name};var matching=menu.Items.OfType<ToolStripMenuItem>().Where(i=>prefixes.Any(p=>i.Text.StartsWith(p,StringComparison.Ordinal))).ToArray();if(matching.Length==0){group.Dispose();return;}int position=menu.Items.IndexOf(matching[0]);foreach(var item in matching){menu.Items.Remove(item);group.DropDownItems.Add(item);}menu.Items.Insert(position,group);
        }
        private static IEnumerable<ToolStripMenuItem> MenuEntries(ToolStripItemCollection items){foreach(var item in items.OfType<ToolStripMenuItem>()){yield return item;foreach(var child in MenuEntries(item.DropDownItems))yield return child;}}
        private static void OrganizeTaskMenu(ContextMenuStrip menu,string section){
            if(section=="Progetto"){
                GroupCommands(menu,"Apri e recupera","ProjectLibrary","Home e progetti","Recupera lavoro","Esempi");
                GroupCommands(menu,"Condividi ed esporta","ProjectShare","Esporta…","Stampa…","Flowgorithm ·");
                GroupCommands(menu,"Impostazioni del progetto e dei file","ProjectSettings","Variabili del progetto","Attiva icona");
                var settings=menu.Items.OfType<ToolStripMenuItem>().Single(i=>i.Name=="ProjectSettings");menu.Items.Remove(settings);menu.Items.Add(new ToolStripSeparator());menu.Items.Add(settings);
            }else if(section=="Esegui"){
                GroupCommands(menu,"Debug passo passo","DebugSteps","Avanti ·","Esci dalla funzione");
                GroupCommands(menu,"Ispeziona l’esecuzione","RuntimeInspect","Variabili e chiamate","Console ·","File generati");
            }else if(section=="Vista")GroupCommands(menu,"Aspetto e animazioni","ViewAppearance","Stile dei diagrammi","Tema chiaro","Animazioni dell’");
            for(int i=menu.Items.Count-1;i>=0;i--)if(menu.Items[i] is ToolStripSeparator&&(i==0||i==menu.Items.Count-1||menu.Items[i-1] is ToolStripSeparator)){var item=menu.Items[i];menu.Items.RemoveAt(i);item.Dispose();}
        }
        private string commandQuery="";
        private static string CommandLabel(string text){string s=(text??"").Replace("&","");int key=s.IndexOf("   ",StringComparison.Ordinal);return(key<0?s:s.Substring(0,key)).Trim().TrimEnd('…',' ','▾');}
        private ContextMenuStrip PrepareTaskMenu(string section){ContextMenuStrip menu;if(!taskMenus.TryGetValue(section,out menu)||menu.IsDisposed){menu=BuildTaskMenu(section);taskMenus[section]=menu;}if(moreUndo!=null)moreUndo.Enabled=undo.Count>0||InspectorHasChanges();if(moreRedo!=null)moreRedo.Enabled=redo.Count>0;foreach(ToolStripMenuItem item in MenuEntries(menu.Items)){if(item.Name.StartsWith("Selection",StringComparison.Ordinal)){bool editable=!Canvas.ViewOnly&&!Canvas.IsPointerGestureActive&&!Canvas.IsConnecting;item.Enabled=editable&&(item.Name=="SelectionPaste"|| (item.Name=="SelectionAll"?Document.Nodes.Count>0:Canvas.SelectionCount>0));}if(item.Name=="VariablePolicy")item.Text="Variabili: "+(Project.RequiresDeclarations?"dichiarate":"automatiche")+"…";if(item.Name=="RunToSelection")item.Enabled=CanRunToSelected();if(item.Name=="PauseExecution")item.Enabled=runTimer.Enabled;if(item.Name=="ResetExecution")item.Enabled=Runner!=null;if(item.Name=="StepOutExecution")item.Enabled=Runner!=null&&Runner.CallDepth>1;if(item.Name=="SeparateIo")item.Checked=IoInstruction.Separate;if(item.Name=="CodeVisibility")item.Checked=workspaceLayout.Code;if(item.Name=="GridVisibility")item.Checked=Canvas.SnapToGrid;if(item.Name=="ThemePreference")item.Text=Ui.IsDark?"Attiva tema chiaro":"Attiva tema scuro";if(item.Name=="MotionPreference")item.Checked=UiMotion.PreferredEnabled;if(item.Name=="LayoutChoices")for(int i=0;i<item.DropDownItems.Count;i++)((ToolStripMenuItem)item.DropDownItems[i]).Checked=workspaceLayout.Mode==i;}return menu;}
        private IList<FlowQuickCommand> QuickCommands(){var result=new List<FlowQuickCommand>();foreach(string section in new[]{"Progetto","Modifica","Esegui","Codice","Vista","Aiuto"}){var menu=PrepareTaskMenu(section);CollectCommands(menu.Items,section,()=>true,result);}return result;}
        private void CollectCommands(ToolStripItemCollection items,string path,Func<bool> parent,IList<FlowQuickCommand> result){foreach(ToolStripMenuItem entry in items.OfType<ToolStripMenuItem>()){var item=entry;if(!item.Available||item.Name=="FindCommand")continue;Func<bool> available=()=>!item.IsDisposed&&parent()&&item.Enabled;if(item.DropDownItems.Count>0)CollectCommands(item.DropDownItems,path+" › "+CommandLabel(item.Text),available,result);else result.Add(new FlowQuickCommand(CommandLabel(item.Text),path,available,()=>item.PerformClick(),item.Text));}}
        private void ShowCommandPalette(){if(IsDisposed||Canvas.IsPointerGestureActive||Canvas.IsConnecting){SetStatus("Termina il gesto prima di cercare un comando.");return;}var expected=Project;TextBoxBase input=ActiveTextBox(this);int start=input==null?0:input.SelectionStart,length=input==null?0:input.SelectionLength;FlowQuickCommand selected=null;using(var dialog=new CommandPaletteDialog(QuickCommands(),()=>{foreach(string section in taskMenus.Keys.ToArray())PrepareTaskMenu(section);},commandQuery)){if(dialog.ShowDialog(this)==DialogResult.OK)selected=dialog.Selected;commandQuery=dialog.Query.Text;}if(IsDisposed)return;if(selected==null){if(input!=null&&!input.IsDisposed){input.Focus();input.Select(Math.Min(start,input.TextLength),Math.Min(length,Math.Max(0,input.TextLength-start)));}return;}if(!Object.ReferenceEquals(Project,expected)){SetStatus("Il progetto è cambiato. Cerca di nuovo il comando.");return;}if(!selected.Enabled){SetStatus("Questo comando non è disponibile adesso.");return;}if(homePanel!=null&&homePanel.Visible&&selected.Path!="Progetto"&&selected.Path!="Aiuto")EnterEditor();selected.Execute();}
    }
}


