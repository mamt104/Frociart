package it.frociart.app;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.Random;

/** Independent coverage invariants for the pure production PDF planner. */
public final class FlowPdfPagesTests {
    private static int checks, cases;
    private static final double EPS = 0.00001d;
    private interface Action { void run() throws Exception; }

    public static void main(String[] args) throws Exception {
        FlowModel.Document parity = FlowCodec.decode(new String(Files.readAllBytes(Paths.get(args[0])), StandardCharsets.UTF_8));
        double[] bounds = bounds(parity);
        // The real six-node SAF export selects landscape A4; reproduce its width/height calculation.
        double width = bounds[2] - bounds[0], height = bounds[3] - bounds[1];
        int pw = width > height ? 842 : 595, ph = pw == 842 ? 595 : 842;
        double scale = Math.min(1d, (pw - 56d) / width), pageHeight = (ph - 72d) / scale;
        FlowPdfPages.Page[] original = verify("real UI Pari/Dispari, A4", parity, bounds[1], bounds[3], pageHeight, true);
        check(original.length == 2, "real Pari/Dispari keeps two A4 pages");
        for (FlowModel.Node node : parity.nodes) if (node.kind == FlowModel.OUTPUT)
            check(completeCopies(node, original) >= 1, "Output " + node.text + " appears entirely on a page");

        FlowModel.Document translated = parity.copy();
        for (FlowModel.Node node : translated.nodes) node.y -= 90000;
        FlowPdfPages.Page[] negative = verify("same diagram at negative coordinates", translated,
                bounds[1] - 90000, bounds[3] - 90000, pageHeight, true);
        check(original.length == negative.length, "translation preserves page count");
        for (int i = 0; i < original.length; i++) {
            near(original[i].top, negative[i].top + 90000, "translation preserves page top");
            near(original[i].bottom, negative[i].bottom + 90000, "translation preserves page bottom");
        }

        FlowModel.Document touching = document();
        add(touching, FlowModel.START, 8);
        add(touching, FlowModel.OUTPUT, 150 - 92 - 8); // padded bottom exactly 150
        add(touching, FlowModel.END, 150 + 8); // padded top exactly 150
        FlowPdfPages.Page[] exact = verify("nodes exactly at a page boundary", touching, 0, 250, 150, true);
        check(exact.length == 2, "touching-boundary case needs exactly two pages");

        FlowModel.Document crossing = document();
        add(crossing, FlowModel.START, 8);
        add(crossing, FlowModel.OUTPUT, 130); // old fixed split at 180 cuts this block
        add(crossing, FlowModel.END, 270);
        FlowPdfPages.Page[] gap = verify("node crossing the old fixed cut", crossing, 0, 360, 180, true);
        check(gap.length >= 2, "crossing node moves as one full block to next page");

        FlowModel.Document dense = document();
        for (int i = 0; i < 36; i++) add(dense, i % 3 == 0 ? FlowModel.DECISION : FlowModel.PROCESS, i * 27);
        FlowPdfPages.Page[] overlap = verify("dense overlapping group with no vertical gaps", dense, -28, 1100, 240, false);
        boolean overlapping = false;
        for (int i = 1; i < overlap.length; i++) overlapping |= overlap[i].top < overlap[i-1].bottom - EPS;
        check(overlapping, "dense case explicitly uses overlapping pages");

        FlowModel.Document longChain = document();
        for (int i = 0; i < 150; i++) add(longChain, FlowModel.START, 8 + i * 110);
        FlowPdfPages.Page[] chain = verify("150-node chain", longChain, 0, 150 * 110, 770, true);
        check(chain.length <= 24 && chain.length > 15, "long chain stays within the real page limit");

        FlowModel.Document limit = document();
        for (int i = 0; i < 24; i++) add(limit, FlowModel.START, 8 + i * 90);
        FlowPdfPages.Page[] exactly24 = verify("exactly 24 pages", limit, 0, 24 * 90, 90, true);
        check(exactly24.length == 24, "24 pages are accepted");
        add(limit, FlowModel.START, 8 + 24 * 90);
        rejects(() -> FlowPdfPages.plan(limit, 0, 25 * 90, 90, 24), "24 pagine", "25th page produces a clear bounded error");

        FlowModel.Document tooTall = document();
        add(tooTall, FlowModel.DECISION, 0);
        rejects(() -> FlowPdfPages.plan(tooTall, -28, 164, 100, 24), "blocco", "a block taller than one page is refused explicitly");
        rejects(() -> FlowPdfPages.plan(tooTall, 0, 200, 0, 24), "Dimensioni", "zero page height rejected");
        rejects(() -> FlowPdfPages.plan(tooTall, Double.NaN, 200, 400, 24), "Dimensioni", "nonfinite bounds rejected");

        // Stress the coverage contract on dense and separated intervals; no expected cuts duplicate the planner.
        Random random = new Random(1801026L);
        for (int sample = 0; sample < 24; sample++) {
            FlowModel.Document generated = document();
            for (int i = 0; i < 30; i++) add(generated, random.nextInt(6), random.nextInt(1600) - 800);
            double[] b = bounds(generated);
            verify("generated interval coverage " + sample, generated, b[1], b[3], 280, false);
        }
        System.out.println("PDF PLANNER TESTS PASSED: " + checks + " checks across " + cases + " cases");
        System.out.println("Host planner invariants only; Android PDF rendering is verified separately.");
    }

    private static FlowPdfPages.Page[] verify(String label, FlowModel.Document document,
            double top, double bottom, double pageHeight, boolean noCuts) {
        cases++;
        String before = FlowCodec.encode(document);
        FlowPdfPages.Page[] pages = FlowPdfPages.plan(document, top, bottom, pageHeight, 24);
        check(pages.length > 0 && pages.length <= 24, label + ": finite bounded page count");
        check(pages[0].top <= top + EPS, label + ": includes document start");
        check(pages[pages.length-1].bottom >= bottom - EPS, label + ": includes document end");
        for (int i = 0; i < pages.length; i++) {
            FlowPdfPages.Page page = pages[i];
            check(page.bottom > page.top + EPS, label + ": no zero-height page");
            check(page.bottom - page.top <= pageHeight + EPS, label + ": fits A4 height at original scale");
            if (i > 0) {
                check(page.top > pages[i-1].top + EPS, label + ": strictly advances");
                check(page.top <= pages[i-1].bottom + EPS, label + ": no missing strip of the diagram");
            }
            if (noCuts) for (FlowModel.Node node : document.nodes) {
                double a = node.y - FlowPdfPages.NODE_PADDING;
                double b = node.y + FlowModel.height(node.kind) + FlowPdfPages.NODE_PADDING;
                check(!inside(page.top, a, b) && !inside(page.bottom, a, b), label + ": page boundary is outside padded nodes");
            }
        }
        for (FlowModel.Node node : document.nodes)
            check(completeCopies(node, pages) > 0, label + ": every padded block is complete at least once");
        check(before.equals(FlowCodec.encode(document)), label + ": document and coordinates remain byte-for-byte unchanged");
        return pages;
    }
    private static int completeCopies(FlowModel.Node node, FlowPdfPages.Page[] pages) {
        double a = node.y - FlowPdfPages.NODE_PADDING;
        double b = node.y + FlowModel.height(node.kind) + FlowPdfPages.NODE_PADDING;
        int count = 0;
        for (FlowPdfPages.Page page : pages) if (page.top <= a + EPS && page.bottom >= b - EPS) count++;
        return count;
    }
    private static boolean inside(double x, double a, double b) { return x > a + EPS && x < b - EPS; }
    private static FlowModel.Document document() { FlowModel.Document d = new FlowModel.Document(); d.title = "PDF QA"; return d; }
    private static void add(FlowModel.Document d, int kind, float y) {
        FlowModel.Node n = new FlowModel.Node(kind, kind == FlowModel.OUTPUT ? "\"Risultato\"" : "Passo", 0, y);
        n.id = "qa_" + d.nodes.size(); d.nodes.add(n);
    }
    private static double[] bounds(FlowModel.Document d) {
        double left = Double.POSITIVE_INFINITY, top = left, right = Double.NEGATIVE_INFINITY, bottom = right;
        for (FlowModel.Node n : d.nodes) {
            left = Math.min(left, n.x); top = Math.min(top, n.y);
            right = Math.max(right, n.x + FlowModel.width(n.kind)); bottom = Math.max(bottom, n.y + FlowModel.height(n.kind));
        }
        return new double[]{left - 28, top - 28, right + 28, bottom + 28};
    }
    private static void near(double expected, double actual, String message) { check(Math.abs(expected - actual) < EPS, message); }
    private static void rejects(Action action, String messagePart, String label) throws Exception {
        cases++;
        try { action.run(); throw new AssertionError(label + ": no error"); }
        catch (IllegalArgumentException expected) { check(expected.getMessage().contains(messagePart), label); }
    }
    private static void check(boolean ok, String message) {
        checks++;
        if (!ok) throw new AssertionError(message);
    }
}
