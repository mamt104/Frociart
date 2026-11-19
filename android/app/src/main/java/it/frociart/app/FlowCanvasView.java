package it.frociart.app;

import android.content.Context;
import android.animation.Animator;
import android.animation.AnimatorListenerAdapter;
import android.animation.ValueAnimator;
import android.database.ContentObserver;
import android.graphics.Canvas;
import android.graphics.Bitmap;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.PathMeasure;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.view.HapticFeedbackConstants;
import android.view.MotionEvent;
import android.view.View;
import android.view.DragEvent;
import android.content.ClipData;
import android.view.ViewConfiguration;
import android.view.animation.DecelerateInterpolator;
import android.view.animation.LinearInterpolator;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityManager;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityNodeProvider;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

/**
 * Native, retained-mode flowchart canvas. Model coordinates are the desktop
 * coordinates; at zoom 1 one model unit occupies one density-independent pixel.
 * Motion is visual only. Apart from the visible, selected connector affordance,
 * no animator or redraw timer runs while the canvas is idle.
 */
public final class FlowCanvasView extends View {
    private static final android.graphics.PathEffect NOTE_DASH=new android.graphics.DashPathEffect(new float[]{6,4},0);
    public interface InsertionChoice { void choose(String branch); }
    public interface Listener {
        void onBeforeChange();
        void onDiagramChanged(String label);
        default void onCyclePresentationChanged(){}
        void onSelectionChanged();
        void onEditRequested(String nodeId);
        default boolean onSelectionMenuRequested(){return false;}
        default boolean onReadOnlyNodeTap(String nodeId){return false;}
        void onAddRequested(float centerX, float centerY, String sourceId, String branch);
        void onConnectRequested(String nodeId);
        void onConnectionStateChanged(String sourceId, String branch);
        void onHint(String text);
        /** A visible + adds a block; older hosts can retain their existing picker. */
        default void onAddAfterRequested(String sourceId) { onConnectRequested(sourceId); }
        default void onAddAfterRequested(String sourceId,String branch) { onAddAfterRequested(sourceId); }
        /** Every connector drop is reviewed by the host before any model mutation. */
        default void onConnectionDropRequested(String sourceId, String targetId, float centerX, float centerY) {
            onConnectRequested(sourceId);
        }
        default void onConnectionDropRequested(String sourceId,String branch,String targetId,float centerX,float centerY) {
            onConnectionDropRequested(sourceId,targetId,centerX,centerY);
        }
        /** A tap destination can be handled by the host; false retains the legacy commit. */
        default boolean onConnectionTargetRequested(String sourceId, String branch, String targetId,
                                                    float centerX, float centerY) { return false; }
        /** A direct tap on a visible line or branch label opens its controls. */
        default void onEdgeEditRequested(String edgeId) { }
        /** Called once, before the undo snapshot, when a real manual drag starts. */
        default void onManualNodeMove() { }
        /** A manual line edit keeps the layout in the user's hands. */
        default void onManualConnectionChange() { onManualNodeMove(); }
        default void onNodeInsertionRequested(String nodeId,String edgeId,InsertionChoice choice) { choice.choose(null); }
        default void onIoPaletteRequested(FlowModel.Node node,java.util.function.IntConsumer choice) { }
        default void onPaletteInsertionRequested(FlowModel.Node node,String edgeId,InsertionChoice choice) { choice.choose(null); }
        default void onNodeDetachmentRequested(String nodeId,InsertionChoice choice) { choice.choose(null); }
    }

    private static final int ACCENT = 0xFF5B8DEF;
    private static final int LIGHT_PAPER = 0xFFFAF9F6, DARK_PAPER = 0xFF161616;
    private static final int LIGHT_SURFACE = Color.WHITE, DARK_SURFACE = 0xFF222222;
    private static final int LIGHT_INK = 0xFF20222A, DARK_INK = 0xFFF4F5F9;
    private static final int LIGHT_MUTED = 0xFF737987, DARK_MUTED = 0xFFA6ACB9;
    private static final int LIGHT_LINE = 0xFFE4E6ED, DARK_LINE = 0xFF353841;
    private static final int TRUE_COLOR = 0xFF39B985, FALSE_COLOR = 0xFFEF6C82;
    private static final float MAX_ZOOM = FlowViewport.MAX_ZOOM;
    private static final float MOBILE_READING_MIN_ZOOM = 0.80f;
    private static final float MOBILE_READING_ZOOM = 1.05f;
    private static final float MAX_COORDINATE = 100000f;
    private static final int NONE = 0, PENDING = 1, PAN = 2, DRAG = 3,
            MARQUEE = 4, MULTI = 5, SUPPRESSED = 6, PLUS_PENDING = 7, CONNECT_DRAG = 8,
            EDGE_PENDING = 9, EDGE_DRAG = 10;
    private static final int ACCESS_EDIT = 0x02000001, ACCESS_CONNECT = 0x02000002, ACCESS_ADD = 0x02000003,
            ACCESS_ADD_TRUE=0x02000004,ACCESS_ADD_FALSE=0x02000005,ACCESS_LINK_TRUE=0x02000006,ACCESS_LINK_FALSE=0x02000007;
    private static final int DROP_COLOR = 0xFF278C66, INVALID_COLOR = 0xFFD84E68;
    private static final Typeface BODY_TYPEFACE = Typeface.create("sans-serif", Typeface.NORMAL);
    private static final Typeface MEDIUM_TYPEFACE = Typeface.create("sans-serif-medium", Typeface.NORMAL);
    private static final HashMap<Integer,FlowModel.Sizing> fontProfiles=new HashMap<>();
    static Typeface font(FlowDesign d,boolean bold){return d.family==0?(bold?MEDIUM_TYPEFACE:BODY_TYPEFACE):Typeface.create(d.fontFamily(),bold?Typeface.BOLD:Typeface.NORMAL);}
    public static synchronized FlowModel.Sizing fontSizing(FlowDesign d){FlowModel.Sizing result=fontProfiles.get(d.fontKey());if(result!=null)return result;final Paint metrics=new Paint(Paint.ANTI_ALIAS_FLAG);result=new FlowModel.Sizing((value,kind)->{synchronized(metrics){metrics.setTypeface(font(d,kind==FlowModel.START||kind==FlowModel.END));metrics.setTextSize((kind==FlowModel.DECISION?13.3f:kind==FlowModel.START||kind==FlowModel.END?14:13.7f)*d.scale());return metrics.measureText(value);}},d.scale());fontProfiles.put(d.fontKey(),result);return result;}
    public static void installSizing(){FlowModel.setSizing(fontSizing(FlowAppearance.activeDesign()));}
    static {installSizing();}
    private final HashMap<String,float[]> sizingFrom=new HashMap<>();
    private ValueAnimator sizingAnimator;
    private float sizingFraction=1;
    private void stopSizing(){ValueAnimator v=sizingAnimator;sizingAnimator=null;if(v!=null){v.removeAllUpdateListeners();v.removeAllListeners();v.cancel();}sizingFrom.clear();sizingFraction=1;}
    private void animateSizing(){
        HashMap<String,float[]> from=new HashMap<>();for(FlowModel.Node node:document.nodes){NodeVisual old=nodeIndex.get(node.id);if(old!=null&&(Math.abs(old.bounds.width()-width(node))>.1f||Math.abs(old.bounds.height()-height(node))>.1f))from.put(node.id,new float[]{old.bounds.width(),old.bounds.height()});}
        animateSizesFrom(from);
    }
    public void animateSizesFrom(java.util.Map<String,float[]> previous){
        java.util.HashMap<String,float[]> from=new java.util.HashMap<>();for(FlowModel.Node n:document.nodes){float[] old=previous.get(n.id);if(old!=null&&(Math.abs(old[0]-width(n))>.1f||Math.abs(old[1]-height(n))>.1f))from.put(n.id,old);}
        stopSizing();if(from.isEmpty()||!canAnimate())return;sizingFrom.putAll(from);sizingFraction=0;
        final ValueAnimator v=ValueAnimator.ofFloat(0,1);sizingAnimator=v;v.setDuration(320);v.setInterpolator(Ui.SPRING);
        v.addUpdateListener(a->{sizingFraction=(float)a.getAnimatedValue();dirty=true;invalidate();});
        v.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator a){if(sizingAnimator==v){sizingAnimator=null;sizingFrom.clear();sizingFraction=1;dirty=true;invalidate();}}});v.start();
    }
    public void previewText(String id,String text){FlowModel.Node n=document.find(id);if(n==null)return;n.previewText=text;animateSizing();dirty=true;invalidate();}
    public void previewComment(String id,String text){FlowModel.Node n=document.find(id);if(n==null)return;n.previewComment=text;animateSizing();dirty=true;invalidate();}
    public void clearTextPreview(String id){FlowModel.Node n=document.find(id);if(n!=null){n.previewText=null;n.previewComment=null;animateSizing();dirty=true;invalidate();}}


    private final float density;
    private final float touchSlop;
    private FlowAppearance appearance=new FlowAppearance(FlowAppearance.current());
    public int getAppearance(){return appearance.mode;}
    private FlowModel.Sizing sizing=fontSizing(appearance.design());
    FlowModel.Sizing sizingProfile(){return sizing;}
    public FlowAppearance appearanceStyle(){return appearance;}
    public void setAppearance(int mode){setAppearance(new FlowAppearance(mode));}
    public void setAppearance(FlowAppearance style){boolean resized=appearance.design().fontKey()!=style.design().fontKey();if(appearance.mode!=style.mode||!appearance.customDesign.same(style.customDesign))captureAppearance();appearance=new FlowAppearance(style.mode,style.customDesign);sizing=fontSizing(appearance.design());if(resized){nodes.clear();nodeIndex.clear();edges.clear();edgeIndex.clear();cycleFoldDocument=null;structureDirty=dirty=true;}invalidate();}
    private Bitmap appearanceGhost;
    private ValueAnimator appearanceAnimator;
    private float appearanceAlpha;
    private final Paint appearancePaint=new Paint(Paint.ANTI_ALIAS_FLAG|Paint.FILTER_BITMAP_FLAG);
    private void stopAppearance(){ValueAnimator old=appearanceAnimator;appearanceAnimator=null;if(old!=null){old.removeAllUpdateListeners();old.removeAllListeners();old.cancel();}if(appearanceGhost!=null){appearanceGhost.recycle();appearanceGhost=null;}appearanceAlpha=0;}
    private void captureAppearance(){
        if(!canAnimate()||!isShown()||getWidth()<1||getHeight()<1||(long)getWidth()*getHeight()>8000000){stopAppearance();return;}
        Bitmap snapshot;try{snapshot=Bitmap.createBitmap(getWidth(),getHeight(),Bitmap.Config.ARGB_8888);}catch(OutOfMemoryError optionalAnimationUnavailable){stopAppearance();return;}try{draw(new Canvas(snapshot));}catch(RuntimeException e){snapshot.recycle();throw e;}stopAppearance();appearanceGhost=snapshot;appearanceAlpha=1;
        ValueAnimator animator=ValueAnimator.ofFloat(1,0);appearanceAnimator=animator;animator.setDuration(240);animator.setInterpolator(new android.view.animation.AccelerateDecelerateInterpolator());animator.addUpdateListener(v->{appearanceAlpha=(float)v.getAnimatedValue();invalidate();});animator.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator a){if(appearanceAnimator==animator){stopAppearance();invalidate();}}});animator.start();
    }
    @Override public void draw(Canvas canvas){super.draw(canvas);if(appearanceGhost!=null){if(!canAnimate()){stopAppearance();return;}appearancePaint.setAlpha(Math.max(0,Math.min(255,Math.round(appearanceAlpha*255))));canvas.drawBitmap(appearanceGhost,0,0,appearancePaint);}}
    private float width(FlowModel.Node n){return FlowModel.width(n,sizing);}
    private float height(FlowModel.Node n){return FlowModel.height(n,sizing);}
    private FlowModel.NodeSize size(FlowModel.Node n){return FlowModel.size(n,sizing);}
    private boolean monochromeExport;
    private final class ExportPaint extends Paint {
        ExportPaint(int flags){super(flags);}
        @Override public void setColor(int color){
            if(monochromeExport){int value=(77*Color.red(color)+150*Color.green(color)+29*Color.blue(color)+128)>>8;color=Color.argb(Color.alpha(color),value,value,value);}
            super.setColor(color);
        }
    }
    private final Paint fillPaint = new ExportPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint strokePaint = new ExportPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint textPaint = new ExportPaint(Paint.ANTI_ALIAS_FLAG | Paint.SUBPIXEL_TEXT_FLAG);
    private final Paint labelPaint = new ExportPaint(Paint.ANTI_ALIAS_FLAG | Paint.SUBPIXEL_TEXT_FLAG);
    private final Paint gridPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF visibleWorld = new RectF();
    private final RectF tempRect = new RectF();
    private final RectF diagramBounds = new RectF();
    private final RectF marquee = new RectF();
    private final RectF plusHit = new RectF();
    private final RectF truePlusHit = new RectF(),falsePlusHit = new RectF();
    private final Path scratchPath = new Path();
    private float[] connectionLoopPreview = new float[0];
    private final Path runPath = new Path(), runProgressPath = new Path();
    private final PathMeasure runMeasure = new PathMeasure();
    private final float[] runPoint = new float[2];
    private final Paint.FontMetrics fontMetrics = new Paint.FontMetrics();
    private final float[] routeBuffer = new float[16];
    private float[] gridPoints = new float[0];

    private Listener listener;
    private java.util.function.IntSupplier otherNodes=()->0,otherEdges=()->0;
    public void setCapacityProvider(java.util.function.IntSupplier nodes,java.util.function.IntSupplier edges){otherNodes=nodes;otherEdges=edges;}
    private FlowModel.Document document = new FlowModel.Document();
    private boolean dark, readOnly, selectionMode, dirty = true, structureDirty = true, pendingFit = true, pendingReadingFit = true;
    private float zoom = 1f, panX, panY;
    private long geometryGeneration;
    private final ArrayList<NodeVisual> nodes = new ArrayList<>();
    private final ArrayList<NodeVisual> nodesByTop = new ArrayList<>();
    private final ArrayList<EdgeVisual> edges = new ArrayList<>();
    private final HashMap<String, NodeVisual> nodeIndex = new HashMap<>();
    private final HashMap<String, EdgeVisual> edgeIndex = new HashMap<>();
    private final HashSet<String> convergingDecisionEdges=new HashSet<>();
    private final ArrayList<InsertionRoute> stickerBypass=new ArrayList<>();
    private final LinkedHashSet<String> selected = new LinkedHashSet<>();
    private String highlightId, connectionSource, connectionBranch, plusNodeId;

    private boolean motionEnabled = true, systemMotionEnabled = true, motionReady, observingMotion;
    private boolean mobileViewport, modelGeometry, cameraForRun;
    private ValueAnimator connectorAnimator, runAnimator, cameraAnimator, layoutAnimator,edgeMotionAnimator;
    private float edgeMotionFraction=1;private String fusionNodeId;
    private final ArrayList<EdgeSnapshot> outgoingMotion=new ArrayList<>();
    private final HashSet<String> enteringMotion=new HashSet<>();
    private PendingPlacement pendingPlacement;
    private final HashSet<String> blockedBypass=new HashSet<>();private boolean bypassExited;
    private float connectorPhase, runFraction, runLength, layoutFraction = 1f, layoutOpacity = 1f;
    private float cameraEndZoom, cameraEndX, cameraEndY;
    private int runColor = ACCENT;
    private String runDestination;
    private final HashMap<String, LayoutPosition> layoutPositions = new HashMap<>();
    private String dragConnectionSource, dragConnectionTarget;
    private String downBranch,dragConnectionBranch;
    private String dragEdgeId;
    private float dragConnectionX, dragConnectionY;
    private final ContentObserver motionObserver = new ContentObserver(new Handler(Looper.getMainLooper())) {
        @Override public void onChange(boolean selfChange) { refreshSystemMotion(); }
    };

    private int gesture = NONE, activePointer = -1, firstMultiPointer = -1, secondMultiPointer = -1;
    private float downX, downY, lastX, lastY, downWorldX, downWorldY;
    // View-local coordinates change when surrounding controls relayout. Gesture
    // classification and movement follow the finger in physical screen space.
    private float downRawX,downRawY,lastRawX,lastRawY;
    private float multiX, multiY, multiDistance;
    private String downNodeId, downEdgeId;
    private boolean dragChanged, dragHistoryStarted;
    private final ArrayList<DragOrigin> dragOrigins = new ArrayList<>();
    private final ArrayList<InsertionRoute> insertionRoutes = new ArrayList<>();
    private InsertionRoute insertionHover;private float insertionStrength;private boolean insertionReady;
    private final LinkedHashSet<String> marqueeBase = new LinkedHashSet<>();
    private long lastTapTime;
    private float lastTapX, lastTapY;
    private String lastTapNode;
    private boolean lastTapWasBlank;
    private int accessibilityFocus = -1, hoverVirtualId = -1;
    private long semanticGeneration;
    private final CanvasAccessibility accessibility = new CanvasAccessibility();

    private final Runnable longPress = new Runnable() {
        @Override public void run() {
            if (readOnly || gesture != PENDING || activePointer < 0 || isConnecting()) return;
            if(downNodeId==null||selectionMode||selected.size()>1)performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            lastTapTime = 0;
            if (downNodeId != null) {
                if(isFoldCard(downNodeId)){applySelection(foldSelection(java.util.Collections.singleton(downNodeId)),false);startDrag();hint("Trascina per spostare tutto il ciclo");return;}
                if(selectionMode&&!groupSelected()){toggleSelection(downNodeId);cancelHoldFill();gesture=SUPPRESSED;}
                else {startDrag();if(beginGroupLift())return;FlowModel.Node moving=document.find(downNodeId);boolean decision=moving.kind==FlowModel.DECISION;
                 boolean linked=false,output=false;for(FlowModel.Edge edge:document.edges){if(edge.from.equals(downNodeId)){linked=true;output=true;}if(edge.to.equals(downNodeId))linked=true;}
                 boolean eligible=moving.kind!=FlowModel.START&&moving.kind!=FlowModel.END;stickerDetachNone=eligible&&linked&&!output;stickerDetached=eligible&&linked&&(stickerDetachNone||FlowPlacement.continuation(document,downNodeId)!=null);stickerAmbiguous=linked&&!stickerDetached&&decision;
                 if(stickerDetached||stickerAmbiguous){insertionDocument=document.copy();if(stickerDetachNone||stickerAmbiguous)FlowPlacement.disconnect(insertionDocument,downNodeId);else FlowPlacement.detach(insertionDocument,downNodeId);captureInsertionRoutes();hint(stickerAmbiguous?"Decisione sollevata · nel vuoto si stacca senza menu · i vecchi rami restano liberi":"Pronto · trascina oppure rilascia per staccare");}
                 else hint("Blocco sollevato · trascina per spostarlo");
                 holdFill=1;cacheStickerBypass();startLift();}
            } else {
                startMarquee();
                hint("Fai scorrere il dito per selezionare più blocchi.");
            }
        }
    };

    private static final class NodeVisual {
        FlowModel.Node node;
        final RectF bounds = new RectF();
        final Path shape = new Path();
        final Path halo = new Path();
        int kind = -1;
        long generation;
        String text;
        String[] lines = new String[0];
        float[] lineWidths = new float[0];
        float bodySize, bodyY, bodyLineHeight, bodyCenterX;
        String typeLabel;
        float labelWidth, labelY;
    }

    private static final class EdgeVisual {
        FlowModel.Edge edge;
        NodeVisual from, to, peer;
        long sourceGeneration = -1, targetGeneration = -1, corridorGeneration = -1;
        String branch, routeFrom, routeTo, rawLabel, label, colorString;
        long peerGeneration=-1, routingGeneration=-1;
        int color;
        final Path path = new Path();
        final Path arrow = new Path(),motionPath=new Path();
        final PathMeasure motionMeasure=new PathMeasure();
        final RectF bounds = new RectF();
        final RectF labelBounds = new RectF();
        float[] points = new float[0];
        float labelWidth;
        boolean corridor;
        float corridorTop,corridorBottom;
    }

    private static final class DragOrigin {
        final FlowModel.Node node;
        final float x, y;
        DragOrigin(FlowModel.Node node) { this.node = node; x = node.x; y = node.y; }
    }
    private static final class InsertionRoute {
        BranchRoutingContext context;String id,from,to,branch;long generation=-1;float[] points;Path path;final Path prefix=new Path(),arrow=new Path();final PathMeasure measure=new PathMeasure();
        InsertionRoute(EdgeVisual edge,BranchRoutingContext context){this.context=context;id=edge.edge.id;from=edge.edge.from;to=edge.edge.to;branch=edge.edge.branch;points=edge.points.clone();path=new Path(edge.path);measure.setPath(path,false);buildArrow(arrow,points);}
    }

    /** A chooser previews the release pose without changing the saved/undo document. */
    private static final class PendingPlacement {
        final FlowModel.Document expected;final String id;final Map<String,float[]> positions;final boolean detached;
        PendingPlacement(FlowModel.Document expected,String id,Map<String,float[]> positions,boolean detached){this.expected=expected;this.id=id;this.positions=positions;this.detached=detached;}
    }
    private static final class EdgeSnapshot {
        final String id,from,to,branch;final Path path;final int color;
        EdgeSnapshot(EdgeVisual e){id=e.edge.id;from=e.edge.from;to=e.edge.to;branch=e.edge.branch;path=new Path(e.path);color=e.label==null?0xFF9099AA:e.color;}
    }

    private static final class LayoutPosition {
        final float fromX, fromY, toX, toY;
        LayoutPosition(float[] from, float[] to) {
            fromX = from[0]; fromY = from[1]; toX = to[0]; toY = to[1];
        }
    }

    private FlowCycleFold cycleFold;
    private FlowModel.Document cycleFoldDocument;
    private FlowCycleFold.Projection foldProjection;
    private boolean foldModelDirty=true;
    private android.graphics.Bitmap foldGhost;
    private long foldGhostAt;
    private final Paint foldGhostPaint=new Paint(Paint.ANTI_ALIAS_FLAG);
    private void ensureFoldModel(boolean force){
        if(document==null)return;
        if(cycleFoldDocument!=document){cycleFoldDocument=document;cycleFold=new FlowCycleFold(document,sizing);foldModelDirty=false;}
        else if(foldModelDirty||force){cycleFold.update(force);foldModelDirty=false;}
        foldProjection=cycleFold.current;
    }
    private FlowModel.Document canvasScene(){
        if(document==null||readOnly||modelGeometry)return document;
        if(cycleFoldDocument!=document){boolean folded=false;for(FlowModel.Node n:document.nodes)folded|=n.foldedCycle;if(!folded){foldProjection=null;return document;}}
        ensureFoldModel(false);if(foldProjection==null)return document;foldProjection.sync();return foldProjection.scene;
    }
    private String foldOwner(String id){return foldProjection==null||modelGeometry?id:foldProjection.owner(id);}
    private boolean isFoldCard(String id){return !readOnly&&!modelGeometry&&id!=null&&foldProjection!=null&&foldProjection.cards.containsKey(id);}
    private java.util.Set<String> foldSelection(java.util.Collection<String> ids){return foldProjection==null?new java.util.LinkedHashSet<>(ids):foldProjection.selection(ids);}
    private FlowCycle.Info selectedCycle(){ensureFoldModel(false);FlowCycle.Info best=null;if(selected.isEmpty())return null;for(FlowCycle.Info c:cycleFold.cycles)if(c.members.containsAll(selected)&&(best==null||c.members.size()<best.members.size()))best=c;return best;}
    public boolean canFoldSelection(){return !readOnly&&selectedCycle()!=null;}
    public boolean selectionCycleFolded(){FlowCycle.Info c=selectedCycle();return c!=null&&document.find(c.check).foldedCycle;}
    public void toggleSelectedCycle(){FlowCycle.Info c=selectedCycle();if(c!=null)changeFold(()->cycleFold.set(c.check,!document.find(c.check).foldedCycle),!document.find(c.check).foldedCycle);}
    public void foldAllCycles(boolean folded){changeFold(()->cycleFold.all(folded),folded);}
    private void revealFold(String id){if(foldProjection!=null&&foldProjection.owners.containsKey(id))changeFold(()->cycleFold.reveal(id),false);}
    private java.util.Map<String,float[]> presentationPositions(){ensureCache();java.util.Map<String,float[]> result=new java.util.HashMap<>();for(NodeVisual n:nodes)result.put(n.node.id,new float[]{n.bounds.left,n.bounds.top});return result;}
    private boolean changeFold(java.util.function.BooleanSupplier change,boolean closing){
        if(readOnly||document==null||pendingPlacement!=null||(gesture!=NONE&&gesture!=PENDING))return false;
        ensureCache();ensureFoldModel(false);java.util.Map<String,float[]> before=presentationPositions();FlowCycleFold.Projection old=foldProjection;
        finishLayoutAnimation();captureFoldGhost();if(!change.getAsBoolean()){stopFoldGhost();return false;}foldProjection=cycleFold.current;foldModelDirty=false;
        FlowModel.Document scene=foldProjection==null?document:foldProjection.scene;
        for(FlowModel.Node n:scene.nodes)if(!before.containsKey(n.id)&&old!=null){float[] at=before.get(old.owner(n.id));if(at!=null)before.put(n.id,at.clone());}
        dirty=structureDirty=true;applySelection(foldSelection(selected),false);ensureCache();animatePresentation(before,420);
        if(listener!=null)listener.onCyclePresentationChanged();hint(closing?"Cicli ridotti · trascina la scheda per spostare tutto il ciclo; doppio tocco per espandere":"Ciclo espanso · le istruzioni originali sono sul foglio");return true;
    }
    private void animatePresentation(java.util.Map<String,float[]> before,long duration){
        if(!canAnimate()){stopFoldGhost();dirty=true;invalidate();return;}ensureCache();layoutPositions.clear();
        for(NodeVisual n:nodes){float[] from=before.get(n.node.id);if(validPosition(from))layoutPositions.put(n.node.id,new LayoutPosition(from,new float[]{n.node.x,n.node.y}));}
        layoutFraction=0;dirty=true;final ValueAnimator animation=ValueAnimator.ofFloat(0,1);layoutAnimator=animation;animation.setDuration(duration);animation.setInterpolator(Ui.SPRING);
        animation.addUpdateListener(value->{layoutFraction=(float)value.getAnimatedValue();dirty=true;invalidate();});
        animation.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator ended){if(layoutAnimator!=animation)return;layoutAnimator=null;layoutPositions.clear();layoutFraction=1;stopFoldGhost();dirty=true;invalidate();}});animation.start();
    }
    private void stopFoldGhost(){if(foldGhost!=null){foldGhost.recycle();foldGhost=null;}}
    private void captureFoldGhost(){stopFoldGhost();if(!canAnimate()||getWidth()<1||getHeight()<1||(long)getWidth()*getHeight()>8000000)return;
        foldGhost=android.graphics.Bitmap.createBitmap(getWidth(),getHeight(),android.graphics.Bitmap.Config.ARGB_8888);Canvas ghost=new Canvas(foldGhost);ghost.translate(panX,panY);ghost.scale(scale(),scale());drawDiagram(ghost,dark,true,null);foldGhostAt=android.os.SystemClock.uptimeMillis();
    }
    private void drawFoldGhost(Canvas target){if(foldGhost==null)return;float t=Math.min(1,(android.os.SystemClock.uptimeMillis()-foldGhostAt)/420f),p=Ui.spring(t),factor=1-.035f*p;int state=target.save();target.translate(getWidth()*.5f,getHeight()*.5f-12*p);target.scale(factor,factor);foldGhostPaint.setAlpha((int)(255*(1-t*t)));target.drawBitmap(foldGhost,-foldGhost.getWidth()*.5f,-foldGhost.getHeight()*.5f,foldGhostPaint);target.restoreToCount(state);}

    public FlowCanvasView(Context context) {
        super(context);
        density = getResources().getDisplayMetrics().density;
        touchSlop = ViewConfiguration.get(context).getScaledTouchSlop();
        strokePaint.setStyle(Paint.Style.STROKE);
        strokePaint.setStrokeJoin(Paint.Join.ROUND);
        strokePaint.setStrokeCap(Paint.Cap.ROUND);
        labelPaint.setTypeface(MEDIUM_TYPEFACE);
        textPaint.setTypeface(BODY_TYPEFACE);
        gridPaint.setStrokeCap(Paint.Cap.ROUND);
        setFocusable(true);
        setFocusableInTouchMode(true);
        setClickable(true);
        setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_YES);
        motionReady = true;
        refreshSystemMotion();
        updateContentDescription();
    }

    public void setListener(Listener listener) { this.listener = listener; }
    private boolean navigationPreview;
    private final java.util.Map<String,String> navigationCaptions=new java.util.HashMap<>();
    void setNavigationPreview(boolean enabled){navigationPreview=enabled;}
    void replaceNavigationGraph(FlowCycleView.Projection next,FlowCycleView.Projection previous,boolean animate){
        ensureCache();java.util.Map<String,float[]> before=new java.util.HashMap<>(),sizes=new java.util.HashMap<>();for(NodeVisual n:nodes){before.put(n.node.id,new float[]{n.bounds.left,n.bounds.top});sizes.put(n.node.id,new float[]{n.bounds.width(),n.bounds.height()});}
        setDocument(next.graph,false);navigationCaptions.clear();for(java.util.Map.Entry<String,FlowCycle.Info> e:next.cards.entrySet())navigationCaptions.put(e.getKey(),FlowCycleView.caption(e.getValue()));
        for(FlowModel.Node n:next.graph.nodes)if(!before.containsKey(n.id)&&previous!=null){String owner=previous.owner.get(n.id);if(owner!=null&&before.containsKey(owner))before.put(n.id,before.get(owner).clone());}
        if(animate){arrangeAnimated(before);animateSizesFrom(sizes);}else fitToContent();
    }
    public FlowModel.Document getDocument() { return document; }
    public Set<String> getSelectedIds() { return new LinkedHashSet<>(selected); }
    public boolean isSelectionMode() { return selectionMode; }
    public boolean isConnecting() { return connectionSource != null; }
    public boolean isReadOnly() { return readOnly; }
    public boolean isMotionEnabled() { return motionEnabled; }

    /** App preference; Android's animator setting and view visibility still apply. */
    public void setMotionEnabled(boolean enabled) {
        if (motionEnabled == enabled) return;
        motionEnabled = enabled;
        if (!canAnimate()) stopAllMotion(true);
        syncConnectorPulse();
        invalidate();
    }

    /** A readable, content-bounded camera. Model positions remain unrestricted. */
    public void setMobileViewport(boolean enabled) {
        if (mobileViewport == enabled) return;
        cancelCamera(false);
        mobileViewport = enabled;
        if (enabled && getWidth() > 0 && getHeight() > 0) {
            if (zoom < mobileReadingZoom()) fitForReading();
            else constrainViewport();
        }
        invalidate();
    }

    public void focusNodeAnimated(String nodeId) {
        focusNodeAnimated(nodeId,true);
    }

    /** Camera navigation must not enter edit mode or replace the main actions. */
    public void frameNodeAnimated(String nodeId) {
        focusNodeAnimated(nodeId,false);
    }

    private void focusNodeAnimated(String nodeId,boolean selectNode) {
        FlowModel.Node node = document.find(nodeId);
        if (node == null) return;
        if(selectNode)select(nodeId);
        ensureCache();if(foldProjection!=null&&!modelGeometry){FlowModel.Node visual=foldProjection.visual(nodeId);if(visual!=null)node=visual;}
        float targetZoom = Math.max(mobileViewport ? mobileReadingZoom() : 0.78f, Math.min(zoom, 1.3f));
        float targetY = mobileViewport && node.kind == FlowModel.START ? dp(16) - node.y * targetZoom * density :
                focusScreenY() - (node.y + height(node) / 2) * targetZoom * density;
        animateCamera(targetZoom,
                getWidth() / 2f - (node.x + width(node) / 2) * targetZoom * density,
                targetY, 260);
    }

    /**
     * The document already contains the final positions. Only cached geometry is
     * interpolated; history, saving and all export formats see final coordinates.
     */
    public void animateLayout(Map<String, float[]> before, Map<String, float[]> after, long durationMs) {
        finishLayoutAnimation();
        clearRunAnimation();
        if (before == null || after == null || !canAnimate() || durationMs <= 0) { dirty = true; invalidate(); return; }
        boolean largeDiagram = document.nodes.size() > 150 || document.edges.size() > 400;
        for (FlowModel.Node node : document.nodes) {
            float[] from = before.get(node.id), to = after.get(node.id);
            if (!validPosition(from) || !validPosition(to) || (from[0] == to[0] && from[1] == to[1])) continue;
            // An older asynchronous layout must never animate over a newer edit.
            if (node.x != to[0] || node.y != to[1]) continue;
            if (largeDiagram) { animateLargeLayout(durationMs); return; }
            layoutPositions.put(node.id, new LayoutPosition(from, to));
        }
        if (layoutPositions.isEmpty()) return;
        layoutFraction = 0; dirty = true;
        final ValueAnimator animator = ValueAnimator.ofFloat(0, 1);
        layoutAnimator = animator;
        animator.setDuration(Math.max(80, Math.min(1200, durationMs)));
        animator.setInterpolator(Ui.SPRING);
        animator.addUpdateListener(value -> {
            layoutFraction = (float) value.getAnimatedValue(); dirty = true; invalidate();
        });
        animator.addListener(new AnimatorListenerAdapter() {
            @Override public void onAnimationEnd(Animator animation) {
                if (layoutAnimator != animator) return;
                layoutAnimator = null; layoutPositions.clear(); layoutFraction = 1; dirty = true; invalidate();
            }
        });
        animator.start(); invalidate();
    }

    private void animateLargeLayout(long durationMs) {
        // Hundreds of moving routes would need rebuilding even outside the
        // viewport. A short fade reuses final paths and the normal draw culling.
        layoutOpacity = 0.5f; layoutFraction = 1; dirty = true;
        final ValueAnimator animator = ValueAnimator.ofFloat(0, 1);
        layoutAnimator = animator;
        animator.setDuration(Math.max(80, Math.min(160, durationMs)));
        animator.setInterpolator(new DecelerateInterpolator());
        animator.addUpdateListener(value -> { layoutOpacity = 0.5f + 0.5f * (float) value.getAnimatedValue(); invalidate(); });
        animator.addListener(new AnimatorListenerAdapter() {
            @Override public void onAnimationEnd(Animator animation) {
                if (layoutAnimator != animator) return;
                layoutAnimator = null; layoutOpacity = 1; invalidate();
            }
        });
        animator.start(); invalidate();
    }

    /** Animates an exact executed edge, including a decision's chosen branch. */
    public void animateRunStep(String fromId, String toId, String branch, long durationMs) {
        clearRunAnimation();
        finishLayoutAnimation();
        FlowModel.Node target = document.find(toId);
        if (target == null) return;
        highlightId = toId;
        ensureCache();
        EdgeVisual matching = null;
        boolean ambiguous = false;
        for (EdgeVisual edge : edges) {
            if (!same(edge.edge.from, fromId) || !same(edge.edge.to, toId)) continue;
            if (branch != null && !same(edge.branch, branch)) continue;
            if (matching != null) { ambiguous = true; break; }
            matching = edge;
        }
        if (!ambiguous && matching != null) {
            runPath.set(matching.path); runMeasure.setPath(runPath, false);
            runLength = runMeasure.getLength();
            runColor = matching.label == null ? ACCENT : matching.color;
        }
        if (mobileViewport && getWidth() > 0 && getHeight() > 0) {
            float x = screenX(target.x + width(target) / 2);
            float top = screenY(target.y), bottom = screenY(target.y + height(target));
            if (x < dp(16) || x > getWidth() - dp(16) || top < dp(16) || bottom > getHeight() - dp(16)) {
                // Follow the executed node by panning; preserve a zoom the user
                // selected while running instead of re-zooming at every step.
                float targetZoom = zoom;
                animateCamera(targetZoom,
                        getWidth() / 2f - (target.x + width(target) / 2) * targetZoom * density,
                        focusScreenY() - (target.y + height(target) / 2) * targetZoom * density, 280);
                cameraForRun = cameraAnimator != null;
            }
        }
        if (!canAnimate() || durationMs <= 0) { runLength = 0; invalidate(); return; }
        runDestination = toId; runFraction = 0;
        final ValueAnimator animator = ValueAnimator.ofFloat(0, 1);
        runAnimator = animator;
        animator.setDuration(Math.max(80, Math.min(3000, durationMs)));
        animator.setInterpolator(new LinearInterpolator());
        animator.addUpdateListener(value -> { runFraction = (float) value.getAnimatedValue(); invalidate(); });
        animator.addListener(new AnimatorListenerAdapter() {
            @Override public void onAnimationEnd(Animator animation) {
                if (runAnimator != animator) return;
                runAnimator = null; runDestination = null; runLength = 0; runProgressPath.reset(); invalidate();
            }
        });
        animator.start(); invalidate();
    }

    public void clearRunAnimation() {
        if (cameraForRun) cancelCamera(false);
        ValueAnimator animator = runAnimator; runAnimator = null;
        if (animator != null) { animator.removeAllUpdateListeners(); animator.removeAllListeners(); animator.cancel(); }
        boolean hadMotion = runDestination != null || runLength != 0;
        runDestination = null; runLength = 0; runFraction = 0;
        runPath.reset(); runProgressPath.reset(); runMeasure.setPath(null, false);
        if (hadMotion) invalidate();
    }

    /** Commits a drop after the host has chosen its branch and resolved any replacement. */
    public void completeConnectionDrop(String sourceId, String branch, String targetId, float centerX, float centerY) {
        if (readOnly || !Float.isFinite(centerX) || !Float.isFinite(centerY)) return;
        FlowModel.Node source = document.find(sourceId);
        if (source == null || (source.kind == FlowModel.END||source.kind==FlowModel.NOTE)) return;
        if (targetId != null && document.find(targetId) == null) return;
        if(targetId!=null&&document.find(targetId).kind==FlowModel.NOTE){hint("Le note non ricevono collegamenti.");return;}
        if (targetId != null && (document.find(targetId).kind == FlowModel.START||document.find(targetId).kind==FlowModel.NOTE)) {
            hint("Il blocco Inizio non può ricevere una freccia."); return;
        }
        beginConnection(sourceId, branch);
        completeConnection(targetId, clamp(centerX, -MAX_COORDINATE, MAX_COORDINATE),
                clamp(centerY, -MAX_COORDINATE, MAX_COORDINATE));
    }

    private static boolean validPosition(float[] value) {
        return value != null && value.length >= 2 && Float.isFinite(value[0]) && Float.isFinite(value[1]);
    }

    private boolean canAnimate() {
        return motionReady && motionEnabled && systemMotionEnabled && isEnabled() && ValueAnimator.areAnimatorsEnabled()
                && isAttachedToWindow() && isShown() && getWindowVisibility() == VISIBLE;
    }

    private void refreshSystemMotion() {
        if (!motionReady) return;
        try { systemMotionEnabled = Settings.Global.getFloat(getContext().getContentResolver(),
                Settings.Global.ANIMATOR_DURATION_SCALE, 1f) > 0; }
        catch (RuntimeException ignored) { systemMotionEnabled = ValueAnimator.areAnimatorsEnabled(); }
        if (!canAnimate()) stopAllMotion(true);
        syncConnectorPulse();
    }

    private String connectorPulseFor;
    private void stopConnectorPulse() {
        ValueAnimator animator = connectorAnimator; connectorAnimator = null;
        if (animator != null) { animator.removeAllUpdateListeners(); animator.cancel(); }
        connectorPhase = 0;connectorPulseFor=null;
    }

    private void syncConnectorPulse() {
        if (!motionReady) return;
        boolean eligible = canAnimate() && hasWindowFocus() && plusNodeId != null && !readOnly
                && selected.size() == 1 && !isConnecting()
                && (gesture == NONE || gesture == PENDING || gesture == PLUS_PENDING);
        if (!eligible) { stopConnectorPulse(); return; }
        // The draw path calls this method too. Remember the selected port after
        // completion so a new frame cannot restart an endless idle animation.
        if (connectorAnimator != null || plusNodeId.equals(connectorPulseFor)) return;
        connectorPulseFor=plusNodeId;
        connectorAnimator = ValueAnimator.ofFloat(0, 1);
        final ValueAnimator pulse=connectorAnimator;
        connectorAnimator.setDuration(900);
        connectorAnimator.setInterpolator(new LinearInterpolator());
        connectorAnimator.addUpdateListener(value -> { connectorPhase = (float) value.getAnimatedValue(); invalidate(); });
        connectorAnimator.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator animation){if(connectorAnimator==pulse){connectorAnimator=null;connectorPhase=0;invalidate();}}});
        connectorAnimator.start();
    }

    private void finishLayoutAnimation() {
        stopFoldGhost();
        ValueAnimator animator = layoutAnimator; layoutAnimator = null;
        if (animator != null) { animator.removeAllUpdateListeners(); animator.removeAllListeners(); animator.cancel(); }
        if (!layoutPositions.isEmpty()) { layoutPositions.clear(); layoutFraction = 1; dirty = true; invalidate(); }
        if (layoutOpacity != 1) { layoutOpacity = 1; invalidate(); }
    }

    private void cancelCamera(boolean settle) {
        cameraForRun = false;
        ValueAnimator animator = cameraAnimator; cameraAnimator = null;
        if (animator == null) return;
        animator.removeAllUpdateListeners(); animator.removeAllListeners(); animator.cancel();
        if (settle) { zoom = cameraEndZoom; panX = cameraEndX; panY = cameraEndY; constrainViewport(); invalidate(); }
    }

    private void stopAllMotion(boolean settleCamera) {stopAppearance();
        stopSizing(); stopConnectorPulse(); clearRunAnimation(); finishLayoutAnimation(); stopEdgeMotion(); stopTrashMotion(); cancelCamera(settleCamera);
    }

    private void animateCamera(float targetZoom, float targetX, float targetY, long durationMs) {
        cancelCamera(false);
        final float fromZoom = zoom, fromX = panX, fromY = panY;
        zoom = clamp(targetZoom, minimumZoom(), MAX_ZOOM); panX = targetX; panY = targetY;
        constrainViewport();
        cameraEndZoom = zoom; cameraEndX = panX; cameraEndY = panY;
        pendingFit = false;
        if (!canAnimate() || durationMs <= 0 || (fromZoom == zoom && fromX == panX && fromY == panY)) { invalidate(); return; }
        zoom = fromZoom; panX = fromX; panY = fromY;
        final ValueAnimator animator = ValueAnimator.ofFloat(0, 1);
        cameraAnimator = animator;
        animator.setDuration(Math.max(80, Math.min(600, durationMs)));
        animator.setInterpolator(Ui.SPRING);
        animator.addUpdateListener(value -> {
            float progress = (float) value.getAnimatedValue();
            zoom = clamp(fromZoom + (cameraEndZoom - fromZoom) * progress,FlowViewport.ABSOLUTE_MIN_ZOOM,MAX_ZOOM);
            panX = fromX + (cameraEndX - fromX) * progress;
            panY = fromY + (cameraEndY - fromY) * progress;
            invalidate();
        });
        animator.addListener(new AnimatorListenerAdapter() {
            @Override public void onAnimationEnd(Animator animation) {
                if (cameraAnimator != animator) return;
                cameraAnimator = null; cameraForRun = false; zoom = cameraEndZoom; panX = cameraEndX; panY = cameraEndY; invalidate();
            }
        });
        animator.start();
    }

    /** Keeps navigation available while the activity is simulating the diagram. */
    public void setReadOnly(boolean readOnly) {
        if (this.readOnly == readOnly) return;
        resetGesture(true);
        lastTapTime = 0;
        this.readOnly = readOnly;
        cancelConnection();
        stopConnectorPulse();
        getParentDisallow(false);
        updateContentDescription();
        invalidate();
        sendAccessibilityEvent(AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED);
    }

    private void updateContentDescription() {
        setContentDescription(readOnly ? "Anteprima in sola lettura. Trascina il foglio e usa due dita per lo zoom." :
                "Foglio del diagramma. Tocca un blocco per selezionarlo; due tocchi per modificarlo. " +
                        "Il più aggiunge un blocco; trascinalo su un blocco esistente per collegarlo. " +
                        "Una decisione ha due più ai lati, Sì e No: trascina il più del ramo sul blocco di arrivo. " +
                        "Trascina anche la punta di una freccia per cambiarne l’arrivo. " +
                        "Trascina un blocco su una freccia: l’alone blu mostra dove puoi inserirlo. " +
                        "Tieni premuto un blocco per riempirlo e staccarlo. Dal menu tieni premuto e trascina una nuova casella sulla freccia. " +
                        "Trascina il foglio e usa due dita per lo zoom.");
    }

    public void setDocument(FlowModel.Document document, boolean fit) {
        clearPendingPlacement();
        stopAllMotion(false);
        resetGesture(false);stopLanding();
        boolean wasConnecting = isConnecting();
        if(this.document!=document){cycleFold=null;cycleFoldDocument=null;foldProjection=null;}foldModelDirty=true;
        this.document = document == null ? new FlowModel.Document() : document;
        connectionSource = connectionBranch = null;
        highlightId = null;
        selected.clear();
        nodes.clear(); nodesByTop.clear(); edges.clear(); nodeIndex.clear(); edgeIndex.clear();
        accessibilityFocus = hoverVirtualId = -1;keyboardNodeId=null;keyboardConsumedKeys.clear();
        dirty = structureDirty = true;
        if (wasConnecting && listener != null) listener.onConnectionStateChanged(null, null);
        selectionChanged();
        if (fit) {
            pendingReadingFit = true;
            if (getWidth() == 0 || getHeight() == 0) pendingFit = true;
            else fitForReading();
        }
        else invalidate();
        sendAccessibilityEvent(AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED);
    }

    public void setDark(boolean dark) {
        if (this.dark == dark) return;
        this.dark = dark;
        invalidate();
    }

    public void selectGroup(java.util.Collection<String> ids){if(readOnly)return;resetGesture(true);cancelConnection();java.util.LinkedHashSet<String> existing=new java.util.LinkedHashSet<>();for(String id:ids)if(document.find(id)!=null)existing.add(id);applySelection(existing,true);invalidate();}
    public void select(String nodeId) {
        ensureCache();revealFold(nodeId);
        LinkedHashSet<String> next = new LinkedHashSet<>();
        if (document.find(nodeId) != null) next.add(nodeId);
        applySelection(next, true);
        FlowModel.Node node=document.find(nodeId);
        if(!readOnly && node!=null && node.kind==FlowModel.DECISION)hint("Sì a sinistra · No a destra. Trascina un + sul blocco di arrivo, oppure toccalo per aggiungere un passaggio.");
    }

    public void clearSelection() {
        if (selected.isEmpty()) return;
        selected.clear();
        selectionChanged();
    }

    public void setSelectionMode(boolean enabled) {
        if (readOnly && enabled) return;
        if (selectionMode == enabled) return;
        resetGesture(true);
        selectionMode = enabled;
        if (enabled){cancelConnection();hint("Tocca le caselle per selezionarle · tieni premuto il gruppo per staccarlo · trascinalo sulla freccia blu per inserirlo");}
        stopConnectorPulse();
        invalidate();
    }

    public void focusNode(String nodeId) {
        FlowModel.Node node = document.find(nodeId);
        if (node == null) return;
        cancelCamera(false);
        finishLayoutAnimation();
        ensureCache();
        NodeVisual visual = nodeIndex.get(nodeId);
        if (visual == null) return;
        zoom = Math.max(mobileViewport ? mobileReadingZoom() : 0.78f, Math.min(zoom, 1.3f));
        float scale = scale();
        panX = getWidth() / 2f - visual.bounds.centerX() * scale;
        panY = (mobileViewport ? focusScreenY() : getHeight() / 2f) - visual.bounds.centerY() * scale;
        constrainViewport();
        pendingFit = false;
        select(nodeId);
        invalidate();
    }

    public void beginConnection(String sourceId, String branch) {
        if (readOnly) return;
        FlowModel.Node source = document.find(sourceId);
        if (source == null) return;
        if ((source.kind == FlowModel.END||source.kind==FlowModel.NOTE)) { hint("Il blocco Fine non ha un’uscita."); return; }
        resetGesture(true);
        selectionMode = false;
        connectionSource = sourceId;
        connectionBranch = source.kind == FlowModel.DECISION ?
                ("false".equals(branch) ? "false" : "true") : "next";
        stopConnectorPulse();
        select(sourceId);
        if (listener != null) listener.onConnectionStateChanged(connectionSource, connectionBranch);
        invalidate();
    }

    public void cancelConnection() {
        if (!isConnecting()) return;
        connectionSource = connectionBranch = null;
        if (listener != null) listener.onConnectionStateChanged(null, null);
        invalidate();
    }

    public void deleteSelection() {
        if (readOnly) return;
        resetGesture(true);
        if (selected.isEmpty()) return;
        HashSet<String> deleting = new HashSet<>(selected);
        boolean found = false;
        for (FlowModel.Node node : document.nodes) if (deleting.contains(node.id)) { found = true; break; }
        if (!found) { clearSelection(); return; }
        beforeChange();
        for (Iterator<FlowModel.Edge> it = document.edges.iterator(); it.hasNext();) {
            FlowModel.Edge edge = it.next();
            if (deleting.contains(edge.from) || deleting.contains(edge.to)) it.remove();
        }
        for (Iterator<FlowModel.Node> it = document.nodes.iterator(); it.hasNext();) {
            if (deleting.contains(it.next().id)) it.remove();
        }
        if (deleting.contains(connectionSource)) cancelConnection();
        if (deleting.contains(highlightId)) highlightId = null;
        selected.clear();
        dirty = structureDirty = true;
        selectionChanged();
        changed(deleting.size() == 1 ? "Elimina blocco" : "Elimina blocchi");
    }

    public void duplicateSelection() {
        if (readOnly) return;
        resetGesture(true);
        ensureCache();
        ArrayList<FlowModel.Node> originals = new ArrayList<>();
        for (FlowModel.Node node : document.nodes) if (selected.contains(node.id)) originals.add(node);
        if (originals.isEmpty()) return;
        if (document.nodes.size() + otherNodes.getAsInt() + originals.size() > FlowModel.MAX_NODES) {
            hint("Il diagramma può contenere al massimo " + FlowModel.MAX_NODES + " blocchi."); return;
        }
        ArrayList<FlowModel.Edge> internalEdges = new ArrayList<>();
        for (FlowModel.Edge edge : document.edges) {
            if (selected.contains(edge.from) && selected.contains(edge.to)) internalEdges.add(edge);
        }
        if (document.edges.size() + otherEdges.getAsInt() + internalEdges.size() > FlowModel.MAX_EDGES) {
            hint("Il diagramma ha raggiunto il limite di collegamenti."); return;
        }
        HashMap<String, String> idMap = new HashMap<>();
        ArrayList<FlowModel.Node> copies = new ArrayList<>();
        float dx = 28, dy = 28;
        for (FlowModel.Node node : originals) { dx = Math.min(dx, MAX_COORDINATE - node.x); dy = Math.min(dy, MAX_COORDINATE - node.y); }
        for (FlowModel.Node original : originals) {
            FlowModel.Node copy = original.copy();
            copy.id = FlowModel.id(); copy.x += dx; copy.y += dy;
            idMap.put(original.id, copy.id); copies.add(copy);
        }
        ArrayList<FlowModel.Edge> copiedEdges = new ArrayList<>();
        for (FlowModel.Edge original : internalEdges) {
            FlowModel.Edge copy = original.copy();
            copy.id = FlowModel.id(); copy.from = idMap.get(original.from); copy.to = idMap.get(original.to);
            copiedEdges.add(copy);
        }
        beforeChange();
        document.nodes.addAll(copies); document.edges.addAll(copiedEdges);
        selected.clear(); for (FlowModel.Node copy : copies) selected.add(copy.id);
        dirty = structureDirty = true;
        selectionChanged();
        changed(copies.size() == 1 ? "Duplica blocco" : "Duplica blocchi");
    }

    /** Call after a model edit performed by the activity or the interpreter. */
    public void notifyModelChanged() {
        foldModelDirty=true;
        animateSizing();
        finishLayoutAnimation();
        clearRunAnimation();
        dirty = structureDirty = true;
        boolean selectionAltered = false;
        for (Iterator<String> it = selected.iterator(); it.hasNext();) {
            if (document.find(it.next()) == null) { it.remove(); selectionAltered = true; }
        }
        if (connectionSource != null && document.find(connectionSource) == null) cancelConnection();
        if (highlightId != null && document.find(highlightId) == null) highlightId = null;
        if (selectionAltered) selectionChanged();
        invalidate();
        sendAccessibilityEvent(AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED);
    }

    public void setHighlight(String nodeId) {
        if (nodeId == null) clearRunAnimation();
        if (same(highlightId, nodeId)) return;
        highlightId = nodeId;
        invalidate();
    }

    public void zoomBy(float factor) {
        if (!Float.isFinite(factor) || factor <= 0) return;
        resetGesture(true);
        cancelCamera(false);
        zoomAt(zoom * factor, getWidth() / 2f, getHeight() / 2f);
    }

    public void arrangeAnimated(Map<String,float[]> before){
        boolean folded=foldProjection!=null;if(folded){Map<String,float[]> display=new HashMap<>();for(Map.Entry<String,float[]> value:before.entrySet()){float[] offset=foldProjection.offsets.get(value.getKey());display.put(value.getKey(),offset==null?value.getValue():new float[]{value.getValue()[0]+offset[0],value.getValue()[1]+offset[1]});}before=display;}
        if(cycleFold!=null)ensureFoldModel(true);
        resetGesture(true);cancelCamera(false);finishLayoutAnimation();dirty=true;ensureCache();
        FlowViewport.Fit fit=viewportFit();if(folded)animatePresentation(before,360);else animateLayout(before,FlowPlacement.positions(document),360);
        pendingFit=false;pendingReadingFit=false;animateCamera(fit.zoom,fit.panX,fit.panY,360);
    }
    public void fitToContent() {
        cancelCamera(false);
        finishLayoutAnimation();
        pendingReadingFit = false;
        if (getWidth() == 0 || getHeight() == 0) { pendingFit = true; return; }
        ensureCache();
        FlowViewport.Fit fit=viewportFit();
        zoom=fit.zoom;panX=fit.panX;panY=fit.panY;
        pendingFit = false;
        invalidate();
    }

    private void fitForReading() {
        fitToContent();
        float readingZoom=mobileViewport?mobileReadingZoom():0.78f;
        if (nodes.isEmpty() || zoom >= readingZoom) return;
        zoom = readingZoom;
        positionAtStart();
        constrainViewport();
        invalidate();
    }

    private void positionAtStart() {
        NodeVisual first = nodes.get(0);
        for (NodeVisual node : nodes) if (node.kind == FlowModel.START) { first = node; break; }
        panX = getWidth() / 2f - first.bounds.centerX() * scale();
        panY = dp(mobileViewport ? 16 : 46) - first.bounds.top * scale();
    }

    public float[] getViewportCenter() {
        return new float[] { worldX(getWidth() / 2f), worldY(getHeight() / 2f) };
    }

    /** Camera-only snapshot: zoom followed by model coordinates of the visible center. */
    public float[] captureViewport() {
        float[] center = getViewportCenter();
        return new float[] { zoom, center[0], center[1] };
    }

    /** Restores navigation context without changing nodes, selection or edit history. */
    public boolean restoreViewport(float[] state) {
        if (state == null || state.length != 3 || getWidth() <= 0 || getHeight() <= 0
                || isInteracting() || !Float.isFinite(state[0]) || state[0] <= 0
                || !Float.isFinite(state[1]) || !Float.isFinite(state[2])) return false;
        float restoredZoom = clamp(state[0], minimumZoom(), MAX_ZOOM);
        float restoredX = getWidth() / 2f - state[1] * restoredZoom * density;
        float restoredY = getHeight() / 2f - state[2] * restoredZoom * density;
        if (!Float.isFinite(restoredX) || !Float.isFinite(restoredY)) return false;
        stopAllMotion(false);
        zoom = restoredZoom; panX = restoredX; panY = restoredY;
        pendingFit = false; pendingReadingFit = false;
        constrainViewport(); invalidate();
        return true;
    }

    public RectF getDiagramBounds() {
        boolean previous = beginModelGeometry();
        try { return new RectF(diagramBounds); }
        finally { endModelGeometry(previous); }
    }

    /** Draws with the caller's transform, in desktop/model units, always light. */
    public void drawForExport(Canvas canvas) {
        boolean previous = beginModelGeometry(),previousMono=monochromeExport;monochromeExport=monochromeExport||appearance.mode==2;
        try { drawDiagram(canvas, false, false, null); }
        finally { monochromeExport=previousMono;endModelGeometry(previous); }
    }
    /** Convert colors before recording PDF drawing commands; text and paths stay vector. */
    public void drawForPrint(Canvas canvas,boolean monochrome) {
        boolean previous=monochromeExport;monochromeExport=monochrome;
        try {drawForExport(canvas);}finally{monochromeExport=previous;}
    }

    private boolean beginModelGeometry() {
        boolean previous = modelGeometry;
        if (!modelGeometry) { modelGeometry = true; dirty = true; }
        ensureCache();
        return previous;
    }

    private void endModelGeometry(boolean previous) {
        if (modelGeometry != previous) { modelGeometry = previous; dirty = true; }
    }

    @Override protected void onSizeChanged(int width, int height, int oldWidth, int oldHeight) {
        super.onSizeChanged(width, height, oldWidth, oldHeight);
        if(gesture==EDGE_DRAG||gesture==EDGE_PENDING||gesture==CONNECT_DRAG||gesture==PLUS_PENDING)resetGesture(false);
        cancelCamera(false);
        if (pendingFit || oldWidth == 0 || oldHeight == 0) {
            if (pendingReadingFit) fitForReading(); else fitToContent();
        }
        else {
            panX += (width - oldWidth) / 2f;
            panY += (height - oldHeight) / 2f;
            constrainViewport();
        }
    }

    @Override protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        ensureCache();
        canvas.drawColor(appearance.neutral(dark ? DARK_PAPER : LIGHT_PAPER));
        drawGrid(canvas);
        if (nodes.isEmpty()) drawEmptyState(canvas);
        visibleWorld.set(worldX(-dp(50)), worldY(-dp(50)), worldX(getWidth() + dp(50)), worldY(getHeight() + dp(50)));
        int save = canvas.save();
        canvas.translate(panX, panY);
        canvas.scale(scale(), scale());
        int layoutLayer = layoutOpacity < 1 ? canvas.saveLayerAlpha(visibleWorld, (int) (255 * layoutOpacity)) : -1;
        drawDiagram(canvas, dark, true, visibleWorld);
        if (layoutLayer >= 0) canvas.restoreToCount(layoutLayer);
        drawInsertionGlow(canvas);
        drawPaletteGhost(canvas);
        if (gesture == CONNECT_DRAG || gesture == EDGE_DRAG) drawConnectionDrag(canvas);
        if (gesture == MARQUEE) {
            fillPaint.setColor(0x285B8DEF); canvas.drawRect(marquee, fillPaint);
            strokePaint.setColor(ACCENT); strokePaint.setStrokeWidth(1.3f / zoom);
            canvas.drawRect(marquee, strokePaint);
        }
        canvas.restoreToCount(save);
        drawEdgeHandles(canvas);
        drawConnectionButton(canvas);
        if (gesture == CONNECT_DRAG || gesture == EDGE_DRAG) drawConnectionDropHint(canvas);
        drawInsertionHint(canvas);
        drawHoldHint(canvas);drawTrash(canvas);drawFoldGhost(canvas);
    }

    private void drawGrid(Canvas canvas) {
        float spacing = 24 * scale();
        while (spacing < dp(15)) spacing *= 2;
        int columns = (int) (getWidth() / spacing) + 2;
        int rows = (int) (getHeight() / spacing) + 2;
        int count = columns * rows * 2;
        if (gridPoints.length < count) gridPoints = new float[count + 128];
        float startX = positiveModulo(panX, spacing), startY = positiveModulo(panY, spacing);
        int offset = 0;
        for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++) {
            gridPoints[offset++] = startX + col * spacing;
            gridPoints[offset++] = startY + row * spacing;
        }
        gridPaint.setColor(dark ? 0xFF30323B : 0xFFDADDE6);
        gridPaint.setStrokeWidth(dp(1.2f));
        canvas.drawPoints(gridPoints, 0, offset, gridPaint);
    }

    private void drawEmptyState(Canvas canvas) {
        float x = getWidth() / 2f, y = getHeight() / 2f - dp(28);
        strokePaint.setColor(dark ? 0xFF536484 : 0xFFAFC1E0);
        strokePaint.setStrokeWidth(dp(1.8f));
        tempRect.set(x - dp(23), y - dp(44), x + dp(23), y - dp(24));
        canvas.drawRoundRect(tempRect, dp(10), dp(10), strokePaint);
        canvas.drawLine(x, y - dp(24), x, y - dp(10), strokePaint);
        tempRect.set(x - dp(32), y - dp(10), x + dp(32), y + dp(16));
        canvas.drawRoundRect(tempRect, dp(7), dp(7), strokePaint);
        canvas.drawLine(x, y + dp(16), x, y + dp(30), strokePaint);
        fillPaint.setColor(ACCENT); canvas.drawCircle(x, y + dp(35), dp(5), fillPaint);
        textPaint.setTypeface(MEDIUM_TYPEFACE);
        textPaint.setTextSize(dp(19)); textPaint.setColor(dark ? DARK_INK : LIGHT_INK);
        String title = "Un’idea, un blocco alla volta";
        canvas.drawText(title, x - textPaint.measureText(title) / 2, y + dp(80), textPaint);
        textPaint.setTypeface(BODY_TYPEFACE);
        textPaint.setTextSize(dp(13)); textPaint.setColor(dark ? DARK_MUTED : LIGHT_MUTED);
        String subtitle = "Tocca + per iniziare il tuo diagramma";
        canvas.drawText(subtitle, x - textPaint.measureText(subtitle) / 2, y + dp(104), textPaint);
    }

    private void drawDiagram(Canvas canvas, boolean useDark, boolean interactive, RectF visible) {
        if(interactive)drawOutgoingMotion(canvas);
        for (EdgeVisual edge : edges) {
            if ((!interactive||!skipStickerEdge(edge.edge))&&(visible == null || RectF.intersects(visible, edge.bounds))) drawEdge(canvas, edge, useDark, interactive);
        }
        if (interactive){drawStickerConnections(canvas);drawRunTrace(canvas);drawFusion(canvas);}
        for (NodeVisual node : nodes) {
            if (visible == null || RectF.intersects(visible, node.bounds)) drawNode(canvas, node, useDark, interactive);
        }
    }

    private void drawNode(Canvas canvas, NodeVisual visual, boolean useDark, boolean interactive) {
        boolean isSelected = interactive && selected.contains(visual.node.id);
        boolean isCurrent = interactive && same(foldOwner(highlightId), visual.node.id);
        boolean isSource = interactive && same(connectionSource, visual.node.id);
        boolean isDropHover = interactive && (gesture == CONNECT_DRAG || gesture == EDGE_DRAG) && same(dragConnectionTarget, visual.node.id);
        boolean isDropTarget = isDropHover && visual.kind != FlowModel.START && visual.kind != FlowModel.NOTE;
        boolean isInvalidTarget = isDropHover && !isDropTarget;
        RectF b = visual.bounds;
        int stickerSave=beginStickerPaint(canvas,visual,interactive);
        float pulse = 0;
        if (interactive && same(foldOwner(runDestination), visual.node.id)) {
            float entry = runLength > 0 ? clamp((runFraction - 0.66f) / 0.34f, 0, 1) : runFraction;
            pulse = (float) Math.sin(entry * Math.PI);
        }
        int nodeSave = -1;
        if (pulse > 0) {
            nodeSave = canvas.save(); canvas.scale(1 + pulse * 0.025f, 1 + pulse * 0.025f, b.centerX(), b.centerY());
            fillPaint.setColor((runColor & 0x00FFFFFF) | ((int) (48 * pulse) << 24));
            tempRect.set(b); tempRect.inset(-9 - 5 * pulse, -9 - 5 * pulse);
            makeNodeShape(scratchPath, visual.kind, tempRect); canvas.drawPath(scratchPath, fillPaint);
        }
        if (isSelected || isCurrent || isSource || isDropHover) {
            fillPaint.setColor(isDropTarget ? 0x38278C66 : isInvalidTarget ? 0x38D84E68 : isCurrent ? 0x405B8DEF : 0x245B8DEF);
            canvas.drawPath(visual.halo, fillPaint);
        }
        if (interactive) {
            int save = canvas.save(); canvas.translate(0, 2.2f);
            fillPaint.setColor(useDark ? 0x40000000 : 0x090F2038);
            canvas.drawPath(visual.shape, fillPaint); canvas.restoreToCount(save);
        }
        fillPaint.setColor(appearance.neutral(useDark ? DARK_SURFACE : LIGHT_SURFACE));
        canvas.drawPath(visual.shape, fillPaint);
        paintHoldFill(canvas,visual,interactive);
        strokePaint.setColor(isDropTarget ? DROP_COLOR : isInvalidTarget ? INVALID_COLOR :
                isSelected || isCurrent || isSource ? ACCENT : appearance.border(useDark,useDark ? DARK_LINE : LIGHT_LINE));
        strokePaint.setStrokeWidth(interactive ? (isDropHover ? 3.1f : isSelected || isCurrent || isSource ? 2.1f : appearance.width(1.1f)) / Math.max(0.65f, zoom) : appearance.width(1.15f));
        if(visual.kind==FlowModel.NOTE)strokePaint.setPathEffect(NOTE_DASH);
        canvas.drawPath(visual.shape, strokePaint);strokePaint.setPathEffect(null);
        if(visual.kind==FlowModel.CALL){canvas.drawLine(b.left+10,b.top+3,b.left+10,b.bottom-3,strokePaint);canvas.drawLine(b.right-10,b.top+3,b.right-10,b.bottom-3,strokePaint);}
        if (interactive && zoom < 0.23f) { if (nodeSave >= 0) canvas.restoreToCount(nodeSave);if(stickerSave>=0)canvas.restoreToCount(stickerSave); return; }

        int textClip=canvas.save();canvas.clipPath(visual.shape);
        int kindColor = appearance.kind(FlowModel.visualKind(visual.node),kindColor(FlowModel.visualKind(visual.node), useDark),useDark);
        if (visual.kind == FlowModel.START || visual.kind == FlowModel.END) {
            float iconScale=appearance.design().scale();float cx = b.left + 30*iconScale, cy = b.centerY();
            fillPaint.setColor((kindColor & 0x00FFFFFF) | 0x22000000);
            canvas.drawCircle(cx, cy, 11*iconScale, fillPaint);
            fillPaint.setColor(kindColor);
            if (visual.kind == FlowModel.START) {
                scratchPath.reset(); scratchPath.moveTo(cx - 3*iconScale, cy - 5*iconScale); scratchPath.lineTo(cx + 5*iconScale, cy); scratchPath.lineTo(cx - 3*iconScale, cy + 5*iconScale); scratchPath.close();
                canvas.drawPath(scratchPath, fillPaint);
            } else canvas.drawRoundRect(cx - 4*iconScale, cy - 4*iconScale, cx + 4*iconScale, cy + 4*iconScale, 1.5f*iconScale, 1.5f*iconScale, fillPaint);
        } else {
            labelPaint.setTypeface(font(appearance.design(),true));labelPaint.setTextSize(9.3f*appearance.design().scale()); labelPaint.setColor(kindColor);
            canvas.drawText(visual.typeLabel, b.centerX() - visual.labelWidth / 2, b.top + visual.labelY, labelPaint);labelPaint.setTypeface(MEDIUM_TYPEFACE);
        }
        textPaint.setTypeface(font(appearance.design(),visual.kind == FlowModel.START || visual.kind == FlowModel.END));
        textPaint.setTextSize(visual.bodySize); textPaint.setColor(appearance.neutral(useDark ? DARK_INK : LIGHT_INK));
        for (int i = 0; i < visual.lines.length; i++) {
            canvas.drawText(visual.lines[i], b.left + visual.bodyCenterX - visual.lineWidths[i] / 2,
                    b.top + visual.bodyY + i * visual.bodyLineHeight, textPaint);
        }
        canvas.restoreToCount(textClip);
        if(interactive&&visual.node.breakpoint){fillPaint.setColor(Ui.danger());canvas.drawCircle(b.left+12,b.top+12,9,fillPaint);strokePaint.setColor(Color.WHITE);strokePaint.setStrokeWidth(2);canvas.drawLine(b.left+9,b.top+8,b.left+9,b.top+16,strokePaint);canvas.drawLine(b.left+14,b.top+8,b.left+14,b.top+16,strokePaint);}
        if (isCurrent) {
            fillPaint.setColor(ACCENT); canvas.drawCircle(b.centerX(), b.top - 11, 4, fillPaint);
        }
        if (nodeSave >= 0) canvas.restoreToCount(nodeSave);
        if(stickerSave>=0)canvas.restoreToCount(stickerSave);
    }

    private void drawRunTrace(Canvas canvas) {
        if (runDestination == null || runLength <= 0) return;
        float distance = runLength * clamp(runFraction / 0.82f, 0, 1);
        runProgressPath.reset();
        runMeasure.getSegment(0, distance, runProgressPath, true);
        float width = 3.4f / Math.max(0.8f, zoom);
        strokePaint.setColor((runColor & 0x00FFFFFF) | 0x30000000);
        strokePaint.setStrokeWidth(width * 3); canvas.drawPath(runProgressPath, strokePaint);
        strokePaint.setColor(runColor); strokePaint.setStrokeWidth(width); canvas.drawPath(runProgressPath, strokePaint);
        if (runMeasure.getPosTan(distance, runPoint, null)) {
            fillPaint.setColor((runColor & 0x00FFFFFF) | 0x38000000);
            canvas.drawCircle(runPoint[0], runPoint[1], 10 / Math.max(0.8f, zoom), fillPaint);
            fillPaint.setColor(runColor); canvas.drawCircle(runPoint[0], runPoint[1], 5 / Math.max(0.8f, zoom), fillPaint);
            fillPaint.setColor(Color.WHITE); canvas.drawCircle(runPoint[0], runPoint[1], 2.3f / Math.max(0.8f, zoom), fillPaint);
        }
    }

    private void drawConnectionDrag(Canvas canvas) {
        NodeVisual source = nodeIndex.get(dragConnectionSource);
        if (source == null) return;
        float sx = source.bounds.centerX(), sy = source.bounds.bottom;
        boolean sideDrag=gesture==CONNECT_DRAG&&source.kind==FlowModel.DECISION&&dragConnectionBranch!=null;
        if(sideDrag){sx="false".equals(dragConnectionBranch)?source.bounds.right:source.bounds.left;sy=source.bounds.centerY();}
        EdgeVisual moved = edgeIndex.get(dragEdgeId);
        if (gesture == EDGE_DRAG && moved != null && moved.points.length >= 4) {
            sx = moved.points[0]; sy = moved.points[1];
        }
        float ex = dragConnectionX, ey = dragConnectionY;
        NodeVisual target = nodeIndex.get(dragConnectionTarget);
        boolean validTarget = target != null && target.kind != FlowModel.START && target.kind != FlowModel.NOTE;
        int color = validTarget ? DROP_COLOR : target != null ? INVALID_COLOR : ACCENT;
        if (validTarget) { ex = target.bounds.centerX(); ey = target.bounds.top; }
        if(validTarget && target==source) {
            // Preview the same rounded return route that will remain after the drop.
            String branch=dragConnectionBranch!=null?dragConnectionBranch:moved!=null?moved.edge.branch:"next";
            connectionLoopPreview=routeEdge(source,target,branch,connectionLoopPreview);
            buildRoundedRoute(scratchPath,connectionLoopPreview);
            ex=connectionLoopPreview[connectionLoopPreview.length-2];ey=connectionLoopPreview[connectionLoopPreview.length-1];
        } else {
            scratchPath.reset(); scratchPath.moveTo(sx, sy);
            float curve = Math.max(24 / zoom, Math.abs(ey - sy) * 0.45f);
            if(sideDrag)scratchPath.cubicTo(sx+("false".equals(dragConnectionBranch)?curve:-curve),sy,ex,ey-curve,ex,ey);
            else scratchPath.cubicTo(sx, sy + curve, ex, ey - curve, ex, ey);
        }
        strokePaint.setColor((color & 0x00FFFFFF) | 0x38000000); strokePaint.setStrokeWidth(6 / zoom); canvas.drawPath(scratchPath, strokePaint);
        strokePaint.setColor(color); strokePaint.setStrokeWidth(2.1f / zoom); canvas.drawPath(scratchPath, strokePaint);
        if (validTarget) {
            fillPaint.setColor(color); canvas.drawCircle(ex, ey, 5 / zoom, fillPaint);
            fillPaint.setColor(Color.WHITE); canvas.drawCircle(ex, ey, 2 / zoom, fillPaint);
        } else if (target == null && gesture != EDGE_DRAG) {
            float halfWidth = 44 / zoom, halfHeight = 23 / zoom;
            tempRect.set(ex - halfWidth, ey - halfHeight, ex + halfWidth, ey + halfHeight);
            fillPaint.setColor(dark ? 0xF0222222 : 0xF0FFFFFF); canvas.drawRoundRect(tempRect, 12 / zoom, 12 / zoom, fillPaint);
            strokePaint.setColor(color);
            strokePaint.setStrokeWidth(1.5f / zoom); canvas.drawRoundRect(tempRect, 12 / zoom, 12 / zoom, strokePaint);
            canvas.drawLine(ex - 5 / zoom, ey, ex + 5 / zoom, ey, strokePaint);
            canvas.drawLine(ex, ey - 5 / zoom, ex, ey + 5 / zoom, strokePaint);
        }
    }

    /** Screen-space feedback stays above the finger instead of under the drop. */
    private void drawConnectionDropHint(Canvas canvas) {
        NodeVisual target = nodeIndex.get(dragConnectionTarget);
        boolean self = target != null && same(dragConnectionSource, target.node.id);
        boolean valid = target != null && target.kind != FlowModel.START && target.kind != FlowModel.NOTE;
        int color = valid ? DROP_COLOR : target != null ? INVALID_COLOR : ACCENT;
        String title = target == null ? (gesture == EDGE_DRAG ? "Rilascia per altre opzioni" : "Rilascia per aggiungere") : valid ? (self ? "Rilascia per tornare qui" : gesture == EDGE_DRAG ? "Rilascia per cambiare arrivo" : "Rilascia per collegare") : "Inizio non riceve frecce";
        String detail = valid ? FlowModel.kindName(target.kind) + " · " + nonEmpty(target.node.text, placeholder(target.kind)) : null;
        float width = Math.min(dp(252), getWidth() - dp(24)), height = dp(detail == null ? 36 : 54);
        if (width <= 0) return;
        float left = clamp(lastX - width / 2, dp(12), getWidth() - dp(12) - width);
        float top = clamp(lastY - height - dp(50), dp(mobileViewport ? 72 : 8),
                Math.max(dp(mobileViewport ? 72 : 8), getHeight() - dp(mobileViewport ? 96 : 12) - height));
        tempRect.set(left, top, left + width, top + height);
        fillPaint.setColor(dark ? 0xF5222222 : 0xF5FFFFFF); canvas.drawRoundRect(tempRect, dp(12), dp(12), fillPaint);
        strokePaint.setColor((color & 0x00FFFFFF) | 0x90000000); strokePaint.setStrokeWidth(dp(1.2f));
        canvas.drawRoundRect(tempRect, dp(12), dp(12), strokePaint);
        labelPaint.setTextSize(dp(12.8f)); labelPaint.setColor(color);
        String displayedTitle = ellipsize(title, width - dp(20), labelPaint);
        canvas.drawText(displayedTitle, left + (width - labelPaint.measureText(displayedTitle)) / 2,
                top + dp(detail == null ? 23 : 21), labelPaint);
        if (detail != null) {
            labelPaint.setTextSize(dp(11.2f)); labelPaint.setColor(dark ? DARK_MUTED : LIGHT_MUTED);
            detail = ellipsize(detail.replace('\n', ' '), width - dp(20), labelPaint);
            canvas.drawText(detail, left + (width - labelPaint.measureText(detail)) / 2, top + dp(40), labelPaint);
        }
    }

    private void drawEdge(Canvas canvas, EdgeVisual edge, boolean useDark, boolean interactive) {
        if (interactive && gesture == EDGE_DRAG && same(dragEdgeId, edge.edge.id)) return;
        if(interactive&&gesture==CONNECT_DRAG&&dragConnectionBranch!=null&&same(dragConnectionSource,edge.edge.from)&&same(dragConnectionBranch,edge.edge.branch))return;
        int color = appearance.ink(edge.label == null ? (useDark ? 0xFF9099AA : 0xFF8891A2) : edge.color,useDark);
        boolean pressed = interactive && gesture == PENDING && same(downEdgeId, edge.edge.id);
        if (pressed) {
            strokePaint.setColor((color & 0x00FFFFFF) | 0x38000000);
            strokePaint.setStrokeWidth(9 / Math.max(0.6f, zoom)); canvas.drawPath(edge.path, strokePaint);
        }
        float arrival=interactive&&edgeMotionAnimator!=null&&enteringMotion.contains(edge.edge.id)?clamp(edgeMotionFraction,0,1):1;
        int rendered=(color&0x00FFFFFF)|((int)(255*arrival)<<24);
        strokePaint.setColor(rendered);
        strokePaint.setStrokeWidth(interactive ? (pressed ? 2.7f : appearance.width(1.7f)) / Math.max(0.6f, zoom) : appearance.width(1.7f));
        if(arrival<1){edge.motionMeasure.setPath(edge.path,false);edge.motionPath.reset();edge.motionMeasure.getSegment(0,edge.motionMeasure.getLength()*arrival,edge.motionPath,true);canvas.drawPath(edge.motionPath,strokePaint);}else canvas.drawPath(edge.path, strokePaint);
        fillPaint.setColor(rendered); canvas.drawPath(edge.arrow, fillPaint);
        if (edge.label != null && (!interactive || zoom >= 0.3f)) {
            fillPaint.setColor(((useDark ? DARK_PAPER : Color.WHITE)&0x00FFFFFF)|((int)(255*arrival)<<24));
            canvas.drawRoundRect(edge.labelBounds, 6, 6, fillPaint);
            labelPaint.setTextSize(10.5f); labelPaint.setColor(rendered);
            canvas.drawText(edge.label, edge.labelBounds.centerX() - edge.labelWidth / 2, edge.labelBounds.centerY() + 3.6f, labelPaint);
        }
    }

    /** A stable 48dp touch target on the incoming segment, outside the block. */
    private boolean editableDecisionEdge(EdgeVisual edge) {
        return !readOnly && !selectionMode && !isConnecting() && edge != null &&
                edge.from.kind == FlowModel.DECISION && ("true".equals(edge.edge.branch) || "false".equals(edge.edge.branch));
    }

    private void edgeHandle(EdgeVisual edge, float[] point) {
        float[] p=edge.points; int end=p.length-2;
        float ex=p[end],ey=p[end+1],sx=p[end-2],sy=p[end-1];
        float length=(float)Math.hypot(ex-sx,ey-sy);
        float offset=Math.min(dp(30)/scale(),length*.5f);
        point[0]=screenX(ex+(sx-ex)*offset/Math.max(1,length));
        point[1]=screenY(ey+(sy-ey)*offset/Math.max(1,length));
        if(convergingDecisionEdges.contains(edge.edge.id)){
            point[0]=screenX(edge.to.bounds.centerX())+("true".equals(edge.edge.branch)?-dp(28):dp(28));
            point[1]=screenY(edge.to.bounds.top)-dp(22);
            // Keep the incoming grips outside both lateral 48dp touch targets.
            // Their positions must stay stable when the decision is selected.
            float portY=screenY(edge.from.bounds.centerY()),clearance=dp(38);
            if(Math.abs(point[1]-portY)<clearance){
                boolean yes="true".equals(edge.edge.branch);
                float left=screenX(edge.from.bounds.left)-dp(26),right=screenX(edge.from.bounds.right)+dp(26);
                for(int pass=0;pass<2;pass++){
                    if(Math.abs(point[0]-left)<clearance)point[0]=left+(yes?-clearance:clearance);
                    if(Math.abs(point[0]-right)<clearance)point[0]=right+(yes?-clearance:clearance);
                }
            }
        }
    }

    private final float[] edgeHandlePoint = new float[2];
    private void drawEdgeHandles(Canvas canvas) {
        if (pendingPlacement!=null||gesture == MULTI || gesture == MARQUEE || gesture == CONNECT_DRAG || gesture == DRAG) return;
        for (EdgeVisual edge:edges) {
            if (!editableDecisionEdge(edge) || edge.points.length<4 || (gesture==EDGE_DRAG&&same(dragEdgeId,edge.edge.id))) continue;
            edgeHandle(edge,edgeHandlePoint); float x=edgeHandlePoint[0],y=edgeHandlePoint[1];
            if(x < -dp(24)||y < -dp(24)||x>getWidth()+dp(24)||y>getHeight()+dp(24)) continue;
            fillPaint.setColor(dark?DARK_SURFACE:Color.WHITE);canvas.drawCircle(x,y,dp(10),fillPaint);
            strokePaint.setColor(appearance.ink(edge.color,dark));strokePaint.setStrokeWidth(dp(1.7f));canvas.drawCircle(x,y,dp(10),strokePaint);
            // Both handles share the same geometry for painting and hit testing.
            strokePaint.setColor(appearance.ink(edge.color,dark));strokePaint.setStrokeWidth(dp(1.8f));
            canvas.drawLine(x-dp(4),y,x+dp(4),y,strokePaint);canvas.drawLine(x,y-dp(4),x,y+dp(4),strokePaint);
        }
    }

    private EdgeVisual hitEdgeHandle(float x,float y) {
        EdgeVisual found=null;float best=dp(24)*dp(24);
        for(int i=edges.size()-1;i>=0;i--){EdgeVisual edge=edges.get(i);
            if(!editableDecisionEdge(edge)||edge.points.length<4)continue;
            edgeHandle(edge,edgeHandlePoint);float dx=x-edgeHandlePoint[0],dy=y-edgeHandlePoint[1],distance=dx*dx+dy*dy;
            if(distance<best){best=distance;found=edge;}
        }return found;
    }

    private void drawConnectionButton(Canvas canvas) {
        updateConnectionButtonGeometry();
        if (plusNodeId == null){stopConnectorPulse();return;}
        NodeVisual visual = nodeIndex.get(plusNodeId);
        if(visual.kind==FlowModel.DECISION){
            stopConnectorPulse();
            drawBranchPlus(canvas,visual,truePlusHit,true);drawBranchPlus(canvas,visual,falsePlusHit,false);return;
        }
        syncConnectorPulse();
        float x = plusHit.centerX(), y = plusHit.centerY();
        if (connectorAnimator != null) {
            float wave = (float) Math.sin(connectorPhase * Math.PI);
            fillPaint.setColor((ACCENT & 0x00FFFFFF) | ((int) (46 * (1 - connectorPhase)) << 24));
            canvas.drawCircle(x, y, dp(20 + 6 * connectorPhase), fillPaint);
            fillPaint.setColor((ACCENT & 0x00FFFFFF) | ((int) (30 * wave) << 24));
            canvas.drawCircle(x, y, dp(20 + 2 * wave), fillPaint);
        }
        strokePaint.setColor(ACCENT); strokePaint.setStrokeWidth(dp(1.5f));
        canvas.drawLine(x, screenY(visual.bounds.bottom) + dp(2), x, y - dp(18), strokePaint);
        fillPaint.setColor(dark ? DARK_SURFACE : Color.WHITE); canvas.drawCircle(x, y, dp(20), fillPaint);
        fillPaint.setColor(ACCENT); canvas.drawCircle(x, y, dp(17), fillPaint);
        strokePaint.setColor(Color.WHITE); strokePaint.setStrokeWidth(dp(2));
        canvas.drawLine(x - dp(5.5f), y, x + dp(5.5f), y, strokePaint);
        canvas.drawLine(x, y - dp(5.5f), x, y + dp(5.5f), strokePaint);
    }

    private void drawBranchPlus(Canvas canvas,NodeVisual node,RectF hit,boolean yes){
        float x=hit.centerX(),y=hit.centerY();int color=appearance.ink(safeColor(yes?node.node.trueColor:node.node.falseColor,yes?TRUE_COLOR:FALSE_COLOR),dark);
        strokePaint.setColor(color);strokePaint.setStrokeWidth(dp(1.6f));
        canvas.drawLine(screenX(yes?node.bounds.left:node.bounds.right),screenY(node.bounds.centerY()),x,y,strokePaint);
        fillPaint.setColor(dark?DARK_SURFACE:Color.WHITE);canvas.drawCircle(x,y,dp(21),fillPaint);
        strokePaint.setColor(color);strokePaint.setStrokeWidth(dp(1.8f));canvas.drawCircle(x,y,dp(18),strokePaint);
        canvas.drawLine(x-dp(6),y,x+dp(6),y,strokePaint);canvas.drawLine(x,y-dp(6),x,y+dp(6),strokePaint);
        String label=yes?nonEmpty(node.node.trueLabel,"Sì"):nonEmpty(node.node.falseLabel,"No");
        labelPaint.setTextSize(dp(11));labelPaint.setColor(color);label=ellipsize(label,dp(72),labelPaint);
        float textWidth=labelPaint.measureText(label);tempRect.set(x-textWidth/2-dp(4),y-dp(38),x+textWidth/2+dp(4),y-dp(23));
        fillPaint.setColor(dark?DARK_PAPER:LIGHT_PAPER);canvas.drawRoundRect(tempRect,dp(4),dp(4),fillPaint);
        canvas.drawText(label,x-textWidth/2,y-dp(27),labelPaint);
    }

    private void updateConnectionButtonGeometry() {
        plusHit.setEmpty();truePlusHit.setEmpty();falsePlusHit.setEmpty(); plusNodeId = null;
        if (pendingPlacement!=null||readOnly || selected.size() != 1 || isConnecting() || gesture == MULTI || gesture == MARQUEE || gesture == CONNECT_DRAG || gesture == EDGE_DRAG || gesture == EDGE_PENDING || gesture == DRAG) return;
        NodeVisual visual = nodeIndex.get(selected.iterator().next());
        if (visual == null || (visual.kind == FlowModel.END||visual.kind==FlowModel.NOTE)) return;
        if(visual.kind==FlowModel.DECISION){
            float y=screenY(visual.bounds.centerY()),left=screenX(visual.bounds.left)-dp(26),right=screenX(visual.bounds.right)+dp(26);
            truePlusHit.set(left-dp(24),y-dp(24),left+dp(24),y+dp(24));falsePlusHit.set(right-dp(24),y-dp(24),right+dp(24),y+dp(24));
            plusNodeId=visual.node.id;return;
        }
        float x = screenX(visual.bounds.centerX()), y = screenY(visual.bounds.bottom) + dp(23);
        if (x < -dp(24) || x > getWidth() + dp(24) || y < -dp(24) || y > getHeight() + dp(24)) return;
        plusHit.set(x - dp(24), y - dp(24), x + dp(24), y + dp(24)); plusNodeId = visual.node.id;
    }

    // Geometries and text layouts are rebuilt only after a model change. Panning,
    // zooming, highlighting and ordinary taps reuse every retained path/layout.
    private void ensureCache() {
        if (!dirty) return;
        FlowModel.Document scene=canvasScene();
        dirty = false;
        boolean rebuildIndexes = structureDirty || nodes.size()!=scene.nodes.size() || edges.size()!=scene.edges.size();
        structureDirty = false;
        HashMap<String, NodeVisual> previousNodes = rebuildIndexes ? new HashMap<>(nodeIndex) : null;
        boolean geometryChanged = nodes.size() != scene.nodes.size(),routingKindsChanged=rebuildIndexes,semanticChanged=rebuildIndexes;
        float changedTop=Float.POSITIVE_INFINITY,changedBottom=Float.NEGATIVE_INFINITY;
        if (rebuildIndexes) { nodes.clear(); nodeIndex.clear(); }
        for (FlowModel.Node model : scene.nodes) {
            if (model == null || model.id == null) continue;
            NodeVisual visual = rebuildIndexes ? previousNodes.remove(model.id) : nodeIndex.get(model.id);
            if (visual == null) visual = new NodeVisual();
            LayoutPosition position = modelGeometry ? null : layoutPositions.get(model.id);
            float x = position == null ? model.x : position.fromX + (position.toX - position.fromX) * layoutFraction;
            float y = position == null ? model.y : position.fromY + (position.toY - position.fromY) * layoutFraction;
            if(!modelGeometry&&pendingPlacement!=null&&pendingPlacement.expected==document){float[] preview=pendingPlacement.positions.get(model.id);if(preview!=null){x=preview[0];y=preview[1];}}
            float width=width(model),height=height(model);float[] sizing=modelGeometry?null:sizingFrom.get(model.id);
            if(sizing!=null){width=sizing[0]+(width-sizing[0])*sizingFraction;height=sizing[1]+(height-sizing[1])*sizingFraction;}
            boolean changedShape = visual.kind != model.kind || visual.bounds.left != x || visual.bounds.top != y || visual.bounds.width()!=width || visual.bounds.height()!=height;
            boolean changedText = visual.kind != model.kind || !same(visual.text, model.displayText());
            if(visual.node!=model)semanticChanged=true;
            visual.node = model;
            if (changedShape) {
                if(visual.kind>=0){changedTop=Math.min(changedTop,visual.bounds.top);changedBottom=Math.max(changedBottom,visual.bounds.bottom);}
                changedTop=Math.min(changedTop,y);changedBottom=Math.max(changedBottom,y+height);
                if(visual.kind!=model.kind)routingKindsChanged=true;
                visual.kind = model.kind;
                visual.bounds.set(x, y, x + width, y + height);
                makeNodeShape(visual.shape, visual.kind, visual.bounds);
                tempRect.set(visual.bounds); tempRect.inset(-5, -5);
                makeNodeShape(visual.halo, visual.kind, tempRect);
                visual.generation++; geometryChanged = true;
            }
            if (changedText || changedShape) updateNodeText(visual);
            if (rebuildIndexes) { nodes.add(visual); nodeIndex.put(model.id, visual); }
        }
        if (rebuildIndexes && !previousNodes.isEmpty()) {
            geometryChanged = true;
            for(NodeVisual removed:previousNodes.values()){changedTop=Math.min(changedTop,removed.bounds.top);changedBottom=Math.max(changedBottom,removed.bounds.bottom);}
        }
        if (geometryChanged) {
            geometryGeneration++;
            nodesByTop.clear(); nodesByTop.addAll(nodes);
            Collections.sort(nodesByTop, new Comparator<NodeVisual>() {
                @Override public int compare(NodeVisual a, NodeVisual b) { return Float.compare(a.bounds.top, b.bounds.top); }
            });
        }
        boolean routingChanged=rebuildIndexes||routingKindsChanged;
        if(!routingChanged)for(FlowModel.Edge edge:scene.edges){EdgeVisual old=edgeIndex.get(edge.id);if(old==null||!same(old.routeFrom,edge.from)||!same(old.routeTo,edge.to)||!same(old.branch,edge.branch)){routingChanged=true;break;}}
        if(routingChanged){branchRoutes=buildBranchRoutes(scene);branchTopologyGeneration++;}
        if(routingChanged){
            convergingDecisionEdges.clear();HashMap<String,FlowModel.Edge[]> pairs=new HashMap<>();
            for(FlowModel.Edge e:scene.edges)if("true".equals(e.branch)||"false".equals(e.branch)){
                FlowModel.Edge[] pair=pairs.get(e.from);if(pair==null){pair=new FlowModel.Edge[2];pairs.put(e.from,pair);}pair["true".equals(e.branch)?0:1]=e;
            }
            for(FlowModel.Edge[] pair:pairs.values())if(pair[0]!=null&&pair[1]!=null&&same(pair[0].to,pair[1].to)){convergingDecisionEdges.add(pair[0].id);convergingDecisionEdges.add(pair[1].id);}
        }
        HashMap<String, EdgeVisual> previousEdges = rebuildIndexes ? new HashMap<>(edgeIndex) : null;
        if (rebuildIndexes) { edges.clear(); edgeIndex.clear(); }
        for (FlowModel.Edge model : scene.edges) {
            if (model == null || model.id == null) continue;
            NodeVisual from = nodeIndex.get(model.from), to = nodeIndex.get(model.to);
            if (from == null || to == null) continue;
            EdgeVisual visual = rebuildIndexes ? previousEdges.remove(model.id) : edgeIndex.get(model.id);
            if (visual == null) visual = new EdgeVisual();
            DecisionRoutes pair=branchRoutes.pairs.get(model.from);
            NodeVisual peer=pair==null?null:nodeIndex.get("true".equals(model.branch)?pair.no:"false".equals(model.branch)?pair.yes:null);
            boolean peerChanged=visual.peer!=peer||(peer!=null&&visual.peerGeneration!=peer.generation)||(from.kind==FlowModel.DECISION&&visual.routingGeneration!=branchTopologyGeneration);
            boolean rebuild = peerChanged || visual.from != from || visual.to != to || !same(visual.branch, model.branch) ||
                    visual.sourceGeneration != from.generation || visual.targetGeneration != to.generation ||
                    (visual.corridor && visual.corridorGeneration != geometryGeneration && changedBottom>=visual.corridorTop && changedTop<=visual.corridorBottom);
            if(visual.edge!=model)semanticChanged=true;
            visual.edge = model; visual.from = from; visual.to = to;visual.peer=peer;visual.peerGeneration=peer==null?-1:peer.generation;visual.routingGeneration=branchTopologyGeneration;visual.routeFrom=model.from;visual.routeTo=model.to;
            visual.branch = model.branch;
            if (rebuild) {
                visual.points = routeEdge(from, to, model.branch, visual.points);
                buildRoundedRoute(visual.path, visual.points);
                buildArrow(visual.arrow, visual.points);
                visual.sourceGeneration = from.generation; visual.targetGeneration = to.generation;
                visual.corridorGeneration = geometryGeneration;
                visual.corridor = peer!=null || (from.kind == FlowModel.DECISION ? to.bounds.top < from.bounds.bottom + 24 : to.bounds.top < from.bounds.bottom + 34);
                visual.corridorTop=Math.min(from.bounds.top,to.bounds.top)-32;visual.corridorBottom=Math.max(from.bounds.bottom,to.bounds.bottom)+32;
                if(peer!=null){visual.corridorTop=Math.min(visual.corridorTop,peer.bounds.top-32);visual.corridorBottom=Math.max(visual.corridorBottom,peer.bounds.bottom+32);}
            }
            String label = null, colorString = null;
            if ("true".equals(model.branch)) { label = nonEmpty(from.node.trueLabel, "Sì"); colorString = from.node.trueColor; }
            else if ("false".equals(model.branch)) { label = nonEmpty(from.node.falseLabel, "No"); colorString = from.node.falseColor; }
            boolean colorChanged = !same(visual.colorString, colorString) || visual.color == 0;
            if (colorChanged) {
                visual.colorString = colorString;
                visual.color = safeColor(colorString, "false".equals(model.branch) ? FALSE_COLOR : TRUE_COLOR);
            }
            boolean labelChanged = !same(visual.rawLabel, label);
            if (labelChanged) {
                visual.rawLabel = label;
                labelPaint.setTextSize(10.5f);
                visual.label = label == null ? null : ellipsize(label, 146, labelPaint);
                visual.labelWidth = label == null ? 0 : labelPaint.measureText(visual.label);
            }
            if (rebuild || labelChanged || colorChanged) {
                updateEdgeBounds(visual);
            }
            if (rebuildIndexes) { edges.add(visual); edgeIndex.put(model.id, visual); }
        }
        boolean first = true;
        for (NodeVisual node : nodes) {
            if (first) { diagramBounds.set(node.bounds); first = false; }
            else diagramBounds.union(node.bounds);
        }
        for (EdgeVisual edge : edges) {
            if (first) { diagramBounds.set(edge.bounds); first = false; }
            else diagramBounds.union(edge.bounds);
        }
        if (first) diagramBounds.set(-140, -90, 140, 90);
        else diagramBounds.inset(-4, -4);
        if(semanticChanged||routingChanged)semanticGeneration++;
    }

    private void updateNodeText(NodeVisual visual) {
        FlowModel.Node model = visual.node;
        visual.text = model.displayText();
        visual.typeLabel = isFoldCard(model.id)?FlowCycleView.caption(foldProjection.cards.get(model.id)):navigationCaptions.containsKey(model.id)?navigationCaptions.get(model.id):typeLabel(model.kind);
        boolean terminal = model.kind == FlowModel.START || model.kind == FlowModel.END;
        float textScale=appearance.design().scale();textPaint.setTypeface(font(appearance.design(),terminal));
        visual.bodySize = (model.kind == FlowModel.DECISION ? 13.3f : terminal ? 14f : 13.7f)*textScale;
        visual.bodyLineHeight = 17.5f*textScale;
        textPaint.setTextSize(visual.bodySize);
        float available = terminal ? visual.bounds.width()-64*textScale : model.kind==FlowModel.DECISION?visual.bounds.width()*.56f:visual.bounds.width()-(model.kind==FlowModel.INPUT||model.kind==FlowModel.OUTPUT?62:36)*textScale;
        visual.lines = size(model).lines;
        visual.lineWidths = new float[visual.lines.length];
        for (int i = 0; i < visual.lines.length; i++) visual.lineWidths[i] = textPaint.measureText(visual.lines[i]);
        textPaint.getFontMetrics(fontMetrics);
        float height = visual.bounds.height();
        float bodyTop = terminal ? 8*textScale : model.kind == FlowModel.DECISION ? height*.34f : 32*textScale;
        float bodyBottom = terminal ? height - 8*textScale : model.kind == FlowModel.DECISION ? height*.68f : height - 12*textScale;
        float blockHeight = (visual.lines.length - 1) * visual.bodyLineHeight + fontMetrics.descent - fontMetrics.ascent;
        visual.bodyY = bodyTop + Math.max(0, (bodyBottom - bodyTop - blockHeight) / 2) - fontMetrics.ascent;
        visual.bodyCenterX = terminal ? 49*textScale + available / 2 : visual.bounds.width() / 2;
        visual.labelY = model.kind == FlowModel.DECISION ? height*.22f+8*textScale : 23*textScale;
        labelPaint.setTypeface(font(appearance.design(),true));labelPaint.setTextSize(9.3f*textScale); visual.labelWidth = labelPaint.measureText(visual.typeLabel);labelPaint.setTypeface(MEDIUM_TYPEFACE);
    }

    private void updateEdgeBounds(EdgeVisual edge) {
        edge.path.computeBounds(edge.bounds, true);
        edge.bounds.inset(-10, -10);
        edge.labelBounds.setEmpty();
        if (edge.label == null || edge.points.length < 4) return;
        float x1 = edge.points[0], y1 = edge.points[1], x2 = edge.points[2], y2 = edge.points[3];
        float dx = x2 - x1, dy = y2 - y1, distance = (float) Math.hypot(dx, dy);
        float half = edge.labelWidth / 2 + 7;
        float position = Math.min(Math.max(half + 9, 29), Math.max(12, distance / 2));
        float x = x1 + (distance > 0 ? dx / distance * position : 0);
        float y = y1 + (distance > 0 ? dy / distance * position : 0);
        if (Math.abs(dx) > Math.abs(dy)) y -= 11;
        else x += half + 3;
        edge.labelBounds.set(x - half, y - 10, x + half, y + 10);
        edge.bounds.union(edge.labelBounds);
    }

    private static void makeNodeShape(Path path, int kind, RectF b) {
        path.reset();
        if (kind == FlowModel.DECISION) {
            path.moveTo(b.centerX(), b.top); path.lineTo(b.right, b.centerY());
            path.lineTo(b.centerX(), b.bottom); path.lineTo(b.left, b.centerY()); path.close();
        } else if (kind == FlowModel.INPUT || kind == FlowModel.OUTPUT) {
            path.moveTo(b.left + 19, b.top); path.lineTo(b.right, b.top);
            path.lineTo(b.right - 19, b.bottom); path.lineTo(b.left, b.bottom); path.close();
        } else {
            float radius = kind == FlowModel.START || kind == FlowModel.END ? Math.min(29,b.height() / 2) : 11;
            path.addRoundRect(b, radius, radius, Path.Direction.CW);
        }
    }

    private static final class DecisionRoutes {
        String yes,no;long generation=-1;float[] yesPoints,noPoints;
    }
    private static final class BranchRoutingContext {
        final HashMap<String,DecisionRoutes> pairs=new HashMap<>();
    }
    private BranchRoutingContext branchRoutes=new BranchRoutingContext();
    private long branchTopologyGeneration;
    private BranchRoutingContext buildBranchRoutes(FlowModel.Document scene) {
        BranchRoutingContext context=new BranchRoutingContext();
        for(FlowModel.Edge edge:scene.edges){
            NodeVisual source=nodeIndex.get(edge.from);
            if(source==null||source.kind!=FlowModel.DECISION||(!"true".equals(edge.branch)&&!"false".equals(edge.branch)))continue;
            DecisionRoutes pair=context.pairs.get(edge.from);
            if(pair==null){pair=new DecisionRoutes();context.pairs.put(edge.from,pair);}
            if("true".equals(edge.branch))pair.yes=edge.to;else pair.no=edge.to;
        }
        return context;
    }
    private static float[] boxCoordinates(RectF b){return new float[]{b.left,b.top,b.right,b.bottom};}
    private float[][] branchObstacles(NodeVisual source,NodeVisual yes,NodeVisual no,float[] a,float[] b){
        RectF region=new RectF(source.bounds);region.union(yes.bounds);region.union(no.bounds);
        for(float[] route:new float[][]{a,b})for(int i=0;i<route.length;i+=2)region.union(route[i],route[i+1]);region.inset(-80,-80);
        ArrayList<float[]> result=new ArrayList<>();
        for(NodeVisual node:nodes){
            if(node==source||node==yes||node==no||node.kind==FlowModel.NOTE)continue;
            boolean held=false;if(gesture==DRAG)for(DragOrigin origin:dragOrigins)if(same(origin.node.id,node.node.id)){held=true;break;}
            if(!held&&RectF.intersects(region,node.bounds))result.add(boxCoordinates(node.bounds));
        }
        return result.toArray(new float[0][]);
    }
    private float[] routeEdge(NodeVisual from,NodeVisual to,String branch,float[] retained){return routeEdge(from,to,branch,retained,branchRoutes);}
    private float[] routeEdge(NodeVisual from,NodeVisual to,String branch,float[] retained,BranchRoutingContext context){
        DecisionRoutes pair=context==null?null:context.pairs.get(from.node.id);
        if(from.kind!=FlowModel.DECISION||pair==null||(!"true".equals(branch)&&!"false".equals(branch)))return baseRouteEdge(from,to,branch,retained);
        NodeVisual yes="true".equals(branch)?to:nodeIndex.get(pair.yes),no="false".equals(branch)?to:nodeIndex.get(pair.no);
        if(yes==null||no==null||yes==no)return baseRouteEdge(from,to,branch,retained);
        boolean stored=same(yes.node.id,pair.yes)&&same(no.node.id,pair.no);
        if(!stored||pair.generation!=geometryGeneration){
            float[] a=baseRouteEdge(from,yes,"true",new float[0]),b=baseRouteEdge(from,no,"false",new float[0]);
            float[][] obstacles=FlowBranchRouting.Conflicts(a,b)?branchObstacles(from,yes,no,a,b):new float[0][];
            float[][] paths=FlowBranchRouting.Separate(boxCoordinates(from.bounds),boxCoordinates(yes.bounds),boxCoordinates(no.bounds),a,b,obstacles);
            if(!stored)return ("true".equals(branch)?paths[0]:paths[1]).clone();
            pair.yesPoints=paths[0];pair.noPoints=paths[1];pair.generation=geometryGeneration;
        }
        // Preview buffers may subsequently be reused. Never lend the pair cache.
        return ("true".equals(branch)?pair.yesPoints:pair.noPoints).clone();
    }
    private float[] baseRouteEdge(NodeVisual from, NodeVisual to, String branch, float[] retained) {
        RectF a = from.bounds, b = to.bounds;
        boolean side = from.kind == FlowModel.DECISION;
        boolean falseBranch = "false".equals(branch);
        float sx = side ? (falseBranch ? a.right : a.left) : a.centerX();
        float sy = side ? a.centerY() : a.bottom;
        float ex = b.centerX(), ey = b.top - 2;
        float[] points = routeBuffer; int count = 0;
        points[count++] = sx; points[count++] = sy;
        if (from == to) {
            if (side && !falseBranch) {
                points[count++] = a.left - 58; points[count++] = sy;
                points[count++] = a.left - 58; points[count++] = a.top - 32;
            } else {
                if (!side) { points[count++] = sx; points[count++] = a.bottom + 32; }
                float departureY = points[count - 1];
                points[count++] = a.right + 58; points[count++] = departureY;
                points[count++] = a.right + 58; points[count++] = a.top - 32;
            }
            points[count++] = ex; points[count++] = a.top - 32;
        } else if (side) {
            int direction = falseBranch ? 1 : -1;
            boolean targetOnSide = direction < 0 ? ex < a.left - 26 : ex > a.right + 26;
            if (targetOnSide && b.top > sy + 28) {
                points[count++] = ex; points[count++] = sy;
            } else {
                float outer = direction < 0 ? Math.min(a.left, b.left) - 42 : Math.max(a.right, b.right) + 42;
                if (b.top < a.bottom + 24) outer = outsideCorridor(a, b, direction, outer);
                points[count++] = outer; points[count++] = sy;
                points[count++] = outer; points[count++] = b.top - 32;
                points[count++] = ex; points[count++] = b.top - 32;
            }
        } else if (b.top >= a.bottom + 34) {
            float middle = a.bottom + (b.top - a.bottom) / 2;
            points[count++] = sx; points[count++] = middle;
            points[count++] = ex; points[count++] = middle;
        } else {
            boolean upward = b.top < a.top;
            int direction = upward ? (sx <= ex ? -1 : 1) : (ex < sx - 20 ? -1 : 1);
            float outer = direction < 0 ? Math.min(a.left, b.left) - 52 : Math.max(a.right, b.right) + 52;
            outer = outsideCorridor(a, b, direction, outer);
            points[count++] = sx; points[count++] = a.bottom + 30;
            points[count++] = outer; points[count++] = a.bottom + 30;
            points[count++] = outer; points[count++] = b.top - 32;
            points[count++] = ex; points[count++] = b.top - 32;
        }
        points[count++] = ex; points[count++] = ey;
        return cleanPoints(points, count, retained);
    }

    private float outsideCorridor(RectF a, RectF b, int direction, float initial) {
        float top = Math.min(a.top, b.top) - 32, bottom = Math.max(a.bottom, b.bottom) + 32;
        int low = 0, high = nodesByTop.size();
        while (low < high) {
            int middle = (low + high) >>> 1;
            if (nodesByTop.get(middle).bounds.top < top - 136) low = middle + 1;
            else high = middle;
        }
        float outer = initial;
        for (int i = low; i < nodesByTop.size(); i++) {
            RectF bounds = nodesByTop.get(i).bounds;
            if (bounds.top > bottom) break;
            if (bounds.bottom < top) continue;
            outer = direction < 0 ? Math.min(outer, bounds.left - 42) : Math.max(outer, bounds.right + 42);
        }
        return outer;
    }

    private static float[] cleanPoints(float[] points, int count, float[] retained) {
        // Compact the scratch buffer in place; ordinary drags keep the same
        // number of bends and can reuse the edge's existing coordinate array.
        int length = 0;
        for (int i = 0; i < count; i += 2) {
            float x = points[i], y = points[i + 1];
            if (length >= 2 && Math.abs(points[length - 2] - x) < 0.01f && Math.abs(points[length - 1] - y) < 0.01f) continue;
            if (length >= 4) {
                float ax = points[length - 2] - points[length - 4], ay = points[length - 1] - points[length - 3];
                float bx = x - points[length - 2], by = y - points[length - 1];
                if (Math.abs(ax * by - ay * bx) < 0.01f && ax * bx + ay * by >= 0) length -= 2;
            }
            points[length++] = x; points[length++] = y;
        }
        if (retained.length != length) retained = new float[length];
        System.arraycopy(points, 0, retained, 0, length);
        return retained;
    }

    private static void buildRoundedRoute(Path path, float[] p) {
        path.reset(); if (p.length < 2) return;
        path.moveTo(p[0], p[1]);
        for (int i = 2; i + 3 < p.length; i += 2) {
            float px = p[i - 2], py = p[i - 1], x = p[i], y = p[i + 1], nx = p[i + 2], ny = p[i + 3];
            float before = (float) Math.hypot(x - px, y - py), after = (float) Math.hypot(nx - x, ny - y);
            float radius = Math.min(8, Math.min(before / 2, after / 2));
            if (before < 0.01f || after < 0.01f) { path.lineTo(x, y); continue; }
            path.lineTo(x - (x - px) / before * radius, y - (y - py) / before * radius);
            path.quadTo(x, y, x + (nx - x) / after * radius, y + (ny - y) / after * radius);
        }
        if (p.length >= 4) path.lineTo(p[p.length - 2], p[p.length - 1]);
    }

    private static void buildArrow(Path path, float[] p) {
        path.reset(); if (p.length < 4) return;
        int i = p.length - 2;
        float x = p[i], y = p[i + 1], dx = x - p[i - 2], dy = y - p[i - 1];
        float length = (float) Math.hypot(dx, dy); if (length < 0.01f) return;
        dx /= length; dy /= length;
        path.moveTo(x, y); path.lineTo(x - dx * 8 + dy * 4, y - dy * 8 - dx * 4);
        path.lineTo(x - dx * 8 - dy * 4, y - dy * 8 + dx * 4); path.close();
    }

    @Override public boolean onTouchEvent(MotionEvent event) {
        if (!isEnabled()) return false;
        if(pendingPlacement!=null)return true;
        final int action = event.getActionMasked();
        if (action == MotionEvent.ACTION_DOWN) {
            stopEdgeMotion();stopLanding();
            cancelCamera(false);
            finishLayoutAnimation();
            resetGesture(true);
            getParentDisallow(true);
            ensureCache();
            updateConnectionButtonGeometry();
            requestFocus();
            activePointer = event.getPointerId(0);
            downX = lastX = event.getX(); downY = lastY = event.getY();
            downRawX=lastRawX=event.getRawX();downRawY=lastRawY=event.getRawY();
            downWorldX = worldX(downX); downWorldY = worldY(downY);
            downBranch=truePlusHit.contains(downX,downY)?"true":falsePlusHit.contains(downX,downY)?"false":null;
            NodeVisual hit = hitNode(downWorldX, downWorldY);
            downNodeId = hit == null ? null : hit.node.id;
            EdgeVisual handle=hitEdgeHandle(downX,downY);
            EdgeVisual line=downNodeId==null?hitEdge(downWorldX,downWorldY):null;
            // The label also starts a direct drag; a simple tap keeps the accessible menu.
            if(handle==null && editableDecisionEdge(line) && line.labelBounds.contains(downWorldX,downWorldY))handle=line;
            if(downBranch!=null&&plusNodeId!=null){downNodeId=plusNodeId;gesture=PLUS_PENDING;}
            else if(handle!=null){
                downNodeId=null;downEdgeId=handle.edge.id;gesture=EDGE_PENDING;invalidate();
            } else if (!readOnly && !isConnecting() && plusNodeId != null && plusHit.contains(downX, downY)) {
                downNodeId = plusNodeId; gesture = PLUS_PENDING;
            } else {
                gesture = PENDING;
                if (!readOnly && !isConnecting() && !selectionMode && downNodeId == null) {
                    EdgeVisual edge = hitEdge(downWorldX, downWorldY);
                    downEdgeId = edge == null ? null : edge.edge.id;
                    if (downEdgeId != null) invalidate();
                }
                if (!readOnly && !isConnecting()){if(downNodeId!=null&&!isFoldCard(downNodeId)&&(!selectionMode||groupSelected()))armHoldFill();else postDelayed(longPress, ViewConfiguration.getLongPressTimeout());}
            }
            return true;
        }
        if (action == MotionEvent.ACTION_POINTER_DOWN) {
            finishSingleForMulti();
            setupMulti(event, -1);
            lastTapTime = 0;
            return true;
        }
        if (action == MotionEvent.ACTION_MOVE) {
            if (gesture == MULTI) { moveMulti(event); return true; }
            int index = event.findPointerIndex(activePointer);
            if (index < 0 || gesture == SUPPRESSED || gesture == NONE) return true;
            float x = event.getX(index), y = event.getY(index);
            float rawX=event.getRawX()+x-event.getX(0),rawY=event.getRawY()+y-event.getY(0);
            float dx = rawX-downRawX, dy = rawY-downRawY;
            if ((gesture == PENDING || gesture == PLUS_PENDING || gesture == EDGE_PENDING) && dx * dx + dy * dy > touchSlop * touchSlop) {
                removeCallbacks(longPress); if(!stickerDetached&&liftFraction==0)cancelHoldFill(); lastTapTime = 0;
                if (!readOnly && gesture == EDGE_PENDING) {
                    EdgeVisual edge=edgeIndex.get(downEdgeId);
                    if(editableDecisionEdge(edge)){dragEdgeId=edge.edge.id;dragConnectionSource=edge.edge.from;dragConnectionTarget=null;gesture=EDGE_DRAG;stopConnectorPulse();}
                    else gesture=SUPPRESSED;
                } else if (!readOnly && gesture == PLUS_PENDING && downNodeId != null) startConnectionDrag();
                else if(!readOnly&&groupSelected()&&downNodeId!=null&&!isConnecting())startDrag();
                else if (!readOnly && selectionMode && !isConnecting()) startMarquee();
                else if (!readOnly && downNodeId != null && gesture != PLUS_PENDING && !isConnecting()) startDrag();
                else {
                    gesture = PAN;
                    // Pending MOVE events have not moved the sheet yet.
                    lastX = downX; lastY = downY;
                    lastRawX=downRawX;lastRawY=downRawY;
                }
            }
            if (gesture == PAN) {
                panX += rawX-lastRawX; panY += rawY-lastRawY; constrainViewport(); pendingFit = false; invalidate();
            } else if (gesture == DRAG) {if(dragChanged||dx*dx+dy*dy>touchSlop*touchSlop)moveDrag(dx / scale(), dy / scale());updateInsertionTarget();}
            else if (gesture == MARQUEE) updateMarquee(worldX(x), worldY(y));
            else if (gesture == CONNECT_DRAG || gesture == EDGE_DRAG) updateConnectionDrag(worldX(x), worldY(y));
            lastX = x; lastY = y;
            lastRawX=rawX;lastRawY=rawY;
            return true;
        }
        if (action == MotionEvent.ACTION_POINTER_UP) {
            removeCallbacks(longPress);
            if (event.getPointerCount() - 1 >= 2) setupMulti(event, event.getActionIndex());
            else {
                gesture = SUPPRESSED;
                activePointer = event.getPointerId(event.getActionIndex() == 0 ? 1 : 0);
                firstMultiPointer = secondMultiPointer = -1;
            }
            return true;
        }
        if (action == MotionEvent.ACTION_UP) {
            removeCallbacks(longPress);
            int completed = gesture;
            if(completed==EDGE_DRAG||(completed==CONNECT_DRAG&&dragConnectionBranch!=null))updateConnectionDrag(worldX(event.getX()),worldY(event.getY()));
            String tapped = downNodeId, tappedEdge = downEdgeId;
            String movedEdge=dragEdgeId;
            String droppedSource = dragConnectionSource, droppedTarget = dragConnectionTarget;
            String droppedBranch=dragConnectionBranch;
            float droppedX = dragConnectionX, droppedY = dragConnectionY;
            boolean stillTap = Math.hypot(event.getRawX()-downRawX,event.getRawY()-downRawY)<=touchSlop;
            // Deliberately do not apply event.getX/Y to a drag or marquee here.
            // ACTION_UP can contain a later contact coordinate than the last MOVE.
            Runnable insertionRequest=null;
            if (completed == DRAG) {if(!finishTrashDrop())insertionRequest=finishDrag(true);}
            else if (completed == PLUS_PENDING && stillTap) {
                if (listener != null && tapped != null){if(downBranch==null)listener.onAddAfterRequested(tapped);else listener.onAddAfterRequested(tapped,downBranch);}
                lastTapTime = 0;
            } else if ((completed == PENDING || completed == EDGE_PENDING) && stillTap && tapped == null && tappedEdge != null && !readOnly && !selectionMode && !isConnecting()) {
                performClick(); lastTapTime = 0;
                if (listener != null && edgeIndex.containsKey(tappedEdge)) listener.onEdgeEditRequested(tappedEdge);
            } else if (completed == PENDING && stillTap) handleTap(event, tapped);
            gesture = NONE; activePointer = -1; downNodeId = downEdgeId = null;
            dragConnectionSource = dragConnectionTarget = null;
            dragEdgeId=null;
            downBranch=dragConnectionBranch=null;
            dragOrigins.clear();clearSticker();
            getParentDisallow(false); invalidate();
            if(insertionRequest!=null)insertionRequest.run();
            if(completed==EDGE_DRAG)finishEdgeDrag(movedEdge,droppedTarget,droppedX,droppedY);
            if (completed == CONNECT_DRAG && !readOnly && droppedSource != null) {
                FlowModel.Node source = document.find(droppedSource);
                FlowModel.Node target = document.find(droppedTarget);
                if (target != null && target.kind == FlowModel.START) hint("Il blocco Inizio non può ricevere una freccia.");
                else if (source != null && source.kind != FlowModel.END && source.kind != FlowModel.NOTE) {
                    if (listener != null){if(droppedBranch==null)listener.onConnectionDropRequested(droppedSource,droppedTarget,droppedX,droppedY);else listener.onConnectionDropRequested(droppedSource,droppedBranch,droppedTarget,droppedX,droppedY);}
                }
            }
            return true;
        }
        if (action == MotionEvent.ACTION_CANCEL) {
            lastTapTime = 0;
            if(groupDragging()){for(DragOrigin origin:dragOrigins){origin.node.x=origin.x;origin.node.y=origin.y;}dragChanged=false;dirty=true;resetGesture(false);}else resetGesture(true); getParentDisallow(false); return true;
        }
        return true;
    }

    @Override public boolean performClick() { super.performClick(); return true; }

    private void finishEdgeDrag(String edgeId,String targetId,float x,float y){
        EdgeVisual visual=edgeIndex.get(edgeId);FlowModel.Node target=document.find(targetId);
        if(!editableDecisionEdge(visual))return;
        FlowModel.Edge edge=null;for(FlowModel.Edge candidate:document.edges)if(candidate.id.equals(edgeId))edge=candidate;if(edge==null)return;
        if(target==null){if(listener!=null)listener.onConnectionDropRequested(edge.from,edge.branch,null,x,y);return;}
        if((target.kind==FlowModel.START||target.kind==FlowModel.NOTE)){
            hint("Arrivo invariato · trascina la punta su un altro blocco.");return;
        }
        if(same(edge.to,targetId)){hint("Questo è già l’arrivo della freccia.");return;}
        if(listener!=null)listener.onManualConnectionChange();
        beforeChange();edge.to=targetId;dirty=structureDirty=true;
        changed("Cambia arrivo della freccia");
        performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);
        hint((visual.label==null?"Freccia":visual.label)+" collegato a "+nonEmpty(target.text,FlowModel.kindName(target.kind))+" · Annulla per ripristinare");
        announceForAccessibility("Arrivo del ramo cambiato. Puoi annullare la modifica.");
    }

    private void handleTap(MotionEvent event, String tapped) {
        if (readOnly) {if(tapped!=null&&listener!=null)listener.onReadOnlyNodeTap(tapped);return;}
        performClick();
        if (isConnecting()) { requestConnectionTarget(tapped, downWorldX, downWorldY); lastTapTime = 0; return; }
        if (selectionMode) { if (tapped != null) toggleSelection(tapped); lastTapTime = 0; return; }
        long time = event.getEventTime();
        boolean sameTarget = tapped == null ? lastTapWasBlank : same(tapped, lastTapNode);
        boolean doubleTap = sameTarget && lastTapTime > 0 && time - lastTapTime <= ViewConfiguration.getDoubleTapTimeout() &&
                Math.hypot(downRawX-lastTapX,downRawY-lastTapY)<=dp(36);
        if (tapped == null) clearSelection();
        else if(isFoldCard(tapped)){applySelection(foldSelection(java.util.Collections.singleton(tapped)),true);if(doubleTap){FlowCycle.Info card=foldProjection.cards.get(tapped);changeFold(()->cycleFold.set(card.check,false),false);lastTapTime=0;return;}}
        else select(tapped);
        if (doubleTap) {
            lastTapTime = 0;
            if (listener != null) {
                if (tapped == null) listener.onAddRequested(downWorldX, downWorldY, null, "next");
                else listener.onEditRequested(tapped);
            }
        } else {
            lastTapTime = time; lastTapNode = tapped; lastTapWasBlank = tapped == null;
            lastTapX = downRawX; lastTapY = downRawY;
        }
    }

    private void clearInsertionPreview(){for(InsertionRoute route:insertionRoutes)route.path.reset();insertionRoutes.clear();insertionHover=null;insertionStrength=0;insertionReady=false;}
    private void captureInsertionRoutes(){
        clearInsertionPreview();if(downNodeId==null)return;ensureCache();
        if(groupDragging()){FlowModel.Document staged=insertionDocument==null?document.copy():insertionDocument;if(!FlowGroupPlacement.detach(staged,selected))return;FlowGroupPlacement.Plan plan=FlowGroupPlacement.analyze(staged,selected);BranchRoutingContext context=buildBranchRoutes(staged);for(FlowModel.Edge model:staged.edges)if(FlowGroupPlacement.reason(staged,plan,model,otherEdges.getAsInt())==null){NodeVisual from=nodeIndex.get(model.from),to=nodeIndex.get(model.to);if(from==null||to==null)continue;EdgeVisual edge=new EdgeVisual();edge.edge=model;edge.points=routeEdge(from,to,model.branch,new float[0],context);buildRoundedRoute(edge.path,edge.points);insertionRoutes.add(new InsertionRoute(edge,context));}return;}
        FlowModel.Document valid=insertionDocument==null?document:insertionDocument;
        BranchRoutingContext context=buildBranchRoutes(valid);for(FlowModel.Edge model:valid.edges)if(!model.from.equals(downNodeId)&&!model.to.equals(downNodeId)&&FlowInsertion.reason(valid,downNodeId,model.id,otherEdges.getAsInt())==null){NodeVisual from=nodeIndex.get(model.from),to=nodeIndex.get(model.to);if(from==null||to==null)continue;EdgeVisual edge=new EdgeVisual();edge.edge=model;edge.points=routeEdge(from,to,model.branch,new float[0],context);buildRoundedRoute(edge.path,edge.points);insertionRoutes.add(new InsertionRoute(edge,context));}
    }
    private void refreshInsertionRoutes(){
        ensureCache();for(InsertionRoute route:insertionRoutes){if(route.generation==geometryGeneration)continue;route.generation=geometryGeneration;
            NodeVisual from=nodeIndex.get(route.from),to=nodeIndex.get(route.to);if(from==null||to==null)continue;
            float[] points=routeEdge(from,to,route.branch,new float[0],route.context);
            if(java.util.Arrays.equals(route.points,points))continue;route.points=points.clone();buildRoundedRoute(route.path,route.points);route.measure.setPath(route.path,false);buildArrow(route.arrow,route.points);
        }
    }
    private void updateInsertionTarget(){
        FlowModel.Node n=paletteNode!=null?paletteNode:document.find(downNodeId);if(gesture!=DRAG||n==null||paletteNode==null&&!dragChanged){insertionHover=null;insertionReady=false;insertionStrength=0;return;}
        if(trashReady()&&trashBounds().contains(lastX,lastY)){insertionHover=null;insertionStrength=0;insertionReady=false;return;}
        refreshInsertionRoutes();
        float cx=n.x+width(n)/2,cy=n.y+height(n)/2;
        float influence=dp(120)/scale(),best=influence;InsertionRoute found=null;
        if(!blockedBypass.isEmpty()&&!bypassExited){float distance=Float.MAX_VALUE;for(InsertionRoute route:insertionRoutes)if(blockedBypass.contains(route.id))for(int i=2;i+1<route.points.length;i+=2)distance=Math.min(distance,(float)Math.sqrt(distanceToSegmentSquared(cx,cy,route.points[i-2],route.points[i-1],route.points[i],route.points[i+1])));bypassExited=distance>dp(56)/scale()||Math.hypot(lastX-downX,lastY-downY)>dp(56);}
        for(InsertionRoute route:insertionRoutes)if(bypassExited||!blockedBypass.contains(route.id))for(int i=2;i+1<route.points.length;i+=2){float[] p=route.points;float distance=(float)Math.sqrt(distanceToSegmentSquared(cx,cy,p[i-2],p[i-1],p[i],p[i+1]));if(distance<best){best=distance;found=route;}}
        boolean previous=insertionReady;insertionHover=found;insertionStrength=found==null?0:clamp(1-best/influence,0,1);insertionReady=found!=null&&best<=dp(44)/scale();
        if(previous!=insertionReady)hint(insertionReady?groupDragging()?"Rilascia per inserire tutti i blocchi in ordine. Annulla ripristina tutto.":"Rilascia per inserire il blocco nella freccia. Annulla ripristina tutto.":"Avvicina il blocco alla freccia: il blu indica dove puoi inserirlo.");
    }
    private void drawInsertionGlow(Canvas canvas){
        if(gesture!=DRAG||insertionHover==null||insertionStrength<=0)return;NodeVisual n=paletteNode!=null?paletteVisual:nodeIndex.get(downNodeId);if(n==null)return;
        RectF b=n.bounds;float radius=(float)Math.hypot(b.width(),b.height())/2+dp(48)/scale()*(.4f+.6f*insertionStrength);
        int saved=canvas.save();canvas.clipOutPath(n.shape);
        android.graphics.RadialGradient glow=new android.graphics.RadialGradient(b.centerX(),b.centerY(),radius,new int[]{0xFF5B8DEF,0xB05B8DEF,0x005B8DEF},new float[]{0,.45f,1},android.graphics.Shader.TileMode.CLAMP);
        strokePaint.setShader(glow);float[] widths={22,12,5,2.4f};int[] alphas={25,48,105,235};
        try{for(int i=0;i<widths.length;i++){strokePaint.setAlpha((int)(alphas[i]*insertionStrength));strokePaint.setStrokeWidth(widths[i]/zoom);canvas.drawPath(insertionHover.path,strokePaint);}}
        finally{strokePaint.setShader(null);strokePaint.setAlpha(255);canvas.restoreToCount(saved);}
    }
    private void drawInsertionHint(Canvas canvas){
        if(gestureHintMode()!=2)return;
        String message=insertionReady?(groupDragging()?"Rilascia per inserire il gruppo":"Rilascia per inserire"):"Avvicina il blocco alla freccia";labelPaint.setTextSize(dp(13)*Math.min(1.5f,getResources().getConfiguration().fontScale));
        NodeVisual moving=paletteNode!=null?paletteVisual:nodeIndex.get(downNodeId);if(moving==null)return;
        float width=Math.min(getWidth()-dp(24),labelPaint.measureText(message)+dp(24)),height=dp(44),left=clamp(screenX(moving.bounds.centerX())-width/2,dp(12),getWidth()-dp(12)-width),top=screenY(moving.bounds.top)-height-dp(12);
        if(top<dp(12))top=screenY(moving.bounds.bottom)+dp(12);top=clamp(top,dp(12),Math.max(dp(12),getHeight()-height-dp(12)));
        float reveal=canAnimate()?Math.min(1,(android.os.SystemClock.uptimeMillis()-holdArmedAt-400)/160f):1;int hintLayer=canvas.saveLayerAlpha(0,0,getWidth(),getHeight(),(int)(255*Math.max(0,reveal)));canvas.translate(0,dp(5)*(1-Ui.spring(Math.max(0,reveal))));if(reveal<1)postInvalidateOnAnimation();
        tempRect.set(left,top,left+width,top+height);fillPaint.setColor(dark?DARK_SURFACE:Color.WHITE);canvas.drawRoundRect(tempRect,dp(12),dp(12),fillPaint);
        strokePaint.setColor(ACCENT);strokePaint.setStrokeWidth(dp(1.3f));canvas.drawRoundRect(tempRect,dp(12),dp(12),strokePaint);
        labelPaint.setColor(dark?0xFF91B7FF:0xFF2F5FBF);message=ellipsize(message,width-dp(18),labelPaint);canvas.drawText(message,left+(width-labelPaint.measureText(message))/2,top+height/2+labelPaint.getTextSize()*.35f,labelPaint);
    }

    private final Set<String> landingGroup=new HashSet<>();
    private boolean groupSelected(){return downNodeId!=null&&selected.size()>1&&selected.contains(downNodeId);}
    private boolean groupDragging(){return paletteNode==null&&dragOrigins.size()>1;}
    private boolean heldNode(String id){return same(id,downNodeId)||groupSelected()&&selected.contains(id);}
    private boolean landingNode(String id){return same(id,landingId)||landingId!=null&&landingGroup.contains(id);}
    private boolean beginGroupLift(){
        if(!groupDragging())return false;
        if(!FlowGroupPlacement.eligible(document,selected)){cancelHoldFill();hint("Il gruppo si sposta insieme. Escludi Inizio e Fine per staccarlo.");return true;}
        stickerDetached=true;insertionDocument=document.copy();FlowGroupPlacement.detach(insertionDocument,selected);captureInsertionRoutes();cacheStickerBypass();holdFill=1;startLift();
        FlowGroupPlacement.Plan plan=FlowGroupPlacement.analyze(insertionDocument,selected);
        hint(selected.size()+" blocchi sollevati insieme · collegamenti interni conservati"+(plan.error==null?" · trascina su una freccia per reinserirli":" · "+plan.error));return true;
    }
    private Runnable finishGroupDrag(boolean allowInsertion){
        final FlowModel.Document expected=document;final String anchor=downNodeId;final Set<String> ids=new HashSet<>(selected);
        final Map<String,float[]> before=FlowPlacement.positions(document),presentation=presentationPositions();
        if(allowInsertion)updateInsertionTarget();final InsertionRoute route=allowInsertion&&dragChanged&&insertionReady?insertionHover:null;
        final boolean peeled=allowInsertion&&holdFill>=1&&FlowGroupPlacement.eligible(document,ids),moved=dragChanged;
        final Map<String,float[]> positions=FlowPlacement.positions(document);
        if(snapToGrid&&moved){DragOrigin a=dragOrigins.get(0);for(DragOrigin o:dragOrigins)if(o.node.id.equals(anchor))a=o;float dx=FlowGridSnap.round(a.node.x)-a.node.x,dy=FlowGridSnap.round(a.node.y)-a.node.y;for(String id:ids){float[] at=positions.get(id);at[0]+=dx;at[1]+=dy;}}
        if(route!=null){float[] previous=positions.get(anchor).clone();centerOnRoute(positions,anchor,route,before.get(anchor));float[] at=positions.get(anchor);float dx=at[0]-previous[0],dy=at[1]-previous[1];for(String id:ids)if(!id.equals(anchor)){float[] pos=positions.get(id);pos[0]+=dx;pos[1]+=dy;}}
        for(DragOrigin o:dragOrigins){o.node.x=o.x;o.node.y=o.y;}
        final String state=FlowCodec.encode(document);final ArrayList<EdgeSnapshot> previousEdges=captureEdges();dragOrigins.clear();dragChanged=dragHistoryStarted=false;clearSticker();clearInsertionPreview();dirty=true;
        if(!moved&&!peeled&&route==null)return null;
        return ()->{if(readOnly||document!=expected||!state.equals(FlowCodec.encode(document)))return;FlowModel.Document staged=document.copy();for(String id:ids){FlowModel.Node n=staged.find(id);float[] at=positions.get(id);n.x=at[0];n.y=at[1];}
            if(peeled||route!=null)FlowGroupPlacement.detach(staged,ids);
            if(route!=null){FlowModel.Edge edge=findEdge(staged,route.id);if(edge==null||!FlowGroupPlacement.arrange(staged,ids,edge.from,edge.to)||!FlowGroupPlacement.insert(staged,ids,route.id,otherEdges.getAsInt())){hint("Questo gruppo non può essere inserito qui · posizione e collegamenti originali conservati");animateLayout(before,FlowPlacement.positions(document),420);return;}}
            for(FlowModel.Node n:staged.nodes)if(n.x< -MAX_COORDINATE||n.x>MAX_COORDINATE||n.y< -MAX_COORDINATE||n.y>MAX_COORDINATE){hint("Il gruppo supera i limiti del foglio · nessuna modifica applicata");return;}
            if(listener!=null)listener.onManualNodeMove();beforeChange();if(document!=expected)return;
            for(FlowModel.Node n:document.nodes){FlowModel.Node next=staged.find(n.id);n.x=next.x;n.y=next.y;}document.edges.clear();document.edges.addAll(staged.edges);dirty=structureDirty=true;changed(route!=null?"Inserisci gruppo nella freccia":peeled?"Stacca gruppo":"Sposta blocchi");
            startLanding(anchor,peeled);landingGroup.addAll(ids);animatePresentation(presentation,420);if(peeled||route!=null)startEdgeMotion(previousEdges,anchor);
            hint(route!=null?ids.size()+" blocchi inseriti in ordine · Annulla ripristina l’intero gesto":peeled?"Gruppo staccato · collegamenti interni conservati · Annulla ripristina tutto":"Gruppo spostato · collegamenti conservati");
        };
    }

    private void startDrag() {
        if(isFoldCard(downNodeId))applySelection(foldSelection(java.util.Collections.singleton(downNodeId)),false);
        else if (!selected.contains(downNodeId)) select(downNodeId);
        dragOrigins.clear();
        for (FlowModel.Node node : document.nodes) if (selected.contains(node.id)) dragOrigins.add(new DragOrigin(node));
        gesture = DRAG; dragChanged = false; dragHistoryStarted = false;
        captureInsertionRoutes();
    }

    private void startConnectionDrag() {
        dragConnectionSource = downNodeId;
        dragConnectionBranch=downBranch;
        dragConnectionTarget = null;
        gesture = CONNECT_DRAG;
        stopConnectorPulse();
    }

    private void updateConnectionDrag(float x, float y) {
        dragConnectionX = clamp(x, -MAX_COORDINATE, MAX_COORDINATE);
        dragConnectionY = clamp(y, -MAX_COORDINATE, MAX_COORDINATE);
        NodeVisual target = hitConnectionTarget(dragConnectionX, dragConnectionY);
        String nextTarget = target == null ? null : target.node.id;
        if (!same(dragConnectionTarget, nextTarget)) {
            dragConnectionTarget = nextTarget;
            if (target != null && target.kind != FlowModel.START && target.kind != FlowModel.NOTE) {
                performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);
                announceForAccessibility("Collega a " + FlowModel.kindName(target.kind) + ": " + nonEmpty(target.node.text, placeholder(target.kind)));
            }
        }
        invalidate();
    }

    /** Magnets apply only to a connection gesture; selection keeps its precise hit test. */
    private NodeVisual hitConnectionTarget(float x, float y) {
        NodeVisual direct = hitNode(x, y);
        if (direct != null) return direct;
        float maximum = dp(20) / scale(), best = maximum * maximum;
        NodeVisual nearest = null;
        // Same reverse order as drawing, with distance deciding between nearby shapes.
        for (int i = nodes.size() - 1; i >= 0; i--) {
            NodeVisual candidate = nodes.get(i);
            // A loop needs a deliberate drop on its source; the outer magnet must not grab a small wobble at the +.
            if (same(candidate.node.id, dragConnectionSource)) continue;
            float distance = distanceToNodeSquared(candidate, x, y);
            if (distance < best) { best = distance; nearest = candidate; }
        }
        return nearest;
    }

    private static float distanceToNodeSquared(NodeVisual visual, float x, float y) {
        RectF b = visual.bounds;
        if (visual.kind == FlowModel.DECISION) {
            float nx = Math.abs(x - b.centerX()) / (b.width() / 2), ny = Math.abs(y - b.centerY()) / (b.height() / 2);
            if (nx + ny <= 1) return 0;
            return Math.min(Math.min(distanceToSegmentSquared(x, y, b.centerX(), b.top, b.right, b.centerY()),
                            distanceToSegmentSquared(x, y, b.right, b.centerY(), b.centerX(), b.bottom)),
                    Math.min(distanceToSegmentSquared(x, y, b.centerX(), b.bottom, b.left, b.centerY()),
                            distanceToSegmentSquared(x, y, b.left, b.centerY(), b.centerX(), b.top)));
        }
        if (visual.kind == FlowModel.INPUT || visual.kind == FlowModel.OUTPUT) {
            float proportion = (y - b.top) / b.height();
            if (proportion >= 0 && proportion <= 1 && x >= b.left + 19 * (1 - proportion) && x <= b.right - 19 * proportion) return 0;
            return Math.min(Math.min(distanceToSegmentSquared(x, y, b.left + 19, b.top, b.right, b.top),
                            distanceToSegmentSquared(x, y, b.right, b.top, b.right - 19, b.bottom)),
                    Math.min(distanceToSegmentSquared(x, y, b.right - 19, b.bottom, b.left, b.bottom),
                            distanceToSegmentSquared(x, y, b.left, b.bottom, b.left + 19, b.top)));
        }
        if (visual.kind == FlowModel.START || visual.kind == FlowModel.END) {
            float radius = b.height() / 2;
            float centerX = clamp(x, b.left + radius, b.right - radius);
            float distance = Math.max(0, (float)Math.hypot(x - centerX, y - b.centerY()) - radius);
            return distance * distance;
        }
        float dx = Math.max(Math.max(b.left - x, x - b.right), 0), dy = Math.max(Math.max(b.top - y, y - b.bottom), 0);
        return dx * dx + dy * dy;
    }

    private static float distanceToSegmentSquared(float px, float py, float ax, float ay, float bx, float by) {
        float dx = bx - ax, dy = by - ay, length = dx * dx + dy * dy;
        float t = length > 0 ? clamp(((px - ax) * dx + (py - ay) * dy) / length, 0, 1) : 0;
        dx = px - (ax + t * dx); dy = py - (ay + t * dy);
        return dx * dx + dy * dy;
    }

    private void moveDrag(float dx, float dy) {
        if (dragOrigins.isEmpty()) return;
        float minDx = -Float.MAX_VALUE, maxDx = Float.MAX_VALUE, minDy = -Float.MAX_VALUE, maxDy = Float.MAX_VALUE;
        for (DragOrigin original : dragOrigins) {
            minDx = Math.max(minDx, -MAX_COORDINATE - original.x); maxDx = Math.min(maxDx, MAX_COORDINATE - original.x);
            minDy = Math.max(minDy, -MAX_COORDINATE - original.y); maxDy = Math.min(maxDy, MAX_COORDINATE - original.y);
        }
        dx = clamp(dx, minDx, maxDx); dy = clamp(dy, minDy, maxDy);
        boolean difference = false;
        for (DragOrigin original : dragOrigins) {
            if (original.node.x != original.x + dx || original.node.y != original.y + dy) { difference = true; break; }
        }
        if (!difference) return;
        for (DragOrigin original : dragOrigins) { original.node.x = original.x + dx; original.node.y = original.y + dy; }
        dragChanged = true; dirty = true; pendingFit = false; invalidate();
    }

    private boolean snapToGrid=true;
    public boolean isSnapToGrid(){return snapToGrid;}
    public void setSnapToGrid(boolean enabled){snapToGrid=enabled;}

    private void finishDrag() {Runnable commit=finishDrag(false);if(commit!=null)commit.run();}
    private Runnable finishDrag(boolean allowInsertion) {
        if(groupDragging())return finishGroupDrag(allowInsertion);
        final Map<String,float[]> animationBefore=FlowPlacement.positions(document);final Map<String,float[]> presentationBefore=presentationPositions();final boolean hadFoldedCycles=foldProjection!=null;
        if(allowInsertion)updateInsertionTarget();
        final InsertionRoute magnetic=allowInsertion&&dragChanged&&insertionReady?insertionHover:null;
        if (dragChanged && snapToGrid && !dragOrigins.isEmpty()) {
            DragOrigin anchor=dragOrigins.get(0);
            float minDx=-Float.MAX_VALUE,maxDx=Float.MAX_VALUE,minDy=-Float.MAX_VALUE,maxDy=Float.MAX_VALUE;
            for(DragOrigin original:dragOrigins){
                if(original.node.id.equals(downNodeId))anchor=original;
                minDx=Math.max(minDx,-MAX_COORDINATE-original.x);maxDx=Math.min(maxDx,MAX_COORDINATE-original.x);
                minDy=Math.max(minDy,-MAX_COORDINATE-original.y);maxDy=Math.min(maxDy,MAX_COORDINATE-original.y);
            }
            float dx=FlowGridSnap.offset(anchor.x,anchor.node.x-anchor.x,minDx,maxDx);
            float dy=FlowGridSnap.offset(anchor.y,anchor.node.y-anchor.y,minDy,maxDy);
            for(DragOrigin original:dragOrigins){original.node.x=original.x+dx;original.node.y=original.y+dy;}
            dirty=true;
        }
        final String nodeId=downNodeId,edgeId=magnetic==null?null:magnetic.id;
        final boolean detach=allowInsertion&&stickerDetached,ambiguous=allowInsertion&&stickerAmbiguous,detachNone=stickerDetachNone;
        final boolean moved=dragChanged,single=dragOrigins.size()==1;
        final FlowModel.Document expected=document;final Map<String,float[]> finalPositions=FlowPlacement.positions(document);
        centerOnRoute(finalPositions,nodeId,magnetic,animationBefore.get(nodeId));
        final boolean peeled=liftFraction>0;
        for(DragOrigin origin:dragOrigins){origin.node.x=origin.x;origin.node.y=origin.y;}
        final String state=FlowCodec.encode(document);
        dragChanged=dragHistoryStarted=false;dragOrigins.clear();clearSticker();clearInsertionPreview();dirty=true;
        if(!moved&&!detach&&!ambiguous)return null;
        final PendingPlacement placement=new PendingPlacement(expected,nodeId,animationBefore,detach||ambiguous);pendingPlacement=placement;dirty=true;invalidate();
        final InsertionChoice detachedChoice=new InsertionChoice(){boolean used;
            public void choose(final String oldBranch){if(used)return;used=true;
                if(pendingPlacement!=placement||readOnly||document!=expected||!state.equals(FlowCodec.encode(document))){if(pendingPlacement==placement)cancelPlacement(placement);return;}
                if(ambiguous&&oldBranch==null){cancelPlacement(placement);hint("Stacco annullato · posizione e collegamenti conservati");return;}
                final FlowModel.Document staged=document.copy();
                if((detach||ambiguous)&&!detachPlanned(staged,nodeId,detach,detachNone,oldBranch)){cancelPlacement(placement);hint("Questo ramo non può richiudere il percorso senza un collegamento su se stesso");return;}
                final FlowModel.Edge candidate=findEdge(staged,edgeId);
                final String source=candidate==null?null:candidate.from,target=candidate==null?null:candidate.to;
                final boolean forward=candidate!=null&&staged.find(target).y>staged.find(source).y;
                InsertionChoice commit=new InsertionChoice(){boolean committed;public void choose(String newBranch){if(committed)return;committed=true;
                    if(pendingPlacement!=placement||readOnly||document!=expected||!state.equals(FlowCodec.encode(document))){if(pendingPlacement==placement)cancelPlacement(placement);return;}
                    if(edgeId!=null&&newBranch==null){cancelPlacement(placement);hint("Inserimento annullato · posizione e collegamenti conservati");return;}
                    final boolean splice=edgeId!=null&&!"skip".equals(newBranch);
                    if(splice&&!FlowInsertion.apply(staged,nodeId,edgeId,newBranch,otherEdges.getAsInt())){cancelPlacement(placement);hint("Inserimento non disponibile · collegamenti originali conservati");return;}
                    final ArrayList<EdgeSnapshot> previousEdges=captureEdges();
                    if(listener!=null)listener.onManualNodeMove();beforeChange();
                    if(document!=expected){clearPendingPlacement();return;}clearPendingPlacement();
                    for(FlowModel.Node node:document.nodes){float[] pos=(edgeId!=null&&!splice?animationBefore:finalPositions).get(node.id);if(pos!=null){node.x=pos[0];node.y=pos[1];}}
                    if(detach||ambiguous)detachPlanned(document,nodeId,detach,detachNone,oldBranch);
                    if(splice)FlowInsertion.apply(document,nodeId,edgeId,newBranch,otherEdges.getAsInt());
                    if(single)FlowPlacement.reflow(document,nodeId,splice?source:null,splice?target:null,splice&&forward);
                    dirty=structureDirty=true;changed(splice?"Inserisci blocco nella freccia":detach||ambiguous?"Stacca blocco":"Sposta blocchi");
                    startLanding(nodeId,peeled);if(hadFoldedCycles)animatePresentation(presentationBefore,420);else animateLayout(animationBefore,FlowPlacement.positions(document),420);
                    if(splice||detach||ambiguous)startEdgeMotion(previousEdges,nodeId);
                    hint(splice?"Blocco inserito · Annulla ripristina tutto":detach||ambiguous?"Blocco staccato · tutte le caselle restano sul foglio · Annulla ripristina tutto":"Posizione aggiornata · collegamenti conservati");
                }};
                FlowModel.Node node=document.find(nodeId);
                if(edgeId!=null&&node!=null&&node.kind==FlowModel.DECISION){if(listener!=null)listener.onNodeInsertionRequested(nodeId,edgeId,commit);else commit.choose(null);}else commit.choose("next");
            }
        };
        return ()->detachedChoice.choose(ambiguous?"none":null);
    }
    private void clearPendingPlacement(){pendingPlacement=null;dirty=true;invalidate();}
    private void cancelPlacement(PendingPlacement placement){if(pendingPlacement!=placement)return;clearPendingPlacement();if(document==placement.expected){startLanding(placement.id,placement.detached);animateLayout(placement.positions,FlowPlacement.positions(document),420);}}
    private void centerOnRoute(Map<String,float[]> positions,String id,InsertionRoute route,float[] release){
        FlowModel.Node n=paletteNode!=null&&paletteNode.id.equals(id)?paletteNode:document.find(id);if(route==null||n==null||release==null)return;
        float cx=release[0]+width(n)/2,cy=release[1]+height(n)/2,best=Float.MAX_VALUE,px=cx,py=cy;
        for(int i=2;i+1<route.points.length;i+=2){float ax=route.points[i-2],ay=route.points[i-1],dx=route.points[i]-ax,dy=route.points[i+1]-ay,l=dx*dx+dy*dy,t=l<.001f?0:clamp(((cx-ax)*dx+(cy-ay)*dy)/l,0,1),x=ax+t*dx,y=ay+t*dy,d=(x-cx)*(x-cx)+(y-cy)*(y-cy);if(d<best){best=d;px=x;py=y;}}
        positions.put(id,new float[]{clamp(px-width(n)/2,-MAX_COORDINATE,MAX_COORDINATE),clamp(py-height(n)/2,-MAX_COORDINATE,MAX_COORDINATE)});
    }
    private ArrayList<EdgeSnapshot> captureEdges(){ensureCache();ArrayList<EdgeSnapshot> result=new ArrayList<>();for(EdgeVisual e:edges)if(!skipStickerEdge(e.edge))result.add(new EdgeSnapshot(e));return result;}
    private void stopEdgeMotion(){ValueAnimator a=edgeMotionAnimator;edgeMotionAnimator=null;if(a!=null){a.removeAllUpdateListeners();a.removeAllListeners();a.cancel();}outgoingMotion.clear();enteringMotion.clear();edgeMotionFraction=1;fusionNodeId=null;invalidate();}
    private void startEdgeMotion(ArrayList<EdgeSnapshot> before,String node){stopEdgeMotion();if(!canAnimate())return;ensureCache();fusionNodeId=node;
        for(EdgeSnapshot old:before){EdgeVisual current=edgeIndex.get(old.id);if(current==null||!same(current.edge.from,old.from)||!same(current.edge.to,old.to)||!same(current.edge.branch,old.branch)){outgoingMotion.add(old);if(current!=null)enteringMotion.add(current.edge.id);}}
        for(EdgeVisual edge:edges){boolean existed=false;for(EdgeSnapshot old:before)if(same(old.id,edge.edge.id)&&same(old.from,edge.edge.from)&&same(old.to,edge.edge.to)){existed=true;break;}if(!existed)enteringMotion.add(edge.edge.id);}
        if(enteringMotion.isEmpty()&&outgoingMotion.isEmpty()){fusionNodeId=null;return;}
        edgeMotionFraction=0;final ValueAnimator a=ValueAnimator.ofFloat(0,1);edgeMotionAnimator=a;a.setDuration(420);a.setInterpolator(Ui.SPRING);a.addUpdateListener(v->{edgeMotionFraction=(float)v.getAnimatedValue();invalidate();});a.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator ignored){if(edgeMotionAnimator==a){edgeMotionAnimator=null;outgoingMotion.clear();enteringMotion.clear();edgeMotionFraction=1;fusionNodeId=null;invalidate();}}});a.start();
    }
    private void drawOutgoingMotion(Canvas canvas){if(edgeMotionAnimator==null)return;float amount=1-clamp(edgeMotionFraction,0,1);for(EdgeSnapshot edge:outgoingMotion){strokePaint.setColor((edge.color&0x00FFFFFF)|((int)(255*amount)<<24));strokePaint.setStrokeWidth((1.7f+2*amount)/Math.max(.6f,zoom));canvas.drawPath(edge.path,strokePaint);}}
    private void drawFusion(Canvas canvas){if(edgeMotionAnimator==null||fusionNodeId==null)return;NodeVisual n=nodeIndex.get(fusionNodeId);if(n==null)return;float t=clamp(edgeMotionFraction,0,1),pulse=(float)Math.sin(t*Math.PI),r=(4+12*pulse)/Math.max(.6f,zoom);fillPaint.setColor(((int)(65*pulse)<<24)|(ACCENT&0x00FFFFFF));for(float y:new float[]{n.bounds.top,n.bounds.bottom})canvas.drawCircle(n.bounds.centerX(),y,r,fillPaint);}
    private static FlowModel.Edge findEdge(FlowModel.Document d,String id){if(id==null)return null;for(FlowModel.Edge edge:d.edges)if(id.equals(edge.id))return edge;return null;}
    private static boolean detachPlanned(FlowModel.Document d,String id,boolean detach,boolean none,String branch){return none||"none".equals(branch)?FlowPlacement.disconnect(d,id):detach?FlowPlacement.detach(d,id):FlowPlacement.detachVia(d,id,branch);}

    private void startMarquee() {
        gesture = MARQUEE;
        marqueeBase.clear(); if (selectionMode) marqueeBase.addAll(selected);
        marquee.set(downWorldX, downWorldY, downWorldX, downWorldY);
        updateMarquee(downWorldX, downWorldY);
    }

    private void updateMarquee(float x, float y) {
        marquee.set(Math.min(downWorldX, x), Math.min(downWorldY, y), Math.max(downWorldX, x), Math.max(downWorldY, y));
        ensureCache();
        LinkedHashSet<String> next = new LinkedHashSet<>(marqueeBase);
        if (marquee.width() > 0 && marquee.height() > 0) {
            for (NodeVisual node : nodes) if (RectF.intersects(marquee, node.bounds)) next.add(node.node.id);
        }
        applySelection(next, false); invalidate();
    }

    private void finishSingleForMulti() {
        removeCallbacks(longPress);cancelHoldFill();
        if (gesture == DRAG) finishDrag();
        dragOrigins.clear(); gesture = MULTI; downNodeId = downEdgeId = null;dragEdgeId=null;dragConnectionSource=dragConnectionTarget=null;downBranch=dragConnectionBranch=null;
        dragConnectionSource = dragConnectionTarget = null;
        stopConnectorPulse();
        invalidate();
    }

    private void setupMulti(MotionEvent event, int skip) {
        int first = -1, second = -1;
        for (int i = 0; i < event.getPointerCount(); i++) {
            if (i == skip) continue;
            if (first < 0) first = i; else { second = i; break; }
        }
        if (second < 0) { gesture = SUPPRESSED; return; }
        firstMultiPointer = event.getPointerId(first); secondMultiPointer = event.getPointerId(second);
        multiX = (event.getX(first) + event.getX(second)) / 2;
        multiY = (event.getY(first) + event.getY(second)) / 2;
        multiDistance = Math.max(dp(8), (float) Math.hypot(event.getX(first) - event.getX(second), event.getY(first) - event.getY(second)));
        gesture = MULTI;
    }

    private void moveMulti(MotionEvent event) {
        int first = event.findPointerIndex(firstMultiPointer), second = event.findPointerIndex(secondMultiPointer);
        if (first < 0 || second < 0) { setupMulti(event, -1); return; }
        float cx = (event.getX(first) + event.getX(second)) / 2;
        float cy = (event.getY(first) + event.getY(second)) / 2;
        float distance = Math.max(dp(8), (float) Math.hypot(event.getX(first) - event.getX(second), event.getY(first) - event.getY(second)));
        float worldPivotX = worldX(multiX), worldPivotY = worldY(multiY);
        zoom = clamp(zoom * distance / multiDistance, minimumZoom(), MAX_ZOOM);
        panX = cx - worldPivotX * scale(); panY = cy - worldPivotY * scale();
        constrainViewport();
        multiX = cx; multiY = cy; multiDistance = distance;
        pendingFit = false; invalidate();
    }

    private void resetGesture(boolean commitDrag) {
        removeCallbacks(longPress);
        boolean overlayChanged = gesture == MARQUEE || gesture == MULTI || gesture == CONNECT_DRAG || gesture == EDGE_DRAG || gesture == EDGE_PENDING;
        if (commitDrag && gesture == DRAG) finishDrag();
        clearInsertionPreview();
        clearSticker();clearPalettePreview();
        dragOrigins.clear(); dragChanged = dragHistoryStarted = false;
        gesture = NONE; activePointer = firstMultiPointer = secondMultiPointer = -1;
        downNodeId = downEdgeId = null;
        dragConnectionSource = dragConnectionTarget = null;
        dragEdgeId=null;
        downBranch=dragConnectionBranch=null;
        if (overlayChanged) invalidate();
    }

    private void requestConnectionTarget(String targetId, float x, float y) {
        if (readOnly || !isConnecting()) return;
        FlowModel.Node source = document.find(connectionSource), target = document.find(targetId);
        if (source == null || (source.kind == FlowModel.END||source.kind==FlowModel.NOTE)) { cancelConnection(); return; }
        if (target != null && target.kind == FlowModel.START) { hint("Il blocco Inizio non può ricevere una freccia."); return; }
        if (targetId != null && target == null) return;
        if (listener != null && listener.onConnectionTargetRequested(connectionSource, connectionBranch, targetId, x, y)) return;
        completeConnection(targetId, x, y);
    }

    private void completeConnection(String targetId, float x, float y) {
        if (readOnly) return;
        FlowModel.Node source = document.find(connectionSource);
        if (source == null || (source.kind == FlowModel.END||source.kind==FlowModel.NOTE)) { cancelConnection(); return; }
        if (targetId == null) {
            if (listener != null) listener.onAddRequested(x, y, connectionSource, connectionBranch);
            return;
        }
        FlowModel.Node target = document.find(targetId);
        if (target == null) return;
        if (target.kind == FlowModel.START) { hint("Il blocco Inizio non può ricevere una freccia."); return; }
        FlowModel.Edge existing = document.outgoing(connectionSource, connectionBranch);
        if (existing != null && same(existing.to, targetId)) { cancelConnection(); select(targetId); return; }
        if (existing == null && document.edges.size()+otherEdges.getAsInt() >= FlowModel.MAX_EDGES) { hint("Il diagramma ha raggiunto il limite di collegamenti."); return; }
        String sourceId = connectionSource, branch = connectionBranch;
        if(listener!=null)listener.onManualConnectionChange();
        beforeChange();
        if (existing != null) {
            existing.to = targetId;
            for (Iterator<FlowModel.Edge> it = document.edges.iterator(); it.hasNext();) {
                FlowModel.Edge edge = it.next();
                if (edge != existing && same(edge.from, sourceId) && same(edge.branch, branch)) it.remove();
            }
        } else document.edges.add(new FlowModel.Edge(sourceId, targetId, branch));
        dirty = structureDirty = true;
        cancelConnection(); select(targetId);
        changed("Collega blocchi");
        performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK);
    }

    private NodeVisual hitNode(float x, float y) {
        ensureCache();
        // Reverse order matches the drawing order when blocks overlap.
        for (int i = nodes.size() - 1; i >= 0; i--) {
            NodeVisual visual = nodes.get(i); RectF b = visual.bounds;
            float tolerance = Math.min(12, dp(5) / scale());
            if (x < b.left - tolerance || x > b.right + tolerance || y < b.top - tolerance || y > b.bottom + tolerance) continue;
            if (visual.kind == FlowModel.DECISION) {
                float rx = b.width() / 2 + tolerance, ry = b.height() / 2 + tolerance;
                if (Math.abs(x - b.centerX()) / rx + Math.abs(y - b.centerY()) / ry > 1.10f) continue;
            } else if (visual.kind == FlowModel.INPUT || visual.kind == FlowModel.OUTPUT) {
                float proportion = clamp((y - b.top) / b.height(), 0, 1);
                float left = b.left + 19 * (1 - proportion), right = b.right - 19 * proportion;
                if (x < left - tolerance || x > right + tolerance) continue;
            }
            return visual;
        }
        return null;
    }

    private EdgeVisual hitEdge(float x, float y) {
        ensureCache();
        float tolerance = dp(12) / scale(), best = tolerance * tolerance;
        EdgeVisual nearest = null;
        for (int i = edges.size() - 1; i >= 0; i--) {
            EdgeVisual edge = edges.get(i);
            if (edge.label != null && x >= edge.labelBounds.left - tolerance / 2 && x <= edge.labelBounds.right + tolerance / 2 &&
                    y >= edge.labelBounds.top - tolerance / 2 && y <= edge.labelBounds.bottom + tolerance / 2) return edge;
            RectF b = edge.bounds;
            if (x < b.left - tolerance || x > b.right + tolerance || y < b.top - tolerance || y > b.bottom + tolerance) continue;
            float[] points = edge.points;
            for (int j = 2; j + 1 < points.length; j += 2) {
                float distance = distanceToSegmentSquared(x, y, points[j - 2], points[j - 1], points[j], points[j + 1]);
                if (distance < best) { best = distance; nearest = edge; }
            }
        }
        return nearest;
    }

    private void toggleSelection(String id) {
        if (document.find(id) == null) return;
        Set<String> group=foldSelection(java.util.Collections.singleton(id));if(selected.containsAll(group))selected.removeAll(group);else selected.addAll(group);
        selectionChanged(); announceSelection();
    }

    private void applySelection(Set<String> next, boolean announce) {
        next=foldSelection(next);
        if (selected.equals(next)) return;
        selected.clear(); selected.addAll(next);
        selectionChanged();
        if (announce) announceSelection();
    }

    private void selectionChanged() {
        if(keyboardNodeId!=null){String visible=foldOwner(keyboardNodeId);keyboardNodeId=selected.contains(keyboardNodeId)&&nodeIndex.containsKey(visible)?visible:null;}
        stopConnectorPulse();
        if (listener != null) listener.onSelectionChanged();
        invalidate();
        sendAccessibilityEvent(AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED);
    }

    private void announceSelection() {
        AccessibilityManager manager = (AccessibilityManager) getContext().getSystemService(Context.ACCESSIBILITY_SERVICE);
        if (manager == null || !manager.isEnabled()) return;
        if (selected.size() == 1) {
            FlowModel.Node node = document.find(selected.iterator().next());
            if (node != null) announceForAccessibility(FlowModel.kindName(node.kind) + ", " + nonEmpty(node.text, placeholder(node.kind)) + ", selezionato");
        } else if (selected.size() > 1) announceForAccessibility(selected.size() + " blocchi selezionati");
    }

    private void beforeChange() { if (listener != null) listener.onBeforeChange(); }
    private void changed(String label) {
        FlowAnnotations.promote(document);foldModelDirty=true;
        if (listener != null) listener.onDiagramChanged(label);
        invalidate(); sendAccessibilityEvent(AccessibilityEvent.TYPE_WINDOW_CONTENT_CHANGED);
    }
    private void hint(String text) { if (listener != null) listener.onHint(text); }
    private void getParentDisallow(boolean disallow) { if (getParent() != null) getParent().requestDisallowInterceptTouchEvent(disallow); }
    private float dp(float value) { return value * density; }
    private float scale() { return Math.max(FlowViewport.ABSOLUTE_MIN_ZOOM, zoom * density); }
    private float worldX(float x) { return (x - panX) / scale(); }
    private float worldY(float y) { return (y - panY) / scale(); }
    private float screenX(float x) { return x * scale() + panX; }
    private float screenY(float y) { return y * scale() + panY; }
    private FlowViewport.Fit viewportFit() {
        ensureCache();
        return FlowViewport.fit(Math.max(1,getWidth()),Math.max(1,getHeight()),density,mobileViewport,
                diagramBounds.left,diagramBounds.top,diagramBounds.right,diagramBounds.bottom);
    }
    private float minimumZoom() { return viewportFit().minimumZoom; }
    private float mobileReadingZoom() {
        if (getWidth() <= 0) return MOBILE_READING_ZOOM;
        return clamp((getWidth() / density - 32) / FlowModel.width(FlowModel.DECISION), MOBILE_READING_MIN_ZOOM, MOBILE_READING_ZOOM);
    }
    private float focusScreenY() { return (getHeight() + dp(mobileViewport ? 0 : -48)) / 2f; }

    private void constrainViewport() {
        if (!mobileViewport || getWidth() == 0 || getHeight() == 0 || document.nodes.isEmpty()) return;
        FlowViewport.Fit fit=viewportFit();
        float scale=scale();
        // Keep routes and labels within reach as well as the node rectangles.
        // Never force an overview back to the first node or shift it off-screen.
        panX=FlowViewport.constrainPan(panX,diagramBounds.left,diagramBounds.right,scale,fit.left,fit.right);
        panY=FlowViewport.constrainPan(panY,diagramBounds.top,diagramBounds.bottom,scale,fit.top,fit.bottom);
    }

    private void zoomAt(float value, float x, float y) {
        float worldX = worldX(x), worldY = worldY(y);
        zoom = clamp(value, minimumZoom(), MAX_ZOOM);
        panX = x - worldX * scale(); panY = y - worldY * scale();
        constrainViewport();
        pendingFit = false; invalidate();
    }
    private static float positiveModulo(float value, float divisor) { return ((value % divisor) + divisor) % divisor; }
    private static float clamp(float value, float low, float high) { return Math.max(low, Math.min(high, value)); }
    private static boolean same(String a, String b) { return a == null ? b == null : a.equals(b); }
    private static String nonEmpty(String value, String fallback) { return value == null || value.trim().isEmpty() ? fallback : value; }
    private static int safeColor(String value, int fallback) {
        if (value == null || !value.matches("#[0-9a-fA-F]{6}")) return fallback;
        try { return Color.parseColor(value); } catch (IllegalArgumentException exception) { return fallback; }
    }
    private static String placeholder(int kind) {
        switch (kind) {
            case FlowModel.START: return "Inizio"; case FlowModel.END: return "Fine";
            case FlowModel.INPUT: return "Nome variabile"; case FlowModel.OUTPUT: return "Valore da mostrare";
            case FlowModel.NOTE:return "Scrivi una nota";case FlowModel.DECLARE:return "nome: TIPO";case FlowModel.CALL: return "nomeFunzione()"; case FlowModel.DECISION: return "Scrivi una condizione"; default: return "Scrivi un’operazione";
        }
    }
    static String typeLabel(int kind) {
        switch (kind) {
            case FlowModel.INPUT: return FlowModel.separateIo?"INPUT":"INPUT / OUTPUT"; case FlowModel.OUTPUT: return FlowModel.separateIo?"OUTPUT":"INPUT / OUTPUT";
            case FlowModel.TURTLE_FORWARD:return "AVANZA";case FlowModel.TURTLE_TURN:return "RUOTA";case FlowModel.TURTLE_HOME:return "ORIGINE TARTARUGA";case FlowModel.TURTLE_CLEAR:return "PULISCI DISEGNO";case FlowModel.TURTLE_PEN:return "COLORE PENNA";case FlowModel.OPEN_FILE:return "APRI FILE";case FlowModel.READ_FILE:return "LEGGI FILE";case FlowModel.WRITE_FILE:return "SCRIVI FILE";case FlowModel.CLOSE_FILE:return "CHIUDI FILE";case FlowModel.NOTE:return "NOTA";case FlowModel.PAUSE:return "PAUSA";case FlowModel.DECLARE:return "DICHIARA";case FlowModel.CALL: return "CHIAMATA"; case FlowModel.DECISION: return "CONDIZIONE"; default: return "OPERAZIONE";
        }
    }
    private static int kindColor(int kind, boolean dark) {
        switch (kind) {
            case FlowModel.NOTE:return dark?0xFFB1B1BA:0xFF667085;
            case FlowModel.PAUSE: return dark ? 0xFFFF8D9D : 0xFFBE294A;
            case FlowModel.START: return dark ? 0xFF73D5AE : 0xFF238768;
            case FlowModel.END: return dark ? 0xFFAAB3C3 : 0xFF737E91;
            case FlowModel.INPUT: return dark ? 0xFF75BEEC : 0xFF388DBD;
            case FlowModel.OUTPUT: return dark ? 0xFFBC99FF : 0xFF814BCE;
            case FlowModel.DECISION: return dark ? 0xFFE9C06E : 0xFFAD7B24;
            default: return dark ? 0xFF94B4FF : 0xFF5479C6;
        }
    }

    private static String[] wrapText(String text, float width, int maxLines, Paint paint) {
        String value = text.replace("\r\n", "\n").replace('\r', '\n').replace('\t', ' ');
        ArrayList<String> lines = new ArrayList<>();
        int offset = 0;
        while (offset < value.length() && lines.size() < maxLines) {
            int newline = value.indexOf('\n', offset);
            int paragraphEnd = newline < 0 ? value.length() : newline;
            if (paragraphEnd == offset) { lines.add(""); offset++; continue; }
            int fit = paint.breakText(value, offset, paragraphEnd, true, width, null);
            fit = Math.max(1, fit);
            int end = Math.min(paragraphEnd, offset + fit);
            if (end < value.length() && end > offset && Character.isHighSurrogate(value.charAt(end - 1))) end--;
            if (end <= offset) end = Math.min(paragraphEnd, offset + Character.charCount(value.codePointAt(offset)));
            if (end < paragraphEnd) {
                int breakAt = value.lastIndexOf(' ', end - 1);
                if (breakAt > offset + Math.max(1, (end - offset) / 3)) end = breakAt;
            }
            String line = value.substring(offset, end).trim();
            int next = end;
            while (next < value.length() && value.charAt(next) == ' ') next++;
            if (next == paragraphEnd && newline >= 0) next++;
            if (lines.size() == maxLines - 1 && next < value.length()) line = ellipsize(line + "…", width, paint);
            lines.add(line); offset = next;
        }
        if (lines.isEmpty()) lines.add("");
        return lines.toArray(new String[0]);
    }

    private static String ellipsize(String value, float width, Paint paint) {
        if (paint.measureText(value) <= width) return value;
        float available = Math.max(0, width - paint.measureText("…"));
        int count = paint.breakText(value, true, available, null);
        if (count > 0 && count < value.length() && Character.isHighSurrogate(value.charAt(count - 1))) count--;
        return value.substring(0, Math.max(0, count)).trim() + "…";
    }

    /** SVG uses the same cached paths, labels and wrapped text as bitmap/PDF. */
    public String createSvg() {
        boolean previous = beginModelGeometry();
        try { return appearance.svg(createSvgInternal()); }
        finally { endModelGeometry(previous); }
    }

    private String createSvgInternal() {
        ensureCache();
        RectF bounds = new RectF(diagramBounds); bounds.inset(-24, -24);
        StringBuilder svg = new StringBuilder(Math.max(1024, nodes.size() * 900 + edges.size() * 450));
        svg.append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
                .append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").append(number(bounds.width()))
                .append("\" height=\"").append(number(bounds.height())).append("\" viewBox=\"")
                .append(number(bounds.left)).append(' ').append(number(bounds.top)).append(' ')
                .append(number(bounds.width())).append(' ').append(number(bounds.height())).append("\">\n")
                .append("<title>").append(xml(nonEmpty(document.title, "Diagramma Frociart"))).append("</title>\n")
                .append("<rect x=\"").append(number(bounds.left)).append("\" y=\"").append(number(bounds.top))
                .append("\" width=\"").append(number(bounds.width())).append("\" height=\"")
                .append(number(bounds.height())).append("\" fill=\"#FFFFFF\"/>\n");
        for (EdgeVisual edge : edges) {
            String color = colorHex(edge.label == null ? 0xFF8891A2 : edge.color);
            svg.append("<path d=\"").append(svgRoute(edge.points)).append("\" fill=\"none\" stroke=\"").append(color)
                    .append("\" stroke-width=\"1.7\" stroke-linecap=\"round\" stroke-linejoin=\"round\"/>\n");
            if (edge.points.length >= 4) {
                int i = edge.points.length - 2;
                float x = edge.points[i], y = edge.points[i + 1], dx = x - edge.points[i - 2], dy = y - edge.points[i - 1];
                float length = (float) Math.hypot(dx, dy);
                if (length > 0.01f) {
                    dx /= length; dy /= length;
                    svg.append("<path d=\"M").append(number(x)).append(' ').append(number(y))
                            .append('L').append(number(x - dx * 8 + dy * 4)).append(' ').append(number(y - dy * 8 - dx * 4))
                            .append('L').append(number(x - dx * 8 - dy * 4)).append(' ').append(number(y - dy * 8 + dx * 4))
                            .append("Z\" fill=\"").append(color).append("\"/>\n");
                }
            }
            if (edge.label != null) {
                RectF b = edge.labelBounds;
                svg.append("<rect x=\"").append(number(b.left)).append("\" y=\"").append(number(b.top))
                        .append("\" width=\"").append(number(b.width())).append("\" height=\"").append(number(b.height()))
                        .append("\" rx=\"6\" fill=\"#FFFFFF\"/>\n");
                appendSvgText(svg, edge.label, b.centerX(), b.centerY() + 3.6f, 10.5f, color, true);
            }
        }
        for (NodeVisual visual : nodes) {
            RectF b = visual.bounds;
            int kind = visual.kind;
            String fill = "#FFFFFF";
            if (kind == FlowModel.DECISION) {
                svg.append("<path d=\"M").append(number(b.centerX())).append(' ').append(number(b.top))
                        .append('L').append(number(b.right)).append(' ').append(number(b.centerY()))
                        .append('L').append(number(b.centerX())).append(' ').append(number(b.bottom))
                        .append('L').append(number(b.left)).append(' ').append(number(b.centerY())).append("Z\"");
            } else if (kind == FlowModel.INPUT || kind == FlowModel.OUTPUT) {
                svg.append("<path d=\"M").append(number(b.left + 19)).append(' ').append(number(b.top))
                        .append('L').append(number(b.right)).append(' ').append(number(b.top))
                        .append('L').append(number(b.right - 19)).append(' ').append(number(b.bottom))
                        .append('L').append(number(b.left)).append(' ').append(number(b.bottom)).append("Z\"");
            } else svg.append("<rect x=\"").append(number(b.left)).append("\" y=\"").append(number(b.top))
                    .append("\" width=\"").append(number(b.width())).append("\" height=\"").append(number(b.height()))
                    .append("\" rx=\"").append(number(kind == FlowModel.START || kind == FlowModel.END ? Math.min(29,b.height() / 2) : 11)).append('"');
            if(kind==FlowModel.NOTE)svg.append(" stroke-dasharray=\"6 4\"");
            svg.append(" fill=\"").append(fill).append("\" stroke=\"#E4E6ED\" stroke-width=\"1.15\"/>\n");
            if(kind==FlowModel.CALL)for(float x:new float[]{b.left+10,b.right-10})svg.append("<path d=\"M").append(number(x)).append(' ').append(number(b.top+3)).append('L').append(number(x)).append(' ').append(number(b.bottom-3)).append("\" fill=\"none\" stroke=\"#E4E6ED\" stroke-width=\"1.15\"/>\n");
            String typeColor = colorHex(appearance.mode==3?appearance.kind(kind,kindColor(kind,false),false):kindColor(kind,false));
            if (kind == FlowModel.START || kind == FlowModel.END) {
                float iconScale=appearance.design().scale();float x = b.left + 30*iconScale, y = b.centerY();
                svg.append("<circle cx=\"").append(number(x)).append("\" cy=\"").append(number(y))
                        .append("\" r=\"").append(number(11*iconScale)).append("\" fill=\"").append(typeColor).append("\" fill-opacity=\"0.133\"/>\n");
                if (kind == FlowModel.START) svg.append("<path d=\"M").append(number(x - 3*iconScale)).append(' ').append(number(y - 5*iconScale))
                        .append('L').append(number(x + 5*iconScale)).append(' ').append(number(y)).append('L').append(number(x - 3*iconScale)).append(' ')
                        .append(number(y + 5*iconScale)).append("Z\" fill=\"").append(typeColor).append("\"/>\n");
                else svg.append("<rect x=\"").append(number(x - 4*iconScale)).append("\" y=\"").append(number(y - 4*iconScale))
                        .append("\" width=\"").append(number(8*iconScale)).append("\" height=\"").append(number(8*iconScale)).append("\" rx=\"").append(number(1.5f*iconScale)).append("\" fill=\"").append(typeColor).append("\"/>\n");
            } else appendSvgText(svg, visual.typeLabel, b.centerX(), b.top + visual.labelY, 9.3f*appearance.design().scale(), typeColor, true,appearance.design());
            for (int i = 0; i < visual.lines.length; i++) appendSvgText(svg, visual.lines[i], b.left + visual.bodyCenterX,
                    b.top + visual.bodyY + i * visual.bodyLineHeight, visual.bodySize, "#20222A", kind == FlowModel.START || kind == FlowModel.END,appearance.design());
        }
        return svg.append("</svg>\n").toString();
    }

    private static String svgRoute(float[] p) {
        if (p.length < 2) return "";
        StringBuilder path = new StringBuilder("M").append(number(p[0])).append(' ').append(number(p[1]));
        for (int i = 2; i + 3 < p.length; i += 2) {
            float px = p[i - 2], py = p[i - 1], x = p[i], y = p[i + 1], nx = p[i + 2], ny = p[i + 3];
            float before = (float) Math.hypot(x - px, y - py), after = (float) Math.hypot(nx - x, ny - y);
            float radius = Math.min(8, Math.min(before / 2, after / 2));
            if (before < 0.01f || after < 0.01f) { path.append('L').append(number(x)).append(' ').append(number(y)); continue; }
            path.append('L').append(number(x - (x - px) / before * radius)).append(' ').append(number(y - (y - py) / before * radius))
                    .append('Q').append(number(x)).append(' ').append(number(y)).append(' ')
                    .append(number(x + (nx - x) / after * radius)).append(' ').append(number(y + (ny - y) / after * radius));
        }
        if (p.length >= 4) path.append('L').append(number(p[p.length - 2])).append(' ').append(number(p[p.length - 1]));
        return path.toString();
    }

    private static void appendSvgText(StringBuilder svg, String value, float x, float y, float size, String color, boolean medium) {appendSvgText(svg,value,x,y,size,color,medium,FlowDesign.DEFAULT);}
    private static void appendSvgText(StringBuilder svg,String value,float x,float y,float size,String color,boolean medium,FlowDesign design) {
        svg.append("<text x=\"").append(number(x)).append("\" y=\"").append(number(y)).append("\" text-anchor=\"middle\" font-family=\"").append(design.svgFamily()).append("\" font-size=\"")
                .append(number(size)).append("\" font-weight=\"").append(medium ? "500" : "400").append("\" fill=\"").append(color).append("\">")
                .append(xml(value)).append("</text>\n");
    }
    private static String number(float value) {
        if (!Float.isFinite(value)) return "0";
        long rounded = Math.round((double) value * 100);
        boolean negative = rounded < 0;
        long absolute = Math.abs(rounded), whole = absolute / 100, fraction = absolute % 100;
        String integer = (negative ? "-" : "") + whole;
        if (fraction == 0) return integer;
        if (fraction % 10 == 0) return integer + "." + fraction / 10;
        return integer + "." + (fraction < 10 ? "0" : "") + fraction;
    }
    private static String colorHex(int value) {
        String hex = Integer.toHexString(value & 0xFFFFFF).toUpperCase(Locale.ROOT);
        return "#" + "000000".substring(hex.length()) + hex;
    }
    private static String xml(String value) {
        if (value == null) return "";
        StringBuilder result = new StringBuilder(value.length() + 16);
        for (int offset = 0; offset < value.length();) {
            int cp = value.codePointAt(offset); offset += Character.charCount(cp);
            if (cp == '&') result.append("&amp;"); else if (cp == '<') result.append("&lt;");
            else if (cp == '>') result.append("&gt;"); else if (cp == '"') result.append("&quot;");
            else if (cp == '\'') result.append("&apos;");
            else if (cp == 9 || cp == 10 || cp == 13 || cp >= 32 && cp <= 0xD7FF || cp >= 0xE000 && cp <= 0xFFFD || cp >= 0x10000 && cp <= 0x10FFFF) result.appendCodePoint(cp);
        }
        return result.toString();
    }

    @Override public AccessibilityNodeProvider getAccessibilityNodeProvider() { return accessibility; }

    @Override public boolean dispatchHoverEvent(MotionEvent event) {
        AccessibilityManager manager = (AccessibilityManager) getContext().getSystemService(Context.ACCESSIBILITY_SERVICE);
        if (manager == null || !manager.isTouchExplorationEnabled()) return super.dispatchHoverEvent(event);
        int next = -1;
        if (event.getActionMasked() != MotionEvent.ACTION_HOVER_EXIT) {
            NodeVisual node = hitNode(worldX(event.getX()), worldY(event.getY()));
            if (node != null) next = accessibilityNodeId(node.node.id);
            else {
                EdgeVisual edge = hitEdge(worldX(event.getX()), worldY(event.getY()));
                if (edge != null) next = accessibilityEdgeId(edge.edge.id);
            }
        }
        if (next != hoverVirtualId) {
            int old = hoverVirtualId; hoverVirtualId = next;
            if (next > 0) sendVirtualEvent(next, AccessibilityEvent.TYPE_VIEW_HOVER_ENTER);
            if (old > 0) sendVirtualEvent(old, AccessibilityEvent.TYPE_VIEW_HOVER_EXIT);
        }
        return next > 0;
    }

    private void sendVirtualEvent(int virtualId,int type){
        if(getParent()==null||virtualId<=0)return;FlowVirtualIds.Entry entry=accessibility.binding(virtualId);if(entry==null)return;
        AccessibilityEvent event=AccessibilityEvent.obtain(type);event.setPackageName(getContext().getPackageName());event.setClassName("android.widget.Button");event.setSource(this,virtualId);event.setEnabled(isEnabled());
        if(entry.key.startsWith("n:")){NodeVisual node=nodeIndex.get(entry.id);event.getText().add(FlowModel.kindName(node.kind)+": "+nonEmpty(node.node.text,placeholder(node.kind)));}
        else event.getText().add(edgeDescription(edgeIndex.get(entry.id)));
        getParent().requestSendAccessibilityEvent(this,event);
    }

    private String edgeDescription(EdgeVisual edge) {
        return "Collegamento da " + FlowModel.kindName(edge.from.kind) + ": " + nonEmpty(edge.from.node.text, placeholder(edge.from.kind)) +
                " a " + FlowModel.kindName(edge.to.kind) + ": " + nonEmpty(edge.to.node.text, placeholder(edge.to.kind)) +
                (edge.rawLabel == null ? "" : ", ramo " + edge.rawLabel);
    }

    private boolean visibleEdge(EdgeVisual edge) {
        return screenX(edge.bounds.right) >= 0 && screenX(edge.bounds.left) <= getWidth() &&
                screenY(edge.bounds.bottom) >= 0 && screenY(edge.bounds.top) <= getHeight();
    }

    private String keyboardNodeId;
    private long keyboardOrderGeneration=-1;
    private FlowModel.Document keyboardOrderDocument;
    private List<String> keyboardOrder=Collections.emptyList();
    private final Set<Integer> keyboardConsumedKeys=new HashSet<>();
    private List<String> keyboardOrder(){
        ensureCache();if(keyboardOrderDocument==document&&keyboardOrderGeneration==semanticGeneration)return keyboardOrder;
        List<String> ids=new ArrayList<>();Set<String> starts=new HashSet<>();List<String[]> links=new ArrayList<>();
        for(NodeVisual node:nodes){ids.add(node.node.id);if(node.kind==FlowModel.START)starts.add(node.node.id);}
        for(EdgeVisual edge:edges)links.add(new String[]{edge.edge.from,edge.edge.to,edge.edge.branch});
        keyboardOrder=FlowCanvasNavigation.order(ids,starts,links);keyboardOrderDocument=document;keyboardOrderGeneration=semanticGeneration;return keyboardOrder;
    }
    private String currentKeyboardNode(){
        if(keyboardNodeId!=null&&nodeIndex.containsKey(keyboardNodeId)&&selected.contains(keyboardNodeId))return keyboardNodeId;
        for(String id:selected){String visible=foldOwner(id);if(nodeIndex.containsKey(visible))return visible;}return null;
    }
    private boolean focusKeyboardNode(String id){
        ensureCache();NodeVisual visual=nodeIndex.get(id);if(visual==null)return false;
        final FlowModel.Document expected=document;final FlowModel.Node model=document.find(id);if(model==null)return false;
        keyboardNodeId=id;applySelection(foldSelection(Collections.singleton(id)),true);
        if(document!=expected||document.find(id)!=model||isInteracting()||!isEnabled()||!isShown())return true;
        focusNodeAnimated(id,false);
        if(document==expected&&document.find(id)==model)sendVirtualEvent(accessibilityNodeId(id),AccessibilityEvent.TYPE_VIEW_FOCUSED);
        return true;
    }
    private boolean activateKeyboardNode(){
        String id=currentKeyboardNode();if(id==null)return false;FlowModel.Node node=document.find(id);if(node==null)return false;
        if(isConnecting()){requestConnectionTarget(id,node.x,node.y);return true;}
        if(readOnly)return navigationPreview&&listener!=null&&listener.onReadOnlyNodeTap(id);
        if(isFoldCard(id)){toggleSelectedCycle();return true;}
        if(listener==null)return false;listener.onEditRequested(id);return true;
    }
    private boolean canvasKey(int key,android.view.KeyEvent event){
        if(!isEnabled()||!isShown()||!hasFocus()||!hasWindowFocus())return false;
        boolean menu=key==android.view.KeyEvent.KEYCODE_MENU||key==android.view.KeyEvent.KEYCODE_F10&&event.isShiftPressed();
        if(event.isCtrlPressed()||event.isAltPressed()||event.isMetaPressed()||event.isShiftPressed()&&!menu)return false;
        boolean move=key==android.view.KeyEvent.KEYCODE_PAGE_DOWN||key==android.view.KeyEvent.KEYCODE_PAGE_UP||key==android.view.KeyEvent.KEYCODE_MOVE_HOME||key==android.view.KeyEvent.KEYCODE_MOVE_END||key==android.view.KeyEvent.KEYCODE_DPAD_LEFT||key==android.view.KeyEvent.KEYCODE_DPAD_RIGHT||key==android.view.KeyEvent.KEYCODE_DPAD_UP||key==android.view.KeyEvent.KEYCODE_DPAD_DOWN;
        boolean activate=key==android.view.KeyEvent.KEYCODE_ENTER||key==android.view.KeyEvent.KEYCODE_NUMPAD_ENTER||key==android.view.KeyEvent.KEYCODE_DPAD_CENTER;
        boolean escape=key==android.view.KeyEvent.KEYCODE_ESCAPE;
        if(!move&&!activate&&!menu&&!escape)return false;
        if(isInteracting())return true;
        if(escape){if(isConnecting()){cancelConnection();return true;}if(!selected.isEmpty()){keyboardNodeId=null;clearSelection();return true;}return false;}
        if(menu)return !readOnly&&!isConnecting()&&!selected.isEmpty()&&listener!=null&&listener.onSelectionMenuRequested();
        if(activate)return event.getRepeatCount()>0||activateKeyboardNode();
        List<String> order=keyboardOrder();if(order.isEmpty())return false;String current=currentKeyboardNode(),target=null;int index=order.indexOf(current);
        if(key==android.view.KeyEvent.KEYCODE_MOVE_HOME)target=order.get(0);
        else if(key==android.view.KeyEvent.KEYCODE_MOVE_END)target=order.get(order.size()-1);
        else if(key==android.view.KeyEvent.KEYCODE_PAGE_DOWN)target=order.get(index<0?0:Math.min(order.size()-1,index+1));
        else if(key==android.view.KeyEvent.KEYCODE_PAGE_UP)target=order.get(index<0?order.size()-1:Math.max(0,index-1));
        else if(current==null)target=order.get(key==android.view.KeyEvent.KEYCODE_DPAD_UP||key==android.view.KeyEvent.KEYCODE_DPAD_LEFT?order.size()-1:0);
        else{
            List<FlowCanvasNavigation.Item> items=new ArrayList<>();for(NodeVisual node:nodes){RectF b=node.bounds;items.add(new FlowCanvasNavigation.Item(node.node.id,b.left,b.top,b.right,b.bottom));}
            int dx=key==android.view.KeyEvent.KEYCODE_DPAD_LEFT?-1:key==android.view.KeyEvent.KEYCODE_DPAD_RIGHT?1:0,dy=key==android.view.KeyEvent.KEYCODE_DPAD_UP?-1:key==android.view.KeyEvent.KEYCODE_DPAD_DOWN?1:0;
            target=FlowCanvasNavigation.direction(items,current,dx,dy);
        }
        return target!=null&&focusKeyboardNode(target);
    }
    @Override public boolean onKeyDown(int key,android.view.KeyEvent event){if(canvasKey(key,event)){keyboardConsumedKeys.add(key);return true;}return super.onKeyDown(key,event);}
    @Override public boolean onKeyUp(int key,android.view.KeyEvent event){if(keyboardConsumedKeys.remove(key))return true;return super.onKeyUp(key,event);}
    @Override protected void onFocusChanged(boolean gain,int direction,Rect previous){super.onFocusChanged(gain,direction,previous);if(!gain)keyboardConsumedKeys.clear();invalidate();}

    private int accessibilityNodeId(String id) { return accessibility.nodeId(id); }
    private int accessibilityEdgeId(String id) { return accessibility.edgeId(id); }

    private final class CanvasAccessibility extends AccessibilityNodeProvider {
        private final FlowVirtualIds identities=new FlowVirtualIds();
        private FlowModel.Document observedDocument;
        private long observedGeneration=-1;
        private String[] nodeSemantics(NodeVisual visual,FlowModel.Node raw){return new String[]{Integer.toString(raw.kind),Integer.toString(visual.kind),Boolean.toString(isFoldCard(visual.node.id))};}
        private String[] edgeSemantics(EdgeVisual visual,FlowModel.Edge raw){return new String[]{raw.from,raw.to,raw.branch,visual.edge.from,visual.edge.to,visual.edge.branch};}
        private void sync() {
            ensureCache();
            if(observedDocument==document&&observedGeneration==semanticGeneration)return;
            Map<String,FlowModel.Node> rawNodes=new HashMap<>();Map<String,FlowModel.Edge> rawEdges=new HashMap<>();
            for(FlowModel.Node node:document.nodes)rawNodes.put(node.id,node);
            for(FlowModel.Edge edge:document.edges)rawEdges.put(edge.id,edge);
            identities.begin(document);
            for(NodeVisual visual:nodes){FlowModel.Node raw=rawNodes.get(visual.node.id);if(raw!=null)identities.bind("n:"+raw.id,raw.id,raw,null,null,nodeSemantics(visual,raw));}
            for(EdgeVisual visual:edges){FlowModel.Edge raw=rawEdges.get(visual.edge.id);if(raw!=null)identities.bind("e:"+raw.id,raw.id,raw,rawNodes.get(raw.from),rawNodes.get(raw.to),edgeSemantics(visual,raw));}
            identities.finish();observedDocument=document;observedGeneration=semanticGeneration;
            if(identities.get(accessibilityFocus)==null)accessibilityFocus=-1;
            if(identities.get(hoverVirtualId)==null)hoverVirtualId=-1;
        }
        private FlowVirtualIds.Entry binding(int id){sync();FlowVirtualIds.Entry entry=identities.get(id);return valid(entry)?entry:null;}
        private boolean valid(FlowVirtualIds.Entry entry){
            if(entry==null||entry.owner!=document)return false;
            if(entry.key.startsWith("n:")){NodeVisual visual=nodeIndex.get(entry.id);FlowModel.Node raw=document.find(entry.id);return visual!=null&&raw!=null&&entry.matches(document,raw,null,null,nodeSemantics(visual,raw));}
            EdgeVisual visual=edgeIndex.get(entry.id);if(visual==null)return false;
            FlowModel.Edge raw=null;for(FlowModel.Edge edge:document.edges)if(entry.id.equals(edge.id)){raw=edge;break;}
            return raw!=null&&entry.matches(document,raw,document.find(raw.from),document.find(raw.to),edgeSemantics(visual,raw));
        }
        private int nodeId(String id){sync();FlowVirtualIds.Entry entry=identities.get("n:"+id);return valid(entry)?entry.virtualId:0;}
        private int edgeId(String id){sync();FlowVirtualIds.Entry entry=identities.get("e:"+id);return valid(entry)?entry.virtualId:0;}
        private boolean actionable(){return isEnabled()&&isShown()&&hasWindowFocus()&&!isInteracting();}
        private void bounds(AccessibilityNodeInfo info,RectF world,boolean edge){
            Rect local=new Rect((int)screenX(world.left),(int)screenY(world.top),(int)screenX(world.right),(int)screenY(world.bottom));
            if(edge)local.inset(-Math.max(0,(int)(dp(24)-local.width())/2),-Math.max(0,(int)(dp(24)-local.height())/2));
            info.setBoundsInParent(local);int[] location=new int[2];getLocationOnScreen(location);local.offset(location[0],location[1]);info.setBoundsInScreen(local);
        }
        @Override public AccessibilityNodeInfo createAccessibilityNodeInfo(int virtualId){
            sync();
            if(virtualId==View.NO_ID){
                AccessibilityNodeInfo info=AccessibilityNodeInfo.obtain(FlowCanvasView.this);FlowCanvasView.this.onInitializeAccessibilityNodeInfo(info);info.setClassName("android.view.View");info.setScrollable(true);
                info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_SCROLL_FORWARD);info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_SCROLL_BACKWARD);
                for(String key:keyboardOrder()){NodeVisual node=nodeIndex.get(key);if(node!=null&&visibleNode(node)){int id=nodeId(key);if(id>0)info.addChild(FlowCanvasView.this,id);}}
                for(EdgeVisual edge:edges)if(visibleEdge(edge)){int id=edgeId(edge.edge.id);if(id>0)info.addChild(FlowCanvasView.this,id);}
                return info;
            }
            FlowVirtualIds.Entry entry=binding(virtualId);if(entry==null)return null;
            AccessibilityNodeInfo info=AccessibilityNodeInfo.obtain();info.setPackageName(getContext().getPackageName());info.setClassName("android.widget.Button");info.setSource(FlowCanvasView.this,virtualId);info.setParent(FlowCanvasView.this);info.setEnabled(isEnabled());info.setFocusable(true);info.setAccessibilityFocused(accessibilityFocus==virtualId);
            if(entry.key.startsWith("e:")){
                EdgeVisual edge=edgeIndex.get(entry.id);info.setContentDescription(edgeDescription(edge));info.setClickable(!readOnly&&!selectionMode&&!isConnecting());info.setVisibleToUser(isShown()&&visibleEdge(edge));bounds(info,edge.bounds,true);
                if(!readOnly&&!selectionMode&&!isConnecting())info.addAction(new AccessibilityNodeInfo.AccessibilityAction(AccessibilityNodeInfo.ACTION_CLICK,"Gestisci collegamento"));
            }else{
                NodeVisual visual=nodeIndex.get(entry.id);FlowModel.Node node=visual.node;
                info.setContentDescription(FlowModel.kindName(node.kind)+": "+nonEmpty(node.text,placeholder(node.kind))+(node.breakpoint?". Punto di interruzione"+(node.breakpointCondition==null||node.breakpointCondition.trim().isEmpty()?" sempre":" se "+node.breakpointCondition):""));
                info.setClickable(!readOnly||navigationPreview);info.setLongClickable(!readOnly&&!isConnecting());info.setSelected(selected.contains(node.id));info.setVisibleToUser(isShown()&&visibleNode(visual));info.setFocused(hasFocus()&&node.id.equals(keyboardNodeId));bounds(info,visual.bounds,false);
                info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_SHOW_ON_SCREEN);
                info.addAction(hasFocus()&&node.id.equals(keyboardNodeId)?AccessibilityNodeInfo.AccessibilityAction.ACTION_CLEAR_FOCUS:AccessibilityNodeInfo.AccessibilityAction.ACTION_FOCUS);
                if(!readOnly||navigationPreview)info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_CLICK);
                if(!readOnly&&!isConnecting()){
                    info.addAction(AccessibilityNodeInfo.AccessibilityAction.ACTION_LONG_CLICK);
                    info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_EDIT,isFoldCard(node.id)?"Espandi ciclo":"Modifica blocco"));
                    if(!isFoldCard(node.id)&&node.kind!=FlowModel.END&&node.kind!=FlowModel.NOTE){
                        info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_ADD,"Aggiungi blocco dopo questo"));info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_CONNECT,"Collega a un blocco esistente"));
                        if(node.kind==FlowModel.DECISION){
                            info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_ADD_TRUE,"Aggiungi nel ramo "+nonEmpty(node.trueLabel,"Sì")));info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_ADD_FALSE,"Aggiungi nel ramo "+nonEmpty(node.falseLabel,"No")));
                            info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_LINK_TRUE,"Collega ramo "+nonEmpty(node.trueLabel,"Sì")));info.addAction(new AccessibilityNodeInfo.AccessibilityAction(ACCESS_LINK_FALSE,"Collega ramo "+nonEmpty(node.falseLabel,"No")));
                        }
                    }
                }
            }
            info.addAction(accessibilityFocus==virtualId?AccessibilityNodeInfo.AccessibilityAction.ACTION_CLEAR_ACCESSIBILITY_FOCUS:AccessibilityNodeInfo.AccessibilityAction.ACTION_ACCESSIBILITY_FOCUS);return info;
        }
        @Override public boolean performAction(int virtualId,int action,Bundle arguments){
            if(Looper.myLooper()!=Looper.getMainLooper())return false;
            if(virtualId==View.NO_ID){
                if(!actionable())return false;
                if(action==AccessibilityNodeInfo.ACTION_SCROLL_FORWARD){zoomBy(1.2f);return true;}if(action==AccessibilityNodeInfo.ACTION_SCROLL_BACKWARD){zoomBy(1/1.2f);return true;}
                return FlowCanvasView.this.performAccessibilityAction(action,arguments);
            }
            FlowVirtualIds.Entry entry=binding(virtualId);if(entry==null)return false;
            if(action==AccessibilityNodeInfo.ACTION_CLEAR_ACCESSIBILITY_FOCUS){if(accessibilityFocus!=virtualId)return false;accessibilityFocus=-1;sendVirtualEvent(virtualId,AccessibilityEvent.TYPE_VIEW_ACCESSIBILITY_FOCUS_CLEARED);invalidate();return true;}
            if(!actionable())return false;
            boolean edge=entry.key.startsWith("e:");
            if(action==AccessibilityNodeInfo.ACTION_ACCESSIBILITY_FOCUS){
                if(accessibilityFocus==virtualId)return false;if(accessibilityFocus>0)sendVirtualEvent(accessibilityFocus,AccessibilityEvent.TYPE_VIEW_ACCESSIBILITY_FOCUS_CLEARED);accessibilityFocus=virtualId;
                sendVirtualEvent(virtualId,AccessibilityEvent.TYPE_VIEW_ACCESSIBILITY_FOCUSED);invalidate();return true;
            }
            if(edge){if(action!=AccessibilityNodeInfo.ACTION_CLICK||readOnly||selectionMode||isConnecting()||listener==null)return false;listener.onEdgeEditRequested(entry.id);if(valid(entry))sendVirtualEvent(virtualId,AccessibilityEvent.TYPE_VIEW_CLICKED);return true;}
            FlowModel.Node node=(FlowModel.Node)entry.model;
            if(action==AccessibilityNodeInfo.AccessibilityAction.ACTION_SHOW_ON_SCREEN.getId()){frameNodeAnimated(entry.id);return true;}
            if(action==AccessibilityNodeInfo.ACTION_FOCUS){requestFocus();return focusKeyboardNode(entry.id);}
            if(action==AccessibilityNodeInfo.ACTION_CLEAR_FOCUS){if(!entry.id.equals(keyboardNodeId))return false;keyboardNodeId=null;clearFocus();invalidate();return true;}
            if(readOnly){return navigationPreview&&action==AccessibilityNodeInfo.ACTION_CLICK&&listener!=null&&listener.onReadOnlyNodeTap(entry.id);}
            if(isConnecting()&&action!=AccessibilityNodeInfo.ACTION_CLICK)return false;
            if(action==ACCESS_EDIT){
                if(isFoldCard(entry.id)){applySelection(foldSelection(Collections.singleton(entry.id)),true);if(valid(entry))toggleSelectedCycle();return true;}
                if(listener==null)return false;listener.onEditRequested(entry.id);return true;
            }
            if(action==AccessibilityNodeInfo.ACTION_CLICK){
                if(isConnecting())requestConnectionTarget(entry.id,node.x,node.y);else applySelection(foldSelection(Collections.singleton(entry.id)),true);
                if(valid(entry))sendVirtualEvent(virtualId,AccessibilityEvent.TYPE_VIEW_CLICKED);return true;
            }
            if(action==AccessibilityNodeInfo.ACTION_LONG_CLICK){toggleSelection(entry.id);return true;}
            if(isFoldCard(entry.id))return false;
            if(action==ACCESS_ADD_TRUE||action==ACCESS_ADD_FALSE||action==ACCESS_LINK_TRUE||action==ACCESS_LINK_FALSE){
                if(selectionMode||node.kind!=FlowModel.DECISION)return false;String branch=action==ACCESS_ADD_TRUE||action==ACCESS_LINK_TRUE?"true":"false";
                if(action==ACCESS_ADD_TRUE||action==ACCESS_ADD_FALSE){if(listener==null)return false;listener.onAddAfterRequested(entry.id,branch);}else beginConnection(entry.id,branch);return true;
            }
            if(node.kind==FlowModel.END||node.kind==FlowModel.NOTE)return false;
            if(action==ACCESS_ADD&&listener!=null){listener.onAddAfterRequested(entry.id);return true;}
            if(action==ACCESS_CONNECT&&listener!=null){listener.onConnectRequested(entry.id);return true;}
            return false;
        }
        @Override public AccessibilityNodeInfo findFocus(int focus){sync();if(focus==AccessibilityNodeInfo.FOCUS_ACCESSIBILITY&&accessibilityFocus>0)return createAccessibilityNodeInfo(accessibilityFocus);if(focus==AccessibilityNodeInfo.FOCUS_INPUT&&hasFocus()&&keyboardNodeId!=null)return createAccessibilityNodeInfo(nodeId(keyboardNodeId));return null;}
        @Override public List<AccessibilityNodeInfo> findAccessibilityNodeInfosByText(String query,int virtualId){
            sync();ArrayList<AccessibilityNodeInfo> result=new ArrayList<>();String text=query==null?"":query.toLowerCase(Locale.ROOT);
            FlowVirtualIds.Entry only=virtualId==View.NO_ID?null:binding(virtualId);if(virtualId!=View.NO_ID&&only==null)return result;
            for(NodeVisual node:nodes)if((only==null||only.key.equals("n:"+node.node.id))&&(FlowModel.kindName(node.kind)+" "+node.node.text).toLowerCase(Locale.ROOT).contains(text)){AccessibilityNodeInfo info=createAccessibilityNodeInfo(nodeId(node.node.id));if(info!=null)result.add(info);}
            for(EdgeVisual edge:edges)if((only==null||only.key.equals("e:"+edge.edge.id))&&edgeDescription(edge).toLowerCase(Locale.ROOT).contains(text)){AccessibilityNodeInfo info=createAccessibilityNodeInfo(edgeId(edge.edge.id));if(info!=null)result.add(info);}
            return result;
        }
        private boolean visibleNode(NodeVisual node){return screenX(node.bounds.right)>=0&&screenX(node.bounds.left)<=getWidth()&&screenY(node.bounds.bottom)>=0&&screenY(node.bounds.top)<=getHeight();}
    }

    @Override protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        if (!observingMotion) {
            try {
                getContext().getContentResolver().registerContentObserver(
                        Settings.Global.getUriFor(Settings.Global.ANIMATOR_DURATION_SCALE), false, motionObserver);
                observingMotion = true;
            } catch (RuntimeException ignored) { /* canAnimate also checks the platform animator flag. */ }
        }
        refreshSystemMotion();
    }

    @Override protected void onWindowVisibilityChanged(int visibility) {
        super.onWindowVisibilityChanged(visibility);
        if (!motionReady) return;
        if (visibility != VISIBLE) stopAllMotion(true);
        else { refreshSystemMotion(); invalidate(); }
    }

    @Override protected void onVisibilityChanged(View changedView, int visibility) {
        super.onVisibilityChanged(changedView, visibility);
        if (!motionReady) return;
        if (!isShown()) stopAllMotion(true);
        else { syncConnectorPulse(); invalidate(); }
    }

    @Override public void onWindowFocusChanged(boolean hasWindowFocus) {
        super.onWindowFocusChanged(hasWindowFocus);
        if (!motionReady) return;
        if (!hasWindowFocus) {keyboardConsumedKeys.clear();stopConnectorPulse();}
        else { syncConnectorPulse(); invalidate(); }
    }

    @Override public void setEnabled(boolean enabled) {
        super.setEnabled(enabled);
        if (!motionReady) return;
        if (!enabled) { resetGesture(true); stopAllMotion(true); getParentDisallow(false); }
        else { syncConnectorPulse(); invalidate(); }
    }

    @Override protected void onDetachedFromWindow() {
        stopFoldGhost();
        resetGesture(true);
        stopAllMotion(true);
        if (observingMotion) {
            getContext().getContentResolver().unregisterContentObserver(motionObserver);
            observingMotion = false;
        }
        super.onDetachedFromWindow();
    }

    private ValueAnimator holdAnimator,liftAnimator,paletteAnimator,landingAnimator;private String landingId;private float landingLift,landingPulse;private boolean landingPeeled;
    private float holdFill,liftFraction,paletteFraction=1;
    private boolean stickerDetached,stickerAmbiguous,stickerDetachNone;private FlowModel.Document insertionDocument;
    private boolean paletteChooseIo;private FlowModel.Node paletteNode;private NodeVisual paletteVisual;private PalettePayload activePalettePayload;
    private static final class PalettePayload {
        final FlowCanvasView owner;final FlowModel.Document expected;final int kind;final String text;final boolean chooseIo;final String token=java.util.UUID.randomUUID().toString();
        PalettePayload(FlowCanvasView owner,int kind,String text){this(owner,kind,text,false);} PalettePayload(FlowCanvasView owner,int kind,String text,boolean chooseIo){this.owner=owner;this.expected=owner.document;this.kind=kind;this.text=text;this.chooseIo=chooseIo;}
    }
    /** A scoped token lets the canvas receive the drag across the palette dialog window. */
    public void wirePalette(View tile,int kind,String text,Runnable dismiss){wirePalette(tile,kind,text,dismiss,false);}
    public void wirePalette(View tile,int kind,String text,Runnable dismiss,boolean chooseIo){
        tile.setLongClickable(true);tile.setOnLongClickListener(v->{
            if(readOnly||document.nodes.size()+otherNodes.getAsInt()>=FlowModel.MAX_NODES)return false;
            PalettePayload payload=new PalettePayload(this,kind,text,chooseIo);
            View.DragShadowBuilder shadow=new View.DragShadowBuilder(this){
                @Override public void onProvideShadowMetrics(android.graphics.Point size,android.graphics.Point touch){size.set(1,1);touch.set(0,0);}
                @Override public void onDrawShadow(Canvas canvas){}
            };
            activePalettePayload=payload;
            ClipData clip=new ClipData(payload.token,new String[]{"application/x-frociart-block"},new ClipData.Item(""));
            int flags=android.os.Build.VERSION.SDK_INT>=35?View.DRAG_FLAG_GLOBAL_SAME_APPLICATION:View.DRAG_FLAG_GLOBAL;
            boolean started=tile.startDragAndDrop(clip,shadow,payload,flags);
            if(started){performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);dismiss.run();}else activePalettePayload=null;return started;
        });
        tile.setTooltipText("Tieni premuto e trascina sul foglio o su una freccia.");
    }
    @Override public boolean onDragEvent(DragEvent event){
        Object state=event.getLocalState();PalettePayload payload=state instanceof PalettePayload?(PalettePayload)state:null;
        if(payload==null&&activePalettePayload!=null&&event.getClipDescription()!=null&&activePalettePayload.token.contentEquals(event.getClipDescription().getLabel()))payload=activePalettePayload;
        if(payload==null||payload.owner!=this)return false;
        int action=event.getAction();
        if(action==DragEvent.ACTION_DRAG_STARTED){if(readOnly||payload.expected!=document)return false;beginPalettePreview(payload);return true;}
        if(action==DragEvent.ACTION_DRAG_LOCATION||action==DragEvent.ACTION_DRAG_ENTERED){if(paletteNode!=null)movePalettePreview(event.getX(),event.getY());return true;}
        if(action==DragEvent.ACTION_DROP){if(readOnly||payload.expected!=document){clearPalettePreview();return false;}movePalettePreview(event.getX(),event.getY());commitPaletteDrop();return true;}
        if(action==DragEvent.ACTION_DRAG_EXITED){insertionHover=null;insertionStrength=0;insertionReady=false;invalidate();return true;}
        if(action==DragEvent.ACTION_DRAG_ENDED){clearPalettePreview();activePalettePayload=null;return true;}return true;
    }
    private void beginPalettePreview(PalettePayload payload){
        resetGesture(true);finishLayoutAnimation();paletteChooseIo=payload.chooseIo;paletteNode=new FlowModel.Node(payload.kind,payload.text==null?defaultPaletteText(payload.kind):payload.text,0,0);
        if(payload.kind==FlowModel.INPUT)paletteNode.prompt="Inserisci un valore";
        paletteVisual=new NodeVisual();paletteVisual.node=paletteNode;paletteVisual.kind=paletteNode.kind;updateNodeText(paletteVisual);
        downNodeId=paletteNode.id;gesture=DRAG;insertionDocument=document.copy();insertionDocument.nodes.add(paletteNode);captureInsertionRoutes();
        paletteFraction=canAnimate()?0:1;if(canAnimate()){paletteAnimator=ValueAnimator.ofFloat(0,1);paletteAnimator.setDuration(340);paletteAnimator.setInterpolator(Ui.SPRING);paletteAnimator.addUpdateListener(v->{paletteFraction=(float)v.getAnimatedValue();invalidate();});paletteAnimator.start();}
        hint("Trascina sulla freccia blu per creare e inserire · rilascia nel foglio per posizionare");
    }
    private void movePalettePreview(float x,float y){if(paletteNode==null)return;paletteNode.x=clamp(worldX(x)-width(paletteNode)/2,-MAX_COORDINATE,MAX_COORDINATE);paletteNode.y=clamp(worldY(y)-height(paletteNode)/2,-MAX_COORDINATE,MAX_COORDINATE);
        paletteVisual.bounds.set(paletteNode.x,paletteNode.y,paletteNode.x+width(paletteNode),paletteNode.y+height(paletteNode));makeNodeShape(paletteVisual.shape,paletteNode.kind,paletteVisual.bounds);tempRect.set(paletteVisual.bounds);tempRect.inset(-5,-5);makeNodeShape(paletteVisual.halo,paletteNode.kind,tempRect);updateInsertionTarget();invalidate();}
    private void clearPalettePreview(){if(paletteAnimator!=null){paletteAnimator.cancel();paletteAnimator=null;}if(paletteNode==null)return;paletteNode=null;paletteVisual=null;insertionDocument=null;clearInsertionPreview();gesture=NONE;downNodeId=null;invalidate();}
    private void drawPaletteGhost(Canvas canvas){if(paletteVisual==null)return;RectF b=paletteVisual.bounds;int save=canvas.save();canvas.scale(.88f+.12f*paletteFraction,.88f+.12f*paletteFraction,b.centerX(),b.centerY());drawNode(canvas,paletteVisual,dark,false);strokePaint.setColor(ACCENT);strokePaint.setStrokeWidth(2/zoom);canvas.drawPath(paletteVisual.shape,strokePaint);canvas.restoreToCount(save);}
    private static String defaultPaletteText(int kind){if(kind==FlowModel.NOTE)return "Scrivi una nota";if(kind==FlowModel.PAUSE)return "Pausa";if(FlowTurtle.isTurtle(kind))return FlowTurtle.defaultText(kind);if(FlowFileBlocks.isFile(kind))return FlowFileBlocks.defaultText(kind);return kind==FlowModel.START?"Inizio":kind==FlowModel.END?"Fine":kind==FlowModel.PROCESS?"x = 1":kind==FlowModel.DECISION?"x > 0":kind==FlowModel.DECLARE?"numero: INTERO":kind==FlowModel.CALL?"nomeFunzione()":"x";}
    private void commitPaletteDrop(){if(paletteNode==null)return;final FlowModel.Node added=paletteNode;final boolean chooseIo=paletteChooseIo;final FlowModel.Document expected=document;final String state=FlowCodec.encode(document);
        final InsertionRoute magnetic=insertionReady?insertionHover:null;final float[] release={added.x,added.y};final Map<String,float[]> placement=FlowPlacement.positions(document);placement.put(added.id,release);centerOnRoute(placement,added.id,magnetic,release);
        final String edgeId=magnetic==null?null:magnetic.id;FlowModel.Edge chosen=null;for(FlowModel.Edge e:document.edges)if(e.id.equals(edgeId)){chosen=e;break;}
        final String source=chosen==null?null:chosen.from,target=chosen==null?null:chosen.to;final boolean forward=chosen!=null&&document.find(target).y>document.find(source).y;
        clearPalettePreview();final InsertionChoice commit=new InsertionChoice(){boolean used;public void choose(String branch){if(used)return;used=true;if(readOnly||document!=expected||!state.equals(FlowCodec.encode(document))||document.nodes.size()+otherNodes.getAsInt()>=FlowModel.MAX_NODES)return;
            if(edgeId!=null&&branch==null)return;boolean splice=edgeId!=null&&!"skip".equals(branch);if(splice&&!"none".equals(branch)&&document.edges.size()+otherEdges.getAsInt()>=FlowModel.MAX_EDGES){hint("Limite di collegamenti raggiunto: nessun blocco aggiunto");return;}
            ArrayList<EdgeSnapshot> previous=captureEdges();if(listener!=null)listener.onManualNodeMove();beforeChange();Map<String,float[]> before=FlowPlacement.positions(document);
            if(magnetic!=null&&splice){float[] at=placement.get(added.id);added.x=at[0];added.y=at[1];}else if(!"skip".equals(branch)&&snapToGrid){added.x=FlowGridSnap.round(added.x);added.y=FlowGridSnap.round(added.y);}document.nodes.add(added);
            if(splice&&!FlowInsertion.apply(document,added.id,edgeId,branch,otherEdges.getAsInt())){document.nodes.remove(added);return;}
            FlowPlacement.reflow(document,added.id,splice?source:null,splice?target:null,splice&&forward);dirty=structureDirty=true;select(added.id);changed("Crea blocco dal menu");
            before.put(added.id,release);startLanding(added.id);animateLayout(before,FlowPlacement.positions(document),420);if(splice)startEdgeMotion(previous,added.id);hint(edgeId==null?"Blocco posizionato · i vicini si adattano · Annulla ripristina tutto":"Blocco creato nella freccia · Annulla ripristina anche il riordino locale");
        }};
        if(chooseIo){if(listener!=null)listener.onIoPaletteRequested(added,kind->{if(kind!=FlowModel.INPUT&&kind!=FlowModel.OUTPUT)return;added.kind=kind;added.prompt=kind==FlowModel.INPUT?"Inserisci un valore":"";commit.choose("next");});}else if(edgeId!=null&&added.kind==FlowModel.DECISION){if(listener!=null)listener.onPaletteInsertionRequested(added,edgeId,commit);}else commit.choose("next");
    }
    private static final long HOLD_GRACE_MS=800,HOLD_FILL_MS=600;
    private final HoldHaptics holdHaptics=new HoldHaptics(this);
    private long holdFillStarted=-1;
    public boolean isInteracting(){return gesture!=NONE||pendingPlacement!=null;}
    private boolean eligibleStationaryHold(){return !readOnly&&gesture==PENDING&&activePointer>=0&&downNodeId!=null&&!isConnecting()&&(!selectionMode||groupSelected())&&Math.hypot(lastX-downX,lastY-downY)<=touchSlop&&isShown()&&hasWindowFocus();}
    private final Runnable beginHoldFill=()->{if(!this.holdArmed||!eligibleStationaryHold())return;holdFillStarted=this.holdArmedAt+HOLD_GRACE_MS;postOnAnimation(this.holdFrame);};
    private final Runnable holdFrame=new Runnable(){public void run(){
        if(holdFillStarted<0||!eligibleStationaryHold()){cancelHoldFill();return;}
        holdFill=Math.max(0,Math.min(1,(android.os.SystemClock.uptimeMillis()-holdFillStarted)/(float)HOLD_FILL_MS));
        holdHaptics.progress(holdFill);invalidate();
        if(holdFill>=1){holdHaptics.confirm();holdFillStarted=-1;longPress.run();}
        else postOnAnimation(this);
    }};
    private boolean holdArmed,holdHintVisible;private long holdArmedAt;
    private final Runnable showHoldHint=()->{if(holdArmed&&eligibleStationaryHold()){holdHintVisible=true;hint("Tieni premuto");invalidate();}};
    private void armHoldFill(){cancelHoldFill();holdArmed=true;holdHintVisible=false;holdArmedAt=android.os.SystemClock.uptimeMillis();postDelayed(showHoldHint,400);postDelayed(beginHoldFill,HOLD_GRACE_MS);invalidate();}
    private void cancelHoldFill(){holdArmed=false;holdHintVisible=false;removeCallbacks(showHoldHint);removeCallbacks(beginHoldFill);removeCallbacks(holdFrame);holdFillStarted=-1;holdHaptics.cancel();if(holdAnimator!=null){holdAnimator.cancel();holdAnimator=null;}holdFill=0;invalidate();}
    private int gestureHintMode(){if(trashReady()&&trashBounds().contains(lastX,lastY))return 4;if(pendingPlacement!=null)return 3;if(gesture==DRAG&&insertionHover!=null&&insertionStrength>=.1f)return 2;return holdArmed&&holdHintVisible&&downNodeId!=null?1:0;}
    private void drawHoldHint(Canvas canvas){
        if(gestureHintMode()!=1)return;NodeVisual node=nodeIndex.get(downNodeId);if(node==null)return;
        String text=liftFraction>0?(stickerAmbiguous?"Pronto · trascina o rilascia":"Pronto · trascina o rilascia"):holdFill>0?"Mantieni premuto · stacco in corso":"Tieni premuto";
        labelPaint.setTextSize(dp(13)*Math.min(1.5f,getResources().getConfiguration().fontScale));
        float width=Math.min(getWidth()-dp(24),labelPaint.measureText(text)+dp(24)),height=dp(48),left=clamp(screenX(node.bounds.centerX())-width/2,dp(12),Math.max(dp(12),getWidth()-dp(12)-width));
        float top=clamp(screenY(node.bounds.top)-height-dp(12),dp(12),Math.max(dp(12),getHeight()-height-dp(12)));
        float reveal=canAnimate()?Math.min(1,(android.os.SystemClock.uptimeMillis()-holdArmedAt-400)/160f):1;int hintLayer=canvas.saveLayerAlpha(0,0,getWidth(),getHeight(),(int)(255*Math.max(0,reveal)));canvas.translate(0,dp(5)*(1-Ui.spring(Math.max(0,reveal))));if(reveal<1)postInvalidateOnAnimation();
        tempRect.set(left,top,left+width,top+height);fillPaint.setColor(dark?DARK_SURFACE:Color.WHITE);canvas.drawRoundRect(tempRect,dp(12),dp(12),fillPaint);strokePaint.setColor(ACCENT);strokePaint.setStrokeWidth(dp(1.2f));canvas.drawRoundRect(tempRect,dp(12),dp(12),strokePaint);
        labelPaint.setColor(dark?0xFF91B7FF:0xFF2F5FBF);text=ellipsize(text,width-dp(18),labelPaint);canvas.drawText(text,left+(width-labelPaint.measureText(text))/2,top+dp(26),labelPaint);
        fillPaint.setColor(dark?0xFF41414B:0xFFD4D8E0);canvas.drawRoundRect(left+dp(12),top+height-dp(9),left+width-dp(12),top+height-dp(6),dp(2),dp(2),fillPaint);
        fillPaint.setColor(ACCENT);canvas.drawRoundRect(left+dp(12),top+height-dp(9),left+dp(12)+(width-dp(24))*Math.min(1,holdFill),top+height-dp(6),dp(2),dp(2),fillPaint);canvas.restoreToCount(hintLayer);
    }
    private void startLift(){showTrash(trashReady());liftFraction=canAnimate()?0:1;if(!canAnimate()){invalidate();return;}liftAnimator=ValueAnimator.ofFloat(0,1);liftAnimator.setDuration(340);liftAnimator.setInterpolator(Ui.SPRING);liftAnimator.addUpdateListener(v->{liftFraction=(float)v.getAnimatedValue();invalidate();});liftAnimator.start();}
    private void clearSticker(){showTrash(false);blockedBypass.clear();bypassExited=false;stickerBypass.clear();cancelHoldFill();if(liftAnimator!=null){liftAnimator.cancel();liftAnimator=null;}liftFraction=0;stickerDetached=stickerAmbiguous=stickerDetachNone=false;insertionDocument=null;}
    private boolean skipStickerEdge(FlowModel.Edge edge){if(pendingPlacement!=null&&pendingPlacement.detached&&(edge.from.equals(pendingPlacement.id)||edge.to.equals(pendingPlacement.id)))return true;return (stickerDetached||stickerAmbiguous)&&liftFraction>0&&(groupDragging()?(selected.contains(edge.from)!=selected.contains(edge.to)):(edge.from.equals(downNodeId)||edge.to.equals(downNodeId)));}
    private void cacheStickerBypass(){
        blockedBypass.clear();bypassExited=false;for(FlowModel.Edge e:document.edges)if(groupDragging()?!selected.contains(e.from)&&selected.contains(e.to):e.to.equals(downNodeId))blockedBypass.add(e.id);
        stickerBypass.clear();if(!stickerDetached)return;
        HashSet<String> incoming=new HashSet<>();for(FlowModel.Edge e:document.edges)if(groupDragging()?!selected.contains(e.from)&&selected.contains(e.to):e.to.equals(downNodeId))incoming.add(e.id);
        for(InsertionRoute route:insertionRoutes)if(incoming.contains(route.id))stickerBypass.add(route);
    }
    private void drawStickerConnections(Canvas canvas){
        if(!stickerDetached||liftFraction<=0)return;
        for(InsertionRoute route:stickerBypass){
            route.prefix.reset();route.measure.getSegment(0,route.measure.getLength()*Math.min(1,liftFraction),route.prefix,true);
            strokePaint.setColor(ACCENT);strokePaint.setStrokeWidth(2/zoom);canvas.drawPath(route.prefix,strokePaint);
            if(liftFraction>=1){fillPaint.setColor(ACCENT);canvas.drawPath(route.arrow,fillPaint);}
        }
    }
    private void stopLanding(){if(landingAnimator!=null){landingAnimator.cancel();landingAnimator=null;}landingId=null;landingGroup.clear();landingLift=landingPulse=0;}
    private void startLanding(String id){startLanding(id,false);}
    private void startLanding(String id,boolean peeled){stopLanding();if(!canAnimate())return;landingId=id;landingPeeled=peeled;landingLift=peeled?1:0;landingAnimator=ValueAnimator.ofFloat(0,1);landingAnimator.setDuration(420);landingAnimator.setInterpolator(new LinearInterpolator());landingAnimator.addUpdateListener(v->{float t=(float)v.getAnimatedValue();landingLift=peeled?1-Ui.spring(t):0;landingPulse=peeled?0:(float)(Math.sin(t*Math.PI*3)*Math.exp(-5*t));invalidate();});landingAnimator.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator a){landingAnimator=null;landingId=null;landingGroup.clear();landingLift=landingPulse=0;invalidate();}});landingAnimator.start();}
    private int beginStickerPaint(Canvas canvas,NodeVisual n,boolean interactive){if(!interactive||!(heldNode(n.node.id)&&holdFill>0||landingNode(n.node.id)||pendingPlacement!=null&&pendingPlacement.detached&&same(n.node.id,pendingPlacement.id)))return -1;int save=canvas.save();RectF b=n.bounds;float amount=landingNode(n.node.id)?landingLift:pendingPlacement!=null&&pendingPlacement.detached&&same(n.node.id,pendingPlacement.id)?1:liftFraction;if(landingNode(n.node.id)&&!landingPeeled)canvas.scale(1+.012f*landingPulse,1-.018f*landingPulse,b.centerX(),b.centerY());if(amount!=0){canvas.translate(0,-10*amount/zoom);canvas.rotate(-3*amount,b.centerX(),b.centerY());canvas.scale(1+.035f*amount,1+.035f*amount,b.centerX(),b.centerY());int shadow=canvas.save();canvas.translate(3,9);fillPaint.setColor(((int)(40*Math.max(0,Math.min(1,amount)))<<24));canvas.drawPath(n.shape,fillPaint);canvas.restoreToCount(shadow);}return save;}
    private void paintHoldFill(Canvas canvas,NodeVisual n,boolean interactive){if(!interactive)return;RectF b=n.bounds;if(heldNode(n.node.id)&&holdFill>0){int save=canvas.save();canvas.clipPath(n.shape);fillPaint.setColor(0x445B8DEF);canvas.drawRect(b.left,b.bottom-b.height()*holdFill,b.right,b.bottom,fillPaint);canvas.restoreToCount(save);}float amount=landingNode(n.node.id)?landingLift:pendingPlacement!=null&&pendingPlacement.detached&&same(n.node.id,pendingPlacement.id)?1:heldNode(n.node.id)?liftFraction:0;if(amount>0){float fold=23*amount;Path corner=new Path();corner.moveTo(b.right-fold,b.top);corner.lineTo(b.right,b.top+fold);corner.lineTo(b.right-fold,b.top+fold);corner.close();fillPaint.setColor(dark?0xFF394B71:0xFFDCE7FF);canvas.drawPath(corner,fillPaint);}}

    private ValueAnimator trashRevealAnimator,trashFlightAnimator,trashHoverAnimator;private boolean trashHovered;private float trashHoverAmount;private float trashReveal,trashFlightFraction;private NodeVisual trashFlight;private float trashFromX,trashFromY;private final RectF trashRect=new RectF();
    private RectF trashBounds(){float size=dp(56),gap=dp(16);trashRect.set(Math.max(gap,getWidth()-size-gap),gap,Math.max(gap,getWidth()-size-gap)+size,gap+size);return trashRect;}
    private boolean trashReady(){FlowModel.Node n=document.find(downNodeId);return !groupDragging()&&!readOnly&&gesture==DRAG&&holdFill>=1&&n!=null&&n.kind!=FlowModel.START&&n.kind!=FlowModel.END;}
    private void showTrash(boolean visible){float target=visible?1:0;if(trashRevealAnimator!=null){trashRevealAnimator.removeAllListeners();trashRevealAnimator.cancel();trashRevealAnimator=null;}if(!canAnimate()){trashReveal=target;invalidate();return;}if(Math.abs(target-trashReveal)<.001f)return;trashRevealAnimator=ValueAnimator.ofFloat(trashReveal,target);trashRevealAnimator.setDuration(240);trashRevealAnimator.setInterpolator(Ui.SPRING);trashRevealAnimator.addUpdateListener(v->{trashReveal=(float)v.getAnimatedValue();invalidate();});trashRevealAnimator.start();}
    private void stopTrashMotion(){if(trashHoverAnimator!=null){trashHoverAnimator.removeAllUpdateListeners();trashHoverAnimator.cancel();trashHoverAnimator=null;}trashHovered=false;trashHoverAmount=0;if(trashRevealAnimator!=null){trashRevealAnimator.removeAllUpdateListeners();trashRevealAnimator.cancel();trashRevealAnimator=null;}if(trashFlightAnimator!=null){trashFlightAnimator.removeAllUpdateListeners();trashFlightAnimator.removeAllListeners();trashFlightAnimator.cancel();trashFlightAnimator=null;}trashFlight=null;trashReveal=0;}
    private void drawTrash(Canvas canvas){RectF box=trashBounds();float r=clamp(trashReveal,0,1.05f);boolean hover=trashReady()&&box.contains(lastX,lastY);if(hover!=trashHovered){trashHovered=hover;if(trashHoverAnimator!=null)trashHoverAnimator.cancel();if(canAnimate()){trashHoverAnimator=ValueAnimator.ofFloat(trashHoverAmount,hover?1:0);trashHoverAnimator.setDuration(220);trashHoverAnimator.setInterpolator(Ui.SPRING);trashHoverAnimator.addUpdateListener(v->{trashHoverAmount=(float)v.getAnimatedValue();invalidate();});trashHoverAnimator.start();}else trashHoverAmount=hover?1:0;}if(r>.01f){int save=canvas.save();canvas.scale(.7f+.3f*r,.7f+.3f*r,box.centerX(),box.centerY());int alpha=(int)(255*Math.min(1,r));fillPaint.setColor(((hover?0xFFD84E68:dark?DARK_SURFACE:Color.WHITE)&0x00FFFFFF)|(alpha<<24));canvas.drawOval(box,fillPaint);strokePaint.setColor((0x00D84E68)|(alpha<<24));strokePaint.setStrokeWidth(dp(hover?2.4f:1.5f));canvas.drawOval(box,strokePaint);strokePaint.setColor(((hover?Color.WHITE:0xFFD84E68)&0x00FFFFFF)|(alpha<<24));strokePaint.setStrokeWidth(dp(2));float x=box.left,y=box.top,u=box.width()/56;scratchPath.reset();scratchPath.moveTo(x+18*u,y+23*u);scratchPath.lineTo(x+20*u,y+41*u);scratchPath.lineTo(x+36*u,y+41*u);scratchPath.lineTo(x+38*u,y+23*u);canvas.drawPath(scratchPath,strokePaint);canvas.drawLine(x+16*u,y+(21-4*trashHoverAmount)*u,x+40*u,y+(21-7*trashHoverAmount)*u,strokePaint);canvas.drawLine(x+24*u,y+(17-4*trashHoverAmount)*u,x+32*u,y+(17-5*trashHoverAmount)*u,strokePaint);canvas.drawLine(x+25*u,y+26*u,x+25*u,y+36*u,strokePaint);canvas.drawLine(x+31*u,y+26*u,x+31*u,y+36*u,strokePaint);canvas.restoreToCount(save);if(hover){labelPaint.setTextSize(dp(13));labelPaint.setColor(0xFFD84E68);String message="Rilascia per eliminare";canvas.drawText(message,Math.max(dp(12),box.right-labelPaint.measureText(message)),box.bottom+dp(22),labelPaint);}}
        if(trashFlight!=null){float t=clamp(trashFlightFraction,0,1),p=Ui.spring(t),size=Math.max(.02f,1-.98f*p),x=trashFromX+(box.centerX()-trashFromX)*p,y=trashFromY+(box.centerY()-trashFromY)*p-dp(18)*(float)Math.sin(Math.PI*t);int save=canvas.save();canvas.translate(x,y);canvas.scale(scale()*size,scale()*size);canvas.translate(-trashFlight.bounds.centerX(),-trashFlight.bounds.centerY());int layer=canvas.saveLayerAlpha(trashFlight.bounds,(int)(255*(1-t*t)));drawNode(canvas,trashFlight,dark,false);canvas.restoreToCount(layer);canvas.restoreToCount(save);}
    }
    private boolean finishTrashDrop(){if(!trashReady()||!trashBounds().contains(lastX,lastY))return false;ensureCache();String id=downNodeId;NodeVisual ghost=nodeIndex.get(id);FlowModel.Node n=document.find(id);float fromX=ghost==null?lastX:screenX(ghost.bounds.centerX()),fromY=ghost==null?lastY:screenY(ghost.bounds.centerY());for(DragOrigin origin:dragOrigins){origin.node.x=origin.x;origin.node.y=origin.y;}if(listener!=null)listener.onManualNodeMove();beforeChange();if(FlowPlacement.continuation(document,id)!=null)FlowPlacement.detach(document,id);else FlowPlacement.disconnect(document,id);document.edges.removeIf(e->e.from.equals(id)||e.to.equals(id));document.nodes.remove(n);selected.remove(id);dragOrigins.clear();dragChanged=false;dirty=structureDirty=true;changed("Elimina blocco nel cestino");if(listener!=null)listener.onSelectionChanged();
        if(canAnimate()&&ghost!=null){trashFlight=ghost;trashFromX=fromX;trashFromY=fromY;trashFlightFraction=0;trashFlightAnimator=ValueAnimator.ofFloat(0,1);trashFlightAnimator.setDuration(320);trashFlightAnimator.setInterpolator(new LinearInterpolator());trashFlightAnimator.addUpdateListener(v->{trashFlightFraction=(float)v.getAnimatedValue();invalidate();});trashFlightAnimator.addListener(new AnimatorListenerAdapter(){@Override public void onAnimationEnd(Animator a){trashFlightAnimator=null;trashFlight=null;invalidate();}});trashFlightAnimator.start();}performHapticFeedback(HapticFeedbackConstants.CONFIRM);hint("Blocco eliminato · Annulla ripristina casella e collegamenti");return true;
    }

}
