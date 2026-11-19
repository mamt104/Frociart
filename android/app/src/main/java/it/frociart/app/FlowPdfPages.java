package it.frociart.app;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

/** Pure pagination in document units. Never moves a node or changes the export scale. */
public final class FlowPdfPages {
    private FlowPdfPages() {}
    public static final double NODE_PADDING = 8d;
    private static final double EPSILON = 0.000001d;

    public static final class Page {
        public final double top, bottom;
        Page(double top, double bottom) { this.top = top; this.bottom = bottom; }
    }
    private static final class Band {
        final double top;
        double bottom;
        Band(double top, double bottom) { this.top = top; this.bottom = bottom; }
    }

    /**
     * Break at the padded top of a group of overlapping nodes when a safe gap
     * exists. A group taller than one page uses overlapping windows: the next
     * window begins at the first node crossing the previous page's bottom.
     * Thus every node is complete on at least one page, including dense layouts.
     */
    public static Page[] plan(FlowModel.Document document, double top, double bottom,
                              double pageHeight, int maxPages) {return plan(document,top,bottom,pageHeight,maxPages,FlowModel.sizing());}
    public static Page[] plan(FlowModel.Document document,double top,double bottom,double pageHeight,int maxPages,FlowModel.Sizing sizing) {
        if (document == null || !finite(top) || !finite(bottom) || !finite(pageHeight)
                || bottom <= top || pageHeight <= 0 || maxPages < 1)
            throw new IllegalArgumentException("Dimensioni del PDF non valide.");
        List<Band> nodes = new ArrayList<>();
        for (FlowModel.Node node : document.nodes) {
            double a = node.y - NODE_PADDING;
            double b = (double) node.y + FlowModel.height(node,sizing) + NODE_PADDING;
            if (!finite(a) || !finite(b))
                throw new IllegalArgumentException("Coordinate del diagramma non valide.");
            if (b - a > pageHeight + EPSILON)
                throw new IllegalArgumentException("Un blocco non entra intero in una pagina A4. Scegli PDF su una pagina unica.");
            nodes.add(new Band(a, b));
            top = Math.min(top, a);
            bottom = Math.max(bottom, b);
        }
        Collections.sort(nodes, Comparator.comparingDouble(b -> b.top));
        List<Band> groups = new ArrayList<>();
        for (Band node : nodes) {
            Band previous = groups.isEmpty() ? null : groups.get(groups.size() - 1);
            if (previous == null || node.top > previous.bottom + EPSILON)
                groups.add(new Band(node.top, node.bottom));
            else previous.bottom = Math.max(previous.bottom, node.bottom);
        }

        List<Page> pages = new ArrayList<>();
        double start = top;
        while (start < bottom - EPSILON) {
            if (pages.size() == maxPages)
                throw new IllegalArgumentException("Il PDF supera " + maxPages + " pagine. Scegli una pagina unica o avvicina i blocchi.");
            double end = Math.min(bottom, start + pageHeight);
            double fullEnd = end;
            if (end < bottom - EPSILON) {
                for (Band group : groups) {
                    if (group.top >= end - EPSILON) break;
                    if (group.bottom > end + EPSILON && group.top > start + EPSILON) {
                        end = group.top;
                        break;
                    }
                }
            }
            if (end <= start + EPSILON)
                throw new IllegalArgumentException("Non riesco a dividere il diagramma senza tagliare un blocco. Scegli PDF su una pagina unica.");
            pages.add(new Page(start, end));
            if (end >= bottom - EPSILON) break;

            double next = end;
            if (Math.abs(end - fullEnd) <= EPSILON) {
                for (Band node : nodes) {
                    if (node.top >= end - EPSILON) break;
                    if (node.bottom > end + EPSILON) next = Math.min(next, node.top);
                }
            }
            if (next <= start + EPSILON)
                throw new IllegalArgumentException("Non riesco a dividere il diagramma senza tagliare un blocco. Scegli PDF su una pagina unica.");
            start = next;
        }
        return pages.toArray(new Page[pages.size()]);
    }
    private static boolean finite(double value) {
        return !Double.isNaN(value) && !Double.isInfinite(value);
    }
}
