package it.frociart.app;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.PriorityQueue;

/**
 * Bounded, deterministic top-down layout of the portable flowchart model.
 * Only node coordinates change. Current coordinates are never layout inputs.
 */
public final class FlowAutoLayout {
    public static final int VERTICAL_GAP = 64;
    public static final int HORIZONTAL_GAP = 88;
    public static final int COMPONENT_GAP = 160;
    public static final int MARGIN = 48;
    private static final int SAFE_SPAN = 196000;
    private FlowAutoLayout() {}

    /**
     * Returns whether at least one coordinate changed. Empty documents are a
     * no-op. Invalid node identities or exceeded model limits are rejected
     * before any mutation. Unresolved edges do not participate in placement;
     * they remain in the document for the normal validation UI to explain.
     *
     * DFS feedback arcs are excluded only from layering. This handles every
     * strongly connected component without moving its original edges: a loop
     * body descends and its return edge points back toward the loop entry.
     * Five fixed ordering sweeps bound work by O(V^2 + E log E), with V <= 500
     * and E <= 1500. There is no iterative convergence or recursive traversal.
     */
    public static boolean apply(FlowModel.Document document) {return apply(document,FlowModel.sizing());}
    public static void applyAnchored(FlowModel.Document target,FlowModel.Document original,FlowModel.Sizing before,FlowModel.Sizing after,float[] focus){
        FlowModel.Node anchor=null;double distance=Double.POSITIVE_INFINITY;
        for(FlowModel.Node n:original.nodes){if(n.kind==FlowModel.NOTE)continue;if(focus==null){if(anchor==null||n.kind==FlowModel.START)anchor=n;if(n.kind==FlowModel.START)break;}else{FlowModel.NodeSize s=FlowModel.size(n,before);double dx=n.x+s.width/2-focus[0],dy=n.y+s.height/2-focus[1],value=dx*dx+dy*dy;if(value<distance){distance=value;anchor=n;}}}
        if(anchor==null)return;FlowModel.NodeSize old=FlowModel.size(anchor,before);float x=anchor.x+old.width/2,y=anchor.y+old.height/2;apply(target,after);FlowModel.Node moved=target.find(anchor.id);FlowModel.NodeSize size=FlowModel.size(moved,after);
        float minX=Float.POSITIVE_INFINITY,minY=minX,maxX=Float.NEGATIVE_INFINITY,maxY=maxX;for(FlowModel.Node n:target.nodes)if(n.kind!=FlowModel.NOTE){minX=Math.min(minX,n.x);minY=Math.min(minY,n.y);maxX=Math.max(maxX,n.x);maxY=Math.max(maxY,n.y);}
        float dx=Math.max(-100000-minX,Math.min(100000-maxX,x-moved.x-size.width/2)),dy=Math.max(-100000-minY,Math.min(100000-maxY,y-moved.y-size.height/2));
        for(FlowModel.Node n:target.nodes)if(n.kind!=FlowModel.NOTE){n.x+=dx;n.y+=dy;}
    }
    public static boolean apply(FlowModel.Document document,FlowModel.Sizing sizing) {
        if (document == null) throw new IllegalArgumentException("Il diagramma non esiste.");
        if (document.nodes.size() > FlowModel.MAX_NODES || document.edges.size() > FlowModel.MAX_EDGES)
            throw new IllegalArgumentException("Il diagramma supera i limiti di 500 blocchi e 1500 collegamenti.");
        document=FlowAnnotations.executable(document);
        if (document.nodes.isEmpty()) return false;
        Graph graph = new Graph(document,sizing);
        graph.buildComponents();
        graph.markFeedbackArcs();
        graph.assignLayers();
        for (Component component : graph.components) graph.layout(component);
        graph.packComponents();
        boolean changed = false;
        for (int i = 0; i < graph.nodes.length; i++) {
            FlowModel.Node node = graph.nodes[i];
            float x = (float) graph.x[i], y = (float) graph.y[i];
            if (Float.compare(node.x, x) != 0 || Float.compare(node.y, y) != 0) changed = true;
        }
        if (changed) for (int i = 0; i < graph.nodes.length; i++) {
            graph.nodes[i].x = (float) graph.x[i]; graph.nodes[i].y = (float) graph.y[i];
        }
        return changed;
    }

    private static final class Arc {
        final int from, to, branch;
        boolean feedback;
        double offset;
        Arc(int from, int to, int branch) { this.from = from; this.to = to; this.branch = branch; }
    }

    private static final class Component {
        final ArrayList<Integer> members = new ArrayList<>();
        final ArrayList<ArrayList<Integer>> rows = new ArrayList<>();
        double width, height;
    }

    private static final class Graph {
        final FlowModel.Sizing sizing;
        float width(FlowModel.Node n){return FlowModel.width(n,sizing);}
        float height(FlowModel.Node n){return FlowModel.height(n,sizing);}
        final FlowModel.Node[] nodes;
        final ArrayList<Arc>[] outgoing, incoming;
        final ArrayList<Component> components = new ArrayList<>();
        final int[] componentOf, order, layer;
        final double[] center, wanted, x, y;
        final Comparator<Integer> semanticOrder;

        @SuppressWarnings("unchecked") Graph(FlowModel.Document document,FlowModel.Sizing sizing) {this.sizing=sizing;
            nodes = document.nodes.toArray(new FlowModel.Node[document.nodes.size()]);
            int count = nodes.length;
            outgoing = new ArrayList[count]; incoming = new ArrayList[count];
            componentOf = new int[count]; order = new int[count]; layer = new int[count];
            center = new double[count]; wanted = new double[count]; x = new double[count]; y = new double[count];
            Arrays.fill(componentOf, -1); Arrays.fill(order, -1);
            HashMap<String,Integer> index = new HashMap<>();
            for (int i = 0; i < count; i++) {
                FlowModel.Node node = nodes[i];
                if (node == null || node.id == null || node.id.isEmpty() || index.put(node.id, i) != null)
                    throw new IllegalArgumentException("Ogni blocco deve avere un identificatore unico.");
                if (!FlowModel.isKnownKind(node.kind))
                    throw new IllegalArgumentException("Il tipo di un blocco non è valido.");
                outgoing[i] = new ArrayList<>(); incoming[i] = new ArrayList<>();
            }
            // Sort before deduplication: true/false edges sharing one target
            // form one placement arc, independently of file edge order.
            ArrayList<Arc> arcs = new ArrayList<>();
            for (FlowModel.Edge edge : document.edges) {
                if (edge == null) continue;
                Integer from = index.get(edge.from), to = index.get(edge.to);
                if (from != null && to != null) arcs.add(new Arc(from, to, branchOrder(edge.branch)));
            }
            Collections.sort(arcs, (a,b) -> {
                int compared = Integer.compare(a.from,b.from);
                if (compared == 0) compared = Integer.compare(a.branch,b.branch);
                return compared == 0 ? Integer.compare(a.to,b.to) : compared;
            });
            HashSet<Long> seen = new HashSet<>();
            for (Arc arc : arcs) {
                long key = ((long) arc.from << 32) | arc.to;
                if (seen.add(key)) { outgoing[arc.from].add(arc); incoming[arc.to].add(arc); }
            }
            semanticOrder = (a,b) -> Integer.compare(order[a],order[b]);
        }

        private int branchOrder(String branch) {
            return "true".equals(branch) ? 0 : "false".equals(branch) ? 2 : 1;
        }

        void buildComponents() {
            // Weak components are packed independently; joining paths with
            // several entry nodes still belong to the same component.
            int[] queue = new int[nodes.length];
            for (int first = 0; first < nodes.length; first++) {
                if (componentOf[first] >= 0) continue;
                Component component = new Component();
                int number = components.size(), begin = 0, end = 0;
                components.add(component); queue[end++] = first; componentOf[first] = number;
                while (begin < end) {
                    int current = queue[begin++]; component.members.add(current);
                    for (Arc arc : outgoing[current]) if (componentOf[arc.to] < 0) {
                        componentOf[arc.to] = number; queue[end++] = arc.to;
                    }
                    for (Arc arc : incoming[current]) if (componentOf[arc.from] < 0) {
                        componentOf[arc.from] = number; queue[end++] = arc.from;
                    }
                }
            }
        }

        void markFeedbackArcs() {
            ArrayList<Integer> roots = new ArrayList<>();
            for (int i = 0; i < nodes.length; i++) roots.add(i);
            Collections.sort(roots, (a,b) -> {
                int pa = nodes[a].kind == FlowModel.START ? 0 : incoming[a].isEmpty() ? 1 : 2;
                int pb = nodes[b].kind == FlowModel.START ? 0 : incoming[b].isEmpty() ? 1 : 2;
                return pa == pb ? Integer.compare(a,b) : Integer.compare(pa,pb);
            });
            byte[] state = new byte[nodes.length];
            int[] stack = new int[nodes.length], cursor = new int[nodes.length];
            int nextOrder = 0;
            for (int root : roots) {
                if (state[root] != 0) continue;
                int depth = 0; stack[depth++] = root; state[root] = 1; order[root] = nextOrder++;
                while (depth > 0) {
                    int current = stack[depth-1];
                    if (cursor[current] >= outgoing[current].size()) { state[current] = 2; depth--; continue; }
                    Arc arc = outgoing[current].get(cursor[current]++);
                    if (state[arc.to] == 1) { arc.feedback = true; continue; }
                    if (state[arc.to] == 0) {
                        state[arc.to] = 1; order[arc.to] = nextOrder++; stack[depth++] = arc.to;
                    }
                }
            }
            // Even a malformed many-way fork has a bounded, ordered lane map.
            for (ArrayList<Arc> arcs : outgoing) {
                int forward = 0; float widest = 0;
                for (Arc arc : arcs) if (!arc.feedback) { forward++; widest = Math.max(widest,width(nodes[arc.to])); }
                int position = 0;
                for (Arc arc : arcs) if (!arc.feedback)
                    arc.offset = (position++ - (forward-1)/2.0) * (widest+HORIZONTAL_GAP);
            }
        }

        void assignLayers() {
            int[] degree = new int[nodes.length];
            PriorityQueue<Integer> ready = new PriorityQueue<>(semanticOrder);
            for (int i = 0; i < nodes.length; i++) {
                for (Arc arc : incoming[i]) if (!arc.feedback) degree[i]++;
                if (degree[i] == 0) ready.add(i);
            }
            int visited = 0;
            while (!ready.isEmpty()) {
                int current = ready.remove(); visited++;
                for (Arc arc : outgoing[current]) if (!arc.feedback) {
                    layer[arc.to] = Math.max(layer[arc.to],layer[current]+1);
                    if (--degree[arc.to] == 0) ready.add(arc.to);
                }
            }
            if (visited != nodes.length) throw new IllegalStateException("Impossibile ordinare i livelli del diagramma.");
        }

        void layout(Component component) {
            Collections.sort(component.members,semanticOrder);
            for (int member : component.members) {
                while (component.rows.size() <= layer[member]) component.rows.add(new ArrayList<>());
                component.rows.get(layer[member]).add(member);
            }
            // Fixed sweeps, rather than convergence on previous coordinates,
            // make layout repeatable and independent of animation progress.
            for (int i = 0; i < 2; i++) { sweep(component,false); sweep(component,true); }
            sweep(component,false);
            double top = 0;
            for (ArrayList<Integer> row : component.rows) {
                float height = 0;
                for (int member : row) height = Math.max(height,height(nodes[member]));
                for (int member : row) {
                    x[member] = center[member] - width(nodes[member])/2;
                    y[member] = top + (height-height(nodes[member]))/2;
                }
                top += height + VERTICAL_GAP;
            }
            normalize(component);
            if (component.width > SAFE_SPAN || component.height > SAFE_SPAN) compactGrid(component);
        }

        void sweep(Component component, boolean up) {
            for (int step = 0; step < component.rows.size(); step++) {
                ArrayList<Integer> row = component.rows.get(up ? component.rows.size()-1-step : step);
                for (int member : row) {
                    double sum = 0; int count = 0;
                    for (Arc arc : up ? outgoing[member] : incoming[member]) if (!arc.feedback) {
                        sum += up ? center[arc.to]-arc.offset : center[arc.from]+arc.offset; count++;
                    }
                    wanted[member] = count == 0 ? center[member] : Math.round(sum/count);
                }
                Collections.sort(row, (a,b) -> {
                    int compared = Double.compare(wanted[a],wanted[b]);
                    return compared == 0 ? semanticOrder.compare(a,b) : compared;
                });
                double extra = 0, previousRight = 0;
                boolean first = true;
                for (int member : row) {
                    double halfWidth = width(nodes[member])/2;
                    center[member] = first ? wanted[member] : Math.max(wanted[member],previousRight+HORIZONTAL_GAP+halfWidth);
                    extra += center[member]-wanted[member]; previousRight = center[member]+halfWidth; first = false;
                }
                if (!row.isEmpty()) {
                    double shift = Math.round(extra/row.size());
                    for (int member : row) center[member] -= shift;
                }
            }
        }

        void normalize(Component component) {
            double left = Double.POSITIVE_INFINITY, top = Double.POSITIVE_INFINITY;
            double right = Double.NEGATIVE_INFINITY, bottom = Double.NEGATIVE_INFINITY;
            for (int member : component.members) {
                left = Math.min(left,x[member]); top = Math.min(top,y[member]);
                right = Math.max(right,x[member]+width(nodes[member]));
                bottom = Math.max(bottom,y[member]+height(nodes[member]));
            }
            for (int member : component.members) { x[member] -= left; y[member] -= top; }
            component.width = right-left; component.height = bottom-top;
        }

        void compactGrid(Component component) {
            // A defensive bound for unusually tangled input: keep all objects
            // separated and serializable within the model's +/-100000 limit.
            ArrayList<Integer> members = new ArrayList<>(component.members);
            Collections.sort(members,(a,b) -> layer[a] == layer[b] ? semanticOrder.compare(a,b) : Integer.compare(layer[a],layer[b]));
            float cellWidth=232,cellHeight=136;for(int member:members){cellWidth=Math.max(cellWidth,width(nodes[member]));cellHeight=Math.max(cellHeight,height(nodes[member]));}
            for (int position = 0; position < members.size(); position++) {
                int member = members.get(position);
                x[member] = (position%25)*(cellWidth+HORIZONTAL_GAP) + (cellWidth-width(nodes[member]))/2;
                y[member] = (position/25)*(cellHeight+VERTICAL_GAP) + (cellHeight-height(nodes[member]))/2;
            }
            normalize(component);
        }

        void packComponents() {
            double area = 0, widest = 0;
            for (Component component : components) {
                area += (component.width+COMPONENT_GAP)*(component.height+COMPONENT_GAP);
                widest = Math.max(widest,component.width);
            }
            double targetWidth = Math.max(widest,Math.sqrt(area*.78));
            double cursorX = 0, cursorY = 0, rowHeight = 0, right = 0, bottom = 0;
            for (Component component : components) {
                if (cursorX > 0 && cursorX+component.width > targetWidth) {
                    cursorY += rowHeight+COMPONENT_GAP; cursorX = 0; rowHeight = 0;
                }
                for (int member : component.members) { x[member] += cursorX; y[member] += cursorY; }
                right = Math.max(right,cursorX+component.width); bottom = Math.max(bottom,cursorY+component.height);
                cursorX += component.width+COMPONENT_GAP; rowHeight = Math.max(rowHeight,component.height);
            }
            if (right > SAFE_SPAN || bottom > SAFE_SPAN) {
                // This bound is independent of graph shape: at most 500 rows,
                // with at most 25 nodes in each component's grid.
                right = 0; bottom = 0;
                for (Component component : components) {
                    compactGrid(component);
                    for (int member : component.members) y[member] += bottom;
                    right = Math.max(right,component.width); bottom += component.height+COMPONENT_GAP;
                }
                bottom -= COMPONENT_GAP;
            }
            double offsetX = right+MARGIN <= 100000 ? MARGIN : -Math.round(right/2);
            double offsetY = bottom+MARGIN <= 100000 ? MARGIN : -Math.round(bottom/2);
            for (int i = 0; i < nodes.length; i++) { x[i] += offsetX; y[i] += offsetY; }
        }
    }
}
