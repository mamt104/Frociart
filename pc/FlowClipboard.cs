using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Frociart {
    // A bounded, versioned byte stream, not a serialized CLR object. Unicode text
    // accompanies it so copying a block also works in ordinary text editors.
    internal static class FlowClipboard {
        internal const string Format="Frociart.Blocks.v1";
        static readonly byte[] Magic=Encoding.ASCII.GetBytes("FRCBLOCK1\n");
        static readonly uint NativeFormat=RegisterClipboardFormat(Format);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterClipboardFormat(string name);
        [DllImport("user32.dll")]static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("user32.dll")]static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")]static extern bool CloseClipboard();
        [DllImport("user32.dll")]static extern IntPtr GetClipboardData(uint format);
        [DllImport("kernel32.dll")]static extern UIntPtr GlobalSize(IntPtr handle);
        [DllImport("kernel32.dll")]static extern IntPtr GlobalLock(IntPtr handle);
        [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr handle);
        internal static bool ContainsBlocks {get{return NativeFormat!=0&&IsClipboardFormatAvailable(NativeFormat);}}
        static DataContractJsonSerializer Serializer(){return new DataContractJsonSerializer(typeof(Diagram),new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=40000});}
        internal static byte[] Encode(Diagram fragment){
            DiagramIO.CheckDocument(fragment);
            using(var json=new MemoryStream()){
                Serializer().WriteObject(json,fragment);
                if(json.Length>DiagramIO.MaxFileBytes)throw new InvalidDataException("La selezione supera il limite di 4 MB degli appunti.");
                using(var packet=new MemoryStream())using(var writer=new BinaryWriter(packet)){
                    writer.Write(Magic);writer.Write((int)json.Length);writer.Write(json.ToArray());return packet.ToArray();
                }
            }
        }
        internal static Diagram Decode(byte[] bytes){
            int header=Magic.Length+4;
            if(bytes==null||bytes.Length<header||bytes.Length>DiagramIO.MaxFileBytes+header)throw new InvalidDataException("I blocchi negli appunti non sono validi o superano il limite di 4 MB.");
            using(var packet=new MemoryStream(bytes))using(var reader=new BinaryReader(packet)){
                if(!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))throw new InvalidDataException("Il formato dei blocchi negli appunti non è supportato.");
                int size=reader.ReadInt32();if(size<=0||size>DiagramIO.MaxFileBytes||size>bytes.Length-header)throw new InvalidDataException("I blocchi negli appunti sono incompleti.");
                // Share the bounded project reader: clipboard bytes are equally
                // untrusted and must be rejected before recursive deserialization.
                return DiagramIO.DecodeDocument(reader.ReadBytes(size));
            }
        }
        internal static void Write(Diagram fragment){
            byte[] bytes=Encode(fragment);
            using(var stream=new MemoryStream(bytes)){
                var data=new DataObject();data.SetData(Format,false,stream);
                data.SetData(DataFormats.UnicodeText,false,String.Join(Environment.NewLine,fragment.Nodes.Select(n=>n.Text??"")));
                Clipboard.SetDataObject(data,true,5,40);
            }
        }
        internal static Diagram Read(){
            if(!ContainsBlocks)return null;
            bool opened=false;
            for(int attempt=0;attempt<5&&!opened;attempt++){opened=OpenClipboard(IntPtr.Zero);if(!opened&&attempt<4)Thread.Sleep(40);}
            if(!opened)throw new ExternalException("Gli appunti sono occupati.");
            byte[] bytes=null;
            try{
                IntPtr handle=GetClipboardData(NativeFormat);if(handle==IntPtr.Zero)return null;
                ulong size=GlobalSize(handle).ToUInt64();
                if(size==0||size>(ulong)(DiagramIO.MaxFileBytes+Magic.Length+4))throw new InvalidDataException("I blocchi negli appunti superano il limite consentito.");
                IntPtr pointer=GlobalLock(handle);if(pointer==IntPtr.Zero)throw new ExternalException("Gli appunti non sono disponibili.");
                try{bytes=new byte[(int)size];Marshal.Copy(pointer,bytes,0,bytes.Length);}finally{GlobalUnlock(handle);}
            }finally{CloseClipboard();}
            return Decode(bytes);
        }
    }

    public partial class FlowCanvas {
        internal Action<Diagram> PrepareClipboardSnapshot;
        internal Action<Diagram> ClipboardWrite=FlowClipboard.Write;
        internal Func<Diagram> ClipboardRead=FlowClipboard.Read;
        private ToolStripMenuItem _menuCopy,_menuCut,_menuPaste;
        private Bitmap clipboardFlight;
        private Rectangle clipboardFlightBounds;
        private long clipboardFlightAt;
        private void StopClipboardFlight(){if(clipboardFlight!=null){clipboardFlight.Dispose();clipboardFlight=null;}}
        private bool TickClipboardFlight(long now){if(clipboardFlight!=null&&(!UiMotion.Enabled||now-clipboardFlightAt>=320))StopClipboardFlight();return clipboardFlight!=null;}
        private void StartClipboardFlight(ISet<string> ids,bool boundaryEdges=false){
            StopClipboardFlight();if(!UiMotion.Enabled||!Visible||!IsHandleCreated)return;EnsureScene();
            var visuals=_nodeVisuals.Where(n=>ids.Contains(n.Model.Id)).ToList();if(visuals.Count==0)return;
            RectangleF bounds=visuals[0].Bounds;foreach(var visual in visuals.Skip(1))bounds=RectangleF.Union(bounds,visual.Bounds);bounds.Inflate(20,20);
            var top=WorldToClient(bounds.Location);var screen=Rectangle.Intersect(ClientRectangle,Rectangle.Ceiling(new RectangleF(top.X,top.Y,bounds.Width*_zoom,bounds.Height*_zoom)));
            if(screen.Width<1||screen.Height<1||(long)screen.Width*screen.Height>8000000)return;
            clipboardFlight=new Bitmap(screen.Width,screen.Height);clipboardFlightBounds=screen;
            using(var graphics=Graphics.FromImage(clipboardFlight)){
                PrepareGraphics(graphics);graphics.TranslateTransform(_panX-screen.Left,_panY-screen.Top);graphics.ScaleTransform(_zoom,_zoom);
                foreach(var edge in _edgeVisuals.Where(e=>(ids.Contains(e.Model.From)&&ids.Contains(e.Model.To)||boundaryEdges&&(ids.Contains(e.Model.From)||ids.Contains(e.Model.To)))))DrawEdge(graphics,edge.Model,edge.Points,edge.Path,bounds,true,_zoom);
                foreach(var visual in visuals)DrawNode(graphics,visual,true,_zoom);
            }
            clipboardFlightAt=manipulationClock.ElapsedMilliseconds;manipulationTimer.Start();
        }
        private void PaintClipboardFlight(Graphics graphics){
            if(clipboardFlight==null)return;float t=Math.Min(1,(manipulationClock.ElapsedMilliseconds-clipboardFlightAt)/320f),p=UiMotion.Spring(t),scale=1-.08f*p;
            var bounds=clipboardFlightBounds;float width=bounds.Width*scale,height=bounds.Height*scale;
            using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
                var matrix=new System.Drawing.Imaging.ColorMatrix();matrix.Matrix33=Math.Max(0,1-t*t);attributes.SetColorMatrix(matrix);
                graphics.DrawImage(clipboardFlight,new Rectangle((int)(bounds.X+(bounds.Width-width)/2),(int)(bounds.Y+(bounds.Height-height)/2-18*p),(int)Math.Max(1,width),(int)Math.Max(1,height)),0,0,clipboardFlight.Width,clipboardFlight.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        internal bool HandleClipboardShortcut(Keys key){
            bool copy=key==(Keys.Control|Keys.C)||key==(Keys.Control|Keys.Insert);
            bool cut=key==(Keys.Control|Keys.X)||key==(Keys.Shift|Keys.Delete);
            bool paste=key==(Keys.Control|Keys.V)||key==(Keys.Shift|Keys.Insert);
            if(!copy&&!cut&&!paste)return false;
            if(IsPointerGestureActive||IsBlockPickerOpen)return true;
            if(ViewOnly){Report("La simulazione è in sola lettura. Torna a Scrivi per modificare i blocchi.");return true;}
            if(paste)PasteSelection();else CopySelection(cut);return true;
        }
        public bool CopySelection(bool cut=false){
            if(ViewOnly||_diagram==null||IsPointerGestureActive||IsBlockPickerOpen)return false;
            var ids=new HashSet<string>(_selectedNodeIds);
            if(ids.Count==0){Report("Seleziona uno o più blocchi da copiare.");return false;}
            var document=_diagram;
            try{
                var fragment=new Diagram{Title="Blocchi copiati",Nodes=document.Nodes.Where(n=>ids.Contains(n.Id)).ToList(),Edges=document.Edges.Where(e=>ids.Contains(e.From)&&ids.Contains(e.To)).ToList()};
                FlowAnnotations.Promote(fragment);fragment=DiagramIO.Clone(fragment);if(PrepareClipboardSnapshot!=null)PrepareClipboardSnapshot(fragment);
                if(fragment.Nodes.Count==0)return false;DiagramIO.CheckDocument(fragment);
                ClipboardWrite(fragment);
                if(cut){
                    if(!ReferenceEquals(document,_diagram)||!ids.SetEquals(_selectedNodeIds)){Report("Blocchi copiati. La selezione è cambiata: nessun blocco tagliato.");return false;}
                    StartClipboardFlight(ids);
                    DeleteSelection();
                }
                Report(fragment.Nodes.Count+(fragment.Nodes.Count==1?(cut?" blocco tagliato.":" blocco copiato."):(cut?" blocchi tagliati.":" blocchi copiati."))+" Ctrl+V per incollare anche in un altro progetto.");return true;
            }catch(ExternalException){Report("Appunti occupati: riprova. Nessun blocco è stato tagliato.");return false;}
            catch(InvalidDataException ex){Report(ex.Message+" Il diagramma è invariato.");return false;}
        }
        public bool PasteSelection(){return PasteSelection(null);}
        private bool PasteSelection(PointF? anchor){
            if(ViewOnly||_diagram==null||IsPointerGestureActive||IsBlockPickerOpen)return false;
            try{
                var fragment=ClipboardRead();if(fragment==null){Report("Negli appunti non ci sono blocchi Frociart. Selezionali e premi Ctrl+C.");return false;}
                fragment=DiagramIO.Clone(fragment);
                if(fragment.Nodes.Count==0){Report("Negli appunti non ci sono blocchi da incollare.");return false;}
                if(ProjectNodeCount+fragment.Nodes.Count>DiagramIO.MaxNodes||ProjectEdgeCount+fragment.Edges.Count>DiagramIO.MaxEdges){Report("Non c'è spazio per incollare: massimo 500 blocchi e 1500 collegamenti. Nessuna modifica applicata.");return false;}
                PointF offset;if(!FindPasteOffset(fragment,anchor,out offset)){Report("Non trovo spazio libero per la selezione. Sposta la vista e riprova.");return false;}
                var map=fragment.Nodes.ToDictionary(n=>n.Id,n=>Guid.NewGuid().ToString("N"));
                foreach(var node in fragment.Nodes){node.Id=map[node.Id];node.X+=offset.X;node.Y+=offset.Y;}
                foreach(var edge in fragment.Edges){edge.Id=Guid.NewGuid().ToString("N");edge.From=map[edge.From];edge.To=map[edge.To];}
                var document=_diagram;CancelConnection();RaiseDiagramChanging();if(!ReferenceEquals(document,_diagram))return false;
                document.Nodes.AddRange(fragment.Nodes);document.Edges.AddRange(fragment.Edges);SetSelection(fragment.Nodes.Select(n=>n.Id),null);RaiseDiagramChanged();
                if(ReferenceEquals(document,_diagram)){
                    var before=fragment.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y+18/Math.Max(.18f,_zoom)));
                    AnimatePlacement(before);StartLanding(fragment.Nodes[0].Id);Focus();
                }
                Report(fragment.Nodes.Count+(fragment.Nodes.Count==1?" blocco incollato.":" blocchi incollati.")+" Collegamenti interni conservati. Ctrl+Z annulla.");return true;
            }catch(ExternalException){Report("Appunti occupati: riprova. Il diagramma è invariato.");return false;}
            catch(InvalidDataException ex){Report(ex.Message+" Il diagramma è invariato.");return false;}
        }
        private bool FindPasteOffset(Diagram fragment,PointF? anchor,out PointF offset){
            var boxes=fragment.Nodes.Select(GetNodeBounds).ToList();RectangleF bounds=boxes[0];foreach(var box in boxes.Skip(1))bounds=RectangleF.Union(bounds,box);
            var occupied=_diagram.Nodes.Select(GetNodeBounds).ToList();for(int i=0;i<occupied.Count;i++){var box=occupied[i];box.Inflate(16,16);occupied[i]=box;}
            var origins=fragment.Nodes.ToDictionary(n=>n.Id,n=>new PointF(n.X,n.Y));
            var center=anchor??ClientToWorld(new Point(ClientSize.Width/2,ClientSize.Height/2));
            var requested=new PointF(Snap(center.X-bounds.Left-bounds.Width/2),Snap(center.Y-bounds.Top-bounds.Height/2));
            var candidates=new List<PointF>{requested};
            for(int ring=1;ring<=6;ring++)foreach(var direction in new[]{new PointF(1,0),new PointF(0,1),new PointF(-1,0),new PointF(0,-1),new PointF(1,1),new PointF(-1,1),new PointF(1,-1),new PointF(-1,-1)})candidates.Add(new PointF(requested.X+direction.X*ring*48,requested.Y+direction.Y*ring*48));
            if(occupied.Count>0){candidates.Add(new PointF(Snap(occupied.Max(b=>b.Right)+48-bounds.Left),requested.Y));candidates.Add(new PointF(Snap(occupied.Min(b=>b.Left)-48-bounds.Right),requested.Y));}
            foreach(var candidate in candidates){
                if(!TryConstrainOffset(origins,candidate,out offset))return false;
                PointF translation=offset;
                if(!boxes.Any(box=>occupied.Any(other=>other.IntersectsWith(new RectangleF(box.X+translation.X,box.Y+translation.Y,box.Width,box.Height)))))return true;
            }
            offset=PointF.Empty;return false;
        }
    }
}
