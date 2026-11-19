using System;using System.Collections.Generic;using System.Drawing;using System.Globalization;using System.Linq;using System.Windows.Forms;
namespace Frociart {
 internal sealed partial class FlowVariableWindow {
  readonly FlowScrollPanel readableViewport=new FlowScrollPanel{Dock=DockStyle.Fill,AutoScroll=true,TabIndex=0};
  readonly TableLayoutPanel readableFooter=new TableLayoutPanel{Dock=DockStyle.Bottom,ColumnCount=1,RowCount=2,TabIndex=1};
  internal readonly FlowButton TextSize=Ui.Button("Testo 100% ▾",135,null);
  readonly Dictionary<Control,FontSpec> readableBaseFonts=new Dictionary<Control,FontSpec>();
  readonly Dictionary<string,Font> readableFonts=new Dictionary<string,Font>();
  readonly Dictionary<FlowButton,int> readableButtonWidths=new Dictionary<FlowButton,int>();
  bool readableReady,layingOutReadability;string readableKey;int textPercent=100;
  internal int TextPercent {get{return textPercent;}}
  sealed class FontSpec {internal readonly string Family;internal readonly float Size;internal readonly FontStyle Style;internal FontSpec(Font f){Family=f.FontFamily.Name;Size=f.SizeInPoints;Style=f.Style;}}
  int ReadPx(int value){return Math.Max(1,(int)Math.Round(value*NativeTheme.DpiScale(this)));}
  static IEnumerable<Control> ReadableControls(Control parent){yield return parent;foreach(Control child in parent.Controls)foreach(Control nested in ReadableControls(child))yield return nested;}
  void InitializeReadableLayout(){
   readableFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));readableFooter.RowStyles.Add(new RowStyle(SizeType.Absolute,0));readableFooter.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
   inputArea.Dock=DockStyle.Fill;inputArea.AutoSize=false;inputArea.RowStyles.Clear();inputArea.RowStyles.Add(new RowStyle(SizeType.Absolute,24));inputArea.RowStyles.Add(new RowStyle(SizeType.Absolute,34));question.AutoSize=false;question.AutoEllipsis=true;
   readableViewport.Controls.Add(root);Controls.Add(readableViewport);Controls.Add(readableFooter);readableViewport.BringToFront();root.Anchor=AnchorStyles.Top|AnchorStyles.Left;
   var heading=(Panel)root.GetControlFromPosition(0,0);heading.Controls.Add(TextSize);TextSize.BringToFront();TextSize.AccessibleName="Dimensione del testo dell'ispettore";TextSize.AccessibleDescription="Solo questa finestra. Ctrl e più ingrandisce, Ctrl e meno riduce, Ctrl e zero ripristina.";TextSize.Click+=delegate{ChooseReadableText();};
   foreach(Control c in ReadableControls(this)){readableBaseFonts[c]=new FontSpec(c.Font);var button=c as FlowButton;if(button!=null)readableButtonWidths[button]=button.Width;}
   foreach(int row in new[]{4,6})((FlowLayoutPanel)root.GetControlFromPosition(0,row)).WrapContents=true;
   ((FlowLayoutPanel)readableFooter.GetControlFromPosition(0,1)).WrapContents=true;
   readableViewport.ClientSizeChanged+=delegate{RefreshReadableState();};ClientSizeChanged+=delegate{RefreshReadableState();};
   Shown+=delegate{RefreshReadableState();if(inputArea.Visible)FocusPendingInput();else Search.Focus();};readableReady=true;RefreshReadableState();
  }
  void ChooseReadableText(){
   var options=new[]{"100%","125%","150%","175%","200%"};string choice=TutorChoiceDialog.Choose(this,"Dimensione del testo",options,v=>v,v=>"Solo la finestra Variabili e chiamate. I dati della prova restano invariati.",textPercent+"%");
   if(choice!=null)SetTextPercent(Int32.Parse(choice.TrimEnd('%'),CultureInfo.InvariantCulture));
  }
  bool HandleReadabilityKey(Keys key){
   if(key==(Keys.Control|Keys.Add)||key==(Keys.Control|Keys.Oemplus)){SetTextPercent(Math.Min(200,textPercent+25));return true;}
   if(key==(Keys.Control|Keys.Subtract)||key==(Keys.Control|Keys.OemMinus)){SetTextPercent(Math.Max(100,textPercent-25));return true;}
   if(key==(Keys.Control|Keys.D0)||key==(Keys.Control|Keys.NumPad0)){SetTextPercent(100);return true;}
   if(key==(Keys.Control|Keys.F)){Search.Focus();Search.SelectAll();readableViewport.ScrollControlIntoView(Search);return true;}return false;
  }
  internal void SetTextPercent(int percent){
   if(percent<100||percent>200||percent%25!=0)throw new ArgumentOutOfRangeException("percent");if(!readableReady||percent==textPercent||closed)return;Control focused=ActiveControl;layingOutReadability=true;SuspendLayout();root.SuspendLayout();
   try{textPercent=percent;foreach(var item in readableBaseFonts){var c=item.Key;if(c.IsDisposed)continue;var f=item.Value;float size=f.Size*percent/100f;string key=f.Family+"|"+f.Style+"|"+size.ToString("R",CultureInfo.InvariantCulture);Font font;if(!readableFonts.TryGetValue(key,out font)){font=new Font(f.Family,size,f.Style,GraphicsUnit.Point);readableFonts.Add(key,font);}c.Font=font;}TextSize.Text="Testo "+percent+"% ▾";readableKey=null;}
   finally{root.ResumeLayout(true);ResumeLayout(true);layingOutReadability=false;}
   RefreshReadableState();if(focused!=null&&!focused.IsDisposed&&focused.CanFocus){focused.Focus();if(readableViewport.Contains(focused))readableViewport.ScrollControlIntoView(focused);}
  }
  string ReadableLayoutKey(){return ClientSize.Width+":"+ClientSize.Height+":"+readableViewport.ClientSize.Width+":"+textPercent+":"+inputArea.Visible+":"+question.Text+":"+FileAction.Visible+":"+Play.Text+":"+elementTools.Visible;}
  void RefreshReadableState(){
   if(!readableReady||layingOutReadability||closed||IsDisposed)return;string key=ReadableLayoutKey();if(key==readableKey)return;layingOutReadability=true;
   try{for(int pass=0;pass<3;pass++)LayoutReadablePass();readableKey=ReadableLayoutKey();}finally{layingOutReadability=false;}
  }
  int FlowHeight(FlowLayoutPanel flow,int width){flow.Width=Math.Max(1,width);return Math.Max(ReadPx(34),flow.GetPreferredSize(new Size(Math.Max(1,width),0)).Height)+ReadPx(6);}
  void LayoutReadablePass(){
   int gap=ReadPx(8),edge=ReadPx(18);foreach(var pair in readableButtonWidths){var b=pair.Key;if(b.IsDisposed)continue;b.Height=Math.Max(ReadPx(34),b.Font.Height+ReadPx(14));b.Width=Math.Max(ReadPx(pair.Value),TextRenderer.MeasureText(b.Text,b.Font,Size.Empty,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Width+ReadPx(28));}
   readableFooter.Padding=new Padding(edge,0,edge,ReadPx(8));int footerWidth=Math.Max(1,ClientSize.Width-edge*2);var commands=(FlowLayoutPanel)readableFooter.GetControlFromPosition(0,1);int commandHeight=FlowHeight(commands,footerWidth),inputHeight=0;
   if(inputArea.Visible){int inputRow=Math.Max(Input.PreferredHeight,Send.Height),questionWidth=Math.Max(ReadPx(140),footerWidth);int questionHeight=Math.Min(question.Font.Height*3+gap,Math.Max(question.Font.Height+ReadPx(4),TextRenderer.MeasureText(question.Text,question.Font,new Size(questionWidth,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix).Height));question.MaximumSize=Size.Empty;inputArea.ColumnStyles[1].Width=Send.Width+gap;inputArea.RowStyles[0].Height=questionHeight;inputArea.RowStyles[1].Height=inputRow;inputHeight=questionHeight+inputRow+gap;question.AccessibleName=question.Text;}
   readableFooter.RowStyles[0].Height=inputHeight;readableFooter.RowStyles[1].Height=commandHeight;readableFooter.Height=inputHeight+commandHeight+readableFooter.Padding.Vertical;
   root.Padding=new Padding(edge);root.Width=Math.Max(1,readableViewport.ClientSize.Width);int width=Math.Max(ReadPx(180),root.Width-root.Padding.Horizontal),height;
   var heading=(Panel)root.GetControlFromPosition(0,0);title.Dock=info.Dock=DockStyle.None;int titleWidth=Math.Max(ReadPx(100),width-TextSize.Width-gap);height=Math.Max(ReadPx(60),title.Font.Height+info.Font.Height+ReadPx(12));title.SetBounds(0,0,titleWidth,title.Font.Height+ReadPx(4));info.SetBounds(0,title.Bottom,titleWidth,info.Font.Height+ReadPx(4));TextSize.SetBounds(Math.Max(0,width-TextSize.Width),0,TextSize.Width,TextSize.Height);root.RowStyles[0].Height=height;
   var scope=(Panel)root.GetControlFromPosition(0,1);Frames.Dock=Follow.Dock=DockStyle.None;Follow.Width=Math.Max(ReadPx(205),TextRenderer.MeasureText(Follow.Text,Follow.Font).Width+ReadPx(30));Follow.Height=Math.Max(ReadPx(30),Follow.Font.Height+ReadPx(10));Frames.ItemHeight=Math.Max(ReadPx(18),Frames.Font.Height+ReadPx(6));int frameHeight=Frames.PreferredHeight;
   bool stacked=width-Follow.Width-gap<ReadPx(180);if(stacked){Frames.SetBounds(0,0,width,frameHeight);Follow.SetBounds(0,frameHeight+ReadPx(4),width,Follow.Height);height=frameHeight+ReadPx(4)+Follow.Height+gap;}else{Frames.SetBounds(0,0,Math.Max(1,width-Follow.Width-gap),frameHeight);Follow.SetBounds(width-Follow.Width,0,Follow.Width,Follow.Height);height=Math.Max(frameHeight,Follow.Height)+gap;}root.RowStyles[1].Height=height;
   if(Frames.Items.Count>0){int widest=Frames.Items.Cast<object>().Max(v=>TextRenderer.MeasureText(v.ToString(),Frames.Font).Width)+ReadPx(40);Frames.DropDownWidth=Math.Min(Screen.FromControl(this).WorkingArea.Width-ReadPx(24),Math.Max(Frames.Width,widest));}
   var searchArea=(Panel)root.GetControlFromPosition(0,2);var label=searchArea.Controls.OfType<Label>().First();label.Width=TextRenderer.MeasureText(label.Text,label.Font).Width+ReadPx(12);height=Math.Max(Search.PreferredHeight,label.Font.Height+ReadPx(8))+ReadPx(6);root.RowStyles[2].Height=height;
   root.RowStyles[4].Height=FlowHeight((FlowLayoutPanel)root.GetControlFromPosition(0,4),width);root.RowStyles[6].Height=FlowHeight((FlowLayoutPanel)root.GetControlFromPosition(0,6),width);root.RowStyles[7].Height=Math.Max(ReadPx(28),notice.Font.Height+ReadPx(6));
   pageLabel.Height=Math.Max(ReadPx(30),pageLabel.Font.Height+ReadPx(6));pageLabel.Width=Math.Min(width,Math.Max(ReadPx(210),TextRenderer.MeasureText("1024–1088 di 16384",pageLabel.Font).Width+ReadPx(12)));
   selection.AutoEllipsis=true;detailArea.RowStyles[0].Height=Math.Max(ReadPx(28),selection.Font.Height+ReadPx(6));elementPageLabel.Height=Math.Max(ReadPx(30),elementPageLabel.Font.Height+ReadPx(6));elementPageLabel.Width=Math.Min(width,Math.Max(ReadPx(180),TextRenderer.MeasureText("Indici 960–1023 di 1024",elementPageLabel.Font).Width+ReadPx(12)));Index.Width=Math.Max(ReadPx(65),TextRenderer.MeasureText("1023",Index.Font).Width+ReadPx(16));
   int vectorTools=elementTools.Visible?FlowHeight(elementTools,width):0;int minTable=Math.Max(ReadPx(84),Variables.Font.Height*3+ReadPx(30)),minDetail=(int)detailArea.RowStyles[0].Height+vectorTools+Math.Max(ReadPx(85),Detail.Font.Height*3+ReadPx(15));Variables.MinimumSize=new Size(0,minTable);detailArea.MinimumSize=new Size(0,minDetail);
   int fixedHeight=root.Padding.Vertical+(int)root.RowStyles.Cast<RowStyle>().Where(r=>r.SizeType==SizeType.Absolute).Sum(r=>r.Height);int contentHeight=Math.Max(readableViewport.ClientSize.Height,fixedHeight+minTable+minDetail);root.Height=contentHeight;readableViewport.AutoScrollMinSize=new Size(0,contentHeight);root.PerformLayout();SizeColumns();
  }
  void DisposeReadableFonts(){foreach(var font in readableFonts.Values)font.Dispose();readableFonts.Clear();readableBaseFonts.Clear();readableButtonWidths.Clear();}
 }
}
