package it.frociart.app;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import static it.frociart.app.FlowModel.*;

/**
 * Host-side checks for the authored curriculum and learning-state contract.
 * Dataset checks do not claim to prove the pedagogical truth of prose.
 * Executable cases read question cards where possible, translate their stated
 * block/control structure, and run the production interpreter, without Android.
 * Source URLs are matched against a separately reviewed retrieval manifest;
 * this offline test does not pretend to perform a live HTTP availability check.
 */
public final class LearningTests {
    private static int checks;
    private static final Set<String> executedQuestions = new LinkedHashSet<>();
    private static void check(boolean ok, String message) {
        checks++;
        if (!ok) throw new AssertionError(message);
    }
    private static void eq(Object expected, Object actual, String message) {
        check(expected == null ? actual == null : expected.equals(actual),
                message + " | expected=" + expected + " actual=" + actual);
    }
    private static void nonempty(String text, String field) {
        check(text != null && text.trim().length() > 0, field + " is not empty");
        check(text.indexOf('\ufffd') < 0, field + " has no replacement character");
    }
    private static FlowLearning.Question question(String id) {
        for (FlowLearning.Lesson lesson : FlowLearning.lessons())
            for (FlowLearning.Question question : lesson.questions)
                if (question.id.equals(id)) return question;
        throw new AssertionError("Unknown question " + id);
    }
    private static void answerIs(String id, String value) {
        FlowLearning.Question q = question(id);
        eq(value, q.options[q.correct], "Answer agrees with evaluated case " + id);
        executedQuestions.add(id);
    }

    private static void dataset(Path root) throws Exception {
        FlowLearning.Unit[] units = FlowLearning.units();
        FlowLearning.Lesson[] lessons = FlowLearning.lessons();
        FlowLearning.Source[] sources = FlowLearning.sources();
        eq(6, units.length, "Six progressive units");
        eq(24, lessons.length, "Twenty-four micro-lessons");
        eq(23, sources.length, "Reviewed primary-source catalog");
        Set<String> unitIds = new HashSet<>(), lessonIds = new HashSet<>(), questionIds = new HashSet<>();
        Set<String> sourceIds = new HashSet<>(), urls = new HashSet<>(), usedSources = new HashSet<>();
        Set<String> exampleNames = new HashSet<>(), prompts = new HashSet<>();
        for (Document d : FlowExamples.all()) exampleNames.add(d.title);
        String[] expectedExamples = { "Somma di due numeri", "Pari o dispari", "Conto alla rovescia",
                "Maggiore tra due numeri", "Esercizio: trova l'errore", "Report di misure" };
        eq(9, FlowExamples.all().size(), "Nine examples including turtle graphics, file copying, executable age example and separate repair exercise");
        for (int i = 0; i < expectedExamples.length; i++)
            eq(expectedExamples[i], FlowExamples.all().get(i).title, "Original five examples keep their order; capstone is appended");
        int questions = 0, feedbacks = 0;
        int[] positions = new int[3];
        for (int i = 0; i < units.length; i++) {
            FlowLearning.Unit u = units[i];
            eq(String.format(Locale.ROOT, "u%02d", i + 1), u.id, "Stable ordered unit ID");
            check(unitIds.add(u.id), "Unit ID unique");
            nonempty(u.title, "Unit title"); nonempty(u.subtitle, "Unit subtitle");
            eq(4, FlowLearning.lessonsFor(u.id).length, "Four lessons per unit " + u.id);
        }
        List<String> manifest = Files.readAllLines(root.resolve("sources-reviewed.tsv"), StandardCharsets.UTF_8);
        for (FlowLearning.Source s : sources) {
            check(sourceIds.add(s.id), "Source ID unique " + s.id);
            check(urls.add(s.url), "Canonical URL unique " + s.id);
            nonempty(s.title, "Source title");
            URI uri = new URI(s.url);
            eq("https", uri.getScheme(), "HTTPS source " + s.id);
            check(uri.getRawUserInfo() == null && uri.getQuery() == null, "Source has no credential/query " + s.id);
            check(Arrays.asList("flowgorithm.org", "www.flowgorithm.org", "cs50.harvard.edu", "web.mit.edu",
                    "ocw.mit.edu", "www.cs.cornell.edu").contains(uri.getHost()), "Primary institutional host " + s.id);
            check(FlowLearning.source(s.id) == s, "Source lookup stable " + s.id);
            boolean found = false;
            for (String line : manifest) if (line.startsWith(s.id + "\t" + s.url + "\t")) found = true;
            check(found, "URL matches a retrieved, reviewed primary source " + s.id);
        }
        int maxOption = 0, maxPrompt = 0, maxCode = 0, maxExplanation = 0;
        for (int i = 0; i < lessons.length; i++) {
            FlowLearning.Lesson l = lessons[i];
            eq(String.format(Locale.ROOT, "l%02d", i + 1), l.id, "Stable ordered lesson ID");
            eq(units[i / 4].id, l.unitId, "Progressive unit membership " + l.id);
            check(lessonIds.add(l.id), "Lesson ID unique " + l.id);
            check(unitIds.contains(l.unitId), "Existing lesson unit " + l.id);
            check(FlowLearning.lesson(l.id) == l, "Lesson lookup stable " + l.id);
            nonempty(l.title, "Lesson title " + l.id); nonempty(l.goal, "Lesson goal " + l.id);
            nonempty(l.explanation, "Lesson explanation " + l.id); nonempty(l.tip, "Documented tip " + l.id);
            nonempty(l.counterexample, "Counterexample " + l.id);
            check(l.sources.length > 0, "Every lesson cites a source " + l.id);
            check(l.practiceExample != null && (l.practiceExample.isEmpty() || exampleNames.contains(l.practiceExample)),
                    "Practice resolves to an existing exact example title " + l.id);
            Set<String> localSources = new HashSet<>();
            for (FlowLearning.Source s : l.sources) {
                check(sourceIds.contains(s.id) && FlowLearning.source(s.id).url.equals(s.url), "Known source reference " + l.id);
                check(localSources.add(s.id), "No duplicate source in lesson " + l.id);
                usedSources.add(s.id);
            }
            eq(3, l.questions.length, "Three guided questions " + l.id);
            maxExplanation = Math.max(maxExplanation, l.explanation.length());
            for (int j = 0; j < l.questions.length; j++) {
                FlowLearning.Question q = l.questions[j];
                eq(l.id + "-q" + (j + 1), q.id, "Stable question ID");
                check(questionIds.add(q.id), "Question ID unique " + q.id);
                nonempty(q.prompt, "Question prompt " + q.id);
                check(q.code != null, "Optional card is empty string, not null " + q.id);
                check(prompts.add(q.prompt + "\n" + q.code), "Question is not an exact repeated card " + q.id);
                eq(3, q.options.length, "Three choices " + q.id);
                eq(q.options.length, q.feedback.length, "Feedback for every option " + q.id);
                check(q.correct >= 0 && q.correct < q.options.length, "Valid answer index " + q.id);
                positions[q.correct]++;
                Set<String> choices = new HashSet<>(), explanations = new HashSet<>();
                for (int k = 0; k < q.options.length; k++) {
                    nonempty(q.options[k], "Option " + q.id + "/" + k);
                    nonempty(q.feedback[k], "Specific feedback " + q.id + "/" + k);
                    check(choices.add(q.options[k].trim().toLowerCase(Locale.ROOT)), "Distinct options " + q.id);
                    check(explanations.add(q.feedback[k]), "Distinct explanatory feedback " + q.id);
                    check(q.feedback[k].length() >= 20, "Feedback explains more than correct/incorrect " + q.id);
                    maxOption = Math.max(maxOption, q.options[k].length());
                    feedbacks++;
                }
                maxPrompt = Math.max(maxPrompt, q.prompt.length());
                maxCode = Math.max(maxCode, q.code.length());
                questions++;
            }
        }
        eq(72, questions, "Question total"); eq(216, feedbacks, "Feedback total");
        eq(sourceIds, usedSources, "All source entries actually support a lesson");
        for (int n : positions) check(n >= 15 && n <= 35, "No fixed correct-position pattern");
        eq(null, FlowLearning.lesson("missing"), "Unknown lesson lookup");
        eq(null, FlowLearning.lesson(null), "Null lesson lookup");
        eq(0, FlowLearning.lessonsFor("missing").length, "Unknown unit lookup");
        FlowLearning.Unit firstUnit = units[0]; units[0] = null;
        check(FlowLearning.units()[0] == firstUnit, "Unit list cannot be replaced through returned array");
        FlowLearning.Lesson firstLesson = lessons[0]; lessons[0] = null;
        check(FlowLearning.lessons()[0] == firstLesson, "Lesson list cannot be replaced through returned array");
        check(FlowLearning.lesson("l21").explanation.contains("non esegue"), "User functions clearly out of engine scope");
        check(FlowLearning.lesson("l22").explanation.contains("non sono array eseguibili"), "Study arrays clearly out of engine scope");
        for (FlowLearning.Question q : FlowLearning.lesson("l22").questions)
            if (!q.code.isEmpty()) check(q.code.startsWith("Pseudocodice"), "List question labeled as pseudocode");
        System.out.println("Dataset: 6 units, 24 lessons, 72 questions, 216 distinct option feedbacks, 23 reviewed source URLs.");
        System.out.println("Answer positions: " + Arrays.toString(positions));
        System.out.println("Text maxima (characters): option=" + maxOption + ", prompt=" + maxPrompt + ", code=" + maxCode
                + ", explanation=" + maxExplanation + ". Native wrapping/IME is verified separately.");
    }

    private static Node node(Document d, String id, int kind, String text) {
        Node node = new Node(kind, text, 300, 60 + d.nodes.size() * 135);
        node.id = id; d.nodes.add(node); return node;
    }
    private static void edge(Document d, String a, String b, String branch) {
        Edge e = new Edge(a, b, branch); e.id = a + "_" + branch + "_" + b; d.edges.add(e);
    }
    private static void edge(Document d, String a, String b) { edge(d, a, b, "next"); }
    private static FlowEngine.Runner run(Document d, String... inputs) {
        FlowEngine.Runner runner = new FlowEngine.Runner(d);
        int input = 0, guard = 0;
        while (runner.state != FlowEngine.State.COMPLETED && runner.state != FlowEngine.State.ERROR && guard++ < 10020) {
            if (runner.state == FlowEngine.State.WAITING) {
                check(input < inputs.length, "Native case supplies every requested input");
                check(runner.submitInput(inputs[input++]), "Native case input accepted");
            } else runner.step();
        }
        eq(FlowEngine.State.COMPLETED, runner.state, "Native case completes: " + d.title + " " + runner.errorMessage);
        eq(inputs.length, input, "Native case consumes precisely the supplied input");
        return runner;
    }
    private static Document sequence(String id, int operationsLimit) {
        Document d = new Document(); d.title = id;
        node(d, "start", START, "Inizio"); String prev = "start"; int operations = 0, nodes = 0;
        for (String line : question(id).code.split("\n")) {
            int kind;
            if (line.startsWith("Operazione")) {
                if (operations++ >= operationsLimit) break;
                kind = PROCESS;
            } else if (line.startsWith("Output:")) kind = OUTPUT;
            else continue;
            String text = line.substring(line.indexOf(':') + 1).trim(), next = "n" + nodes++;
            node(d, next, kind, text); edge(d, prev, next); prev = next;
        }
        node(d, "end", END, "Fine"); edge(d, prev, "end"); return d;
    }
    private static Document process(String statements, String output) {
        Document d = new Document(); d.title = "Lesson expression";
        node(d, "start", START, "Inizio"); node(d, "process", PROCESS, statements);
        node(d, "output", OUTPUT, output); node(d, "end", END, "Fine");
        edge(d, "start", "process"); edge(d, "process", "output"); edge(d, "output", "end"); return d;
    }
    private static String lastLine(String id) {
        String code = question(id).code;
        return code.substring(code.lastIndexOf('\n') + 1);
    }
    private static void expressionQuestion(String id, String result, String answer) {
        eq(result, FlowEngine.evaluate(lastLine(id), null).format(), "Actual question expression " + id);
        answerIs(id, answer);
    }
    private static String outputList(FlowEngine.Runner r, String separator) {
        StringBuilder value = new StringBuilder();
        for (String item : r.output) { if (value.length() > 0) value.append(separator); value.append(item); }
        return value.toString();
    }
    private static void predicateOptions(String id, String variable, double[] inputs, boolean[] expected) {
        FlowLearning.Question q = question(id);
        for (int option = 0; option < q.options.length; option++) {
            boolean mismatch = false;
            for (int i = 0; i < inputs.length; i++) {
                String statement = variable + " = " + inputs[i];
                boolean actual = run(process(statement, q.options[option])).output.get(0).equals("vero");
                if (actual != expected[i]) mismatch = true;
            }
            eq(option != q.correct, mismatch, "Only designated predicate satisfies all specified boundary cases " + id + "/" + option);
        }
        executedQuestions.add(id);
    }
    /** Translate only the explicitly displayed while cards; do not implement a second expression interpreter. */
    private static Document loop(String id, String extraInitialization) {
        Document d = new Document(); d.title = id; node(d, "start", START, "Inizio");
        StringBuilder init = new StringBuilder(extraInitialization);
        ArrayList<String> body = new ArrayList<>(), after = new ArrayList<>();
        String guard = null;
        for (String raw : question(id).code.split("\n")) {
            String line = raw.trim();
            if (line.startsWith("Pseudocodice")) continue;
            if (line.startsWith("Mentre ")) guard = line.substring(7, line.length() - 1);
            else if (guard == null) { if (init.length() > 0) init.append(';'); init.append(line); }
            else if (raw.startsWith("  ")) body.add(line);
            else after.add(line);
        }
        check(guard != null && init.length() > 0 && !body.isEmpty(), "Complete while card " + id);
        node(d, "init", PROCESS, init.toString()); node(d, "guard", DECISION, guard);
        edge(d, "start", "init"); edge(d, "init", "guard");
        String prev = null;
        for (int i = 0; i < body.size(); i++) {
            String line = body.get(i), next = "body" + i; boolean output = line.toLowerCase(Locale.ROOT).startsWith("mostra ");
            node(d, next, output ? OUTPUT : PROCESS, output ? line.substring(7) : line);
            if (prev == null) edge(d, "guard", next, "true"); else edge(d, prev, next);
            prev = next;
        }
        edge(d, prev, "guard"); prev = "guard";
        for (int i = 0; i < after.size(); i++) {
            String line = after.get(i), next = "after" + i;
            check(line.startsWith("Mostra "), "Supported post-loop card statement " + id);
            node(d, next, OUTPUT, line.substring(7));
            edge(d, prev, next, i == 0 ? "false" : "next"); prev = next;
        }
        node(d, "end", END, "Fine"); edge(d, prev, "end", prev.equals("guard") ? "false" : "next");
        return d;
    }

    private static Document report() {
        return FlowExamples.report();
    }

    private static void executableContent(Path root) throws Exception {
        answerIs("l03-q1", run(sequence("l03-q1", 99)).output.get(0));
        answerIs("l03-q2", run(sequence("l03-q2", 99)).output.get(0));
        FlowEngine.Runner state = run(sequence("l04-q1", 2));
        answerIs("l04-q1", "a = " + state.variables.get("a").format() + ", b = " + state.variables.get("b").format());
        answerIs("l04-q2", outputList(run(sequence("l04-q2", 99)), ", poi "));
        answerIs("l05-q1", run(sequence("l05-q1", 99)).output.get(0));
        eq("falso", run(process("x = 4", "x == 7")).output.get(0), "Comparison does not assign");
        answerIs("l05-q2", "Risulta falsa e lascia x a 4");
        FlowLearning.Question swap = question("l05-q3");
        for (int i = 0; i < swap.options.length; i++) {
            String result = run(process("a = 3; b = 8; " + swap.options[i], "a + \",\" + b")).output.get(0);
            eq(i == swap.correct, result.equals("8,3"), "Only correct swap preserves both input values");
        }
        executedQuestions.add("l05-q3");
        boolean uninitialized = false;
        for (FlowEngine.Issue issue : FlowEngine.validate(process("contatore = contatore + 1", "contatore")))
            if (issue.code.equals("uninitialized")) uninitialized = true;
        check(uninitialized, "Question uninitialized read is rejected by production validation");
        answerIs("l06-q1", "Un valore iniziale per contatore");
        expressionQuestion("l07-q1", "14", "14");
        expressionQuestion("l07-q2", "2", "2");
        expressionQuestion("l07-q3", "3.5", "3.5");
        expressionQuestion("l08-q1", "62", "62");
        expressionQuestion("l08-q2", "falso", "Falso, perché i tipi sono diversi");
        FlowLearning.Question tolerance = question("l08-q3");
        eq("vero", FlowEngine.evaluate(tolerance.options[tolerance.correct], null).format(), "Specified absolute tolerance");
        eq("falso", FlowEngine.evaluate(tolerance.options[1], null).format(), "Exact floating equality counterexample");
        eq("0.3", FlowEngine.evaluate("0.1 + 0.2", null).format(), "Displayed rounding differs from exact equality");
        executedQuestions.add(tolerance.id);
        predicateOptions("l09-q1", "punti", new double[]{11, 12, 13}, new boolean[]{false, true, true});
        eq("vero", run(process("a = 8; b = 8", lastLine("l09-q2"))).output.get(0), "Equal maximum decision");
        answerIs("l09-q2", "Il ramo vero");
        Document custom = FlowExamples.parity(); custom.find("check").trueLabel = "Valido"; custom.find("check").trueColor = "#123ABC";
        eq("Pari", run(custom, "4").output.get(0), "Renaming/recoloring true branch preserves logical selection");
        answerIs("l09-q3", "Quando l'espressione della Decisione è vera");
        predicateOptions("l10-q1", "x", new double[]{-10, 0, 1, 3, 5, 6, 100}, new boolean[]{false, false, true, true, true, false, false});
        predicateOptions("l10-q2", "x", new double[]{-10, 0, 1, 3, 5, 6, 100}, new boolean[]{true, true, false, false, false, true, true});
        eq("vero", run(process("permesso = vero; pronto = falso", lastLine("l10-q3"))).output.get(0), "Boolean negation card");
        answerIs("l10-q3", "Vero");
        predicateOptions("l12-q2", "x", new double[]{-4, 0, 9}, new boolean[]{false, true, true});
        eq("falso", run(process("totale = 12; numero = 0", lastLine("l12-q3"))).output.get(0), "Short circuit protects zero division");
        answerIs("l12-q3", "Risulta falsa senza eseguire la divisione");
        eq(0, run(loop("l13-q1", "")).output.size(), "While executes zero times on false initial guard");
        answerIs("l13-q1", "Zero");
        answerIs("l13-q2", outputList(run(loop("l13-q2", "")), ", "));
        answerIs("l14-q2", outputList(run(loop("l14-q2", "")), ", "));
        answerIs("l14-q3", run(loop("l14-q3", "")).output.get(0));
        FlowLearning.Question counts = question("l14-q1");
        for (int j = 0; j < counts.options.length; j++) {
            int count = 0;
            for (int i = 0; i < 12; i++) {
                if (run(process("i = " + i, counts.options[j])).output.get(0).equals("falso")) break;
                count++;
            }
            eq(j == counts.correct, count == 5, "Counting predicate has exactly five visits " + j);
        }
        executedQuestions.add(counts.id);
        answerIs("l15-q1", run(process("somma = 0; somma = somma + 4; somma = somma + 1; somma = somma + 3", "somma")).output.get(0));
        eq("6", run(process("prodotto = 1; prodotto = prodotto * 3; prodotto = prodotto * 2", "prodotto")).output.get(0), "Multiplication neutral initial value");
        eq("0", run(process("prodotto = 0; prodotto = prodotto * 3; prodotto = prodotto * 2", "prodotto")).output.get(0), "Zero-product bug counterexample");
        answerIs("l15-q3", "Inizializza prodotto a 1");
        FlowEngine.Runner proof = new FlowEngine.Runner(loop("l19-q1", "")); int guardVisits = 0;
        while (proof.state != FlowEngine.State.COMPLETED && proof.state != FlowEngine.State.ERROR && proof.steps < 30) {
            if ("guard".equals(proof.currentNodeId)) {
                eq("vero", FlowEngine.evaluate(question("l19-q1").options[question("l19-q1").correct], proof.variables).format(), "Invariant holds at every guard visit");
                guardVisits++;
            }
            proof.step();
        }
        eq(FlowEngine.State.COMPLETED, proof.state, "Invariant example finishes");
        eq(4, guardVisits, "Invariant checked before all three iterations and at exit");
        eq("3", proof.variables.get("i").format(), "Invariant final index");
        eq("6", proof.variables.get("totale").format(), "Invariant final total");
        executedQuestions.add("l19-q1");
        eq("falso", run(process("i = 1; totale = 2; totale = totale + 2", "totale == 2 * i")).output.get(0), "Invariant may be false mid-body");
        eq("vero", run(process("i = 1; totale = 2; totale = totale + 2; i = i + 1", "totale == 2 * i")).output.get(0), "Invariant restored before next guard");
        answerIs("l19-q2", "No, purché sia ristabilita prima del prossimo controllo");
        answerIs("l19-q3", "Il totale finale è 6");
        FlowEngine.Runner variant = run(loop("l20-q1", ""));
        eq("6", variant.variables.get("i").format(), "Variant reaches zero after six iterations");
        answerIs("l20-q1", "6 - i");
        FlowLearning.Question crossing = question("l20-q3");
        for (int option = 0; option < crossing.options.length; option++) {
            ArrayList<Integer> visited = new ArrayList<>();
            for (int i = 0; i <= 12; i += 2) {
                if (run(process("i = " + i, crossing.options[option])).output.get(0).equals("falso")) break;
                visited.add(i);
            }
            eq(option == crossing.correct, visited.equals(Arrays.asList(0, 2, 4)), "Termination guard matches exact desired sequence");
        }
        executedQuestions.add(crossing.id);

        // Academic list questions are calculated on the host, not advertised as engine array support.
        Matcher list = Pattern.compile("\\[(.*?)\\]").matcher(question("l22-q1").code);
        check(list.find(), "Academic list is present"); String[] values = list.group(1).split(",\\s*");
        int comparisons = 0;
        for (String value : values) { comparisons++; if (Integer.parseInt(value) == 14) break; }
        answerIs("l22-q1", Integer.toString(comparisons));
        eq(21, Integer.parseInt(values[values.length / 2]), "Binary first middle is the stated value");
        String remainder = Arrays.toString(Arrays.copyOfRange(values, values.length / 2 + 1, values.length));
        answerIs("l22-q2", remainder);
        answerIs("l23-q1", Integer.toString(20 + 20));
        Matcher dimensions = Pattern.compile("\\d+").matcher(question("l23-q2").code);
        check(dimensions.find(), "Grid row dimension"); int rows = Integer.parseInt(dimensions.group());
        check(dimensions.find(), "Grid column dimension"); int columns = Integer.parseInt(dimensions.group());
        int marks = 0; for (int row = 0; row < rows; row++) for (int col = 0; col < columns; col++) marks++;
        answerIs("l23-q2", Integer.toString(marks));
        eq(4, (8 * 8) / (4 * 4), "Exact quadratic operation-count ratio");
        answerIs("l23-q3", "Quadruplica");

        Document report = report();
        eq("[Media: -5.5, Massimo: -3]", run(report, "-8", "-3", "9999").output.toString(), "Capstone all-negative case");
        answerIs("l24-q1", "Usa il primo dato valido come massimo, poi confronta i successivi");
        eq("[Nessun dato]", run(report, "9999").output.toString(), "Capstone zero-data path avoids undefined mean/maximum");
        answerIs("l24-q2", "Mostrare Nessun dato senza calcolare la media");
        eq("[Valore rifiutato, Media: 1, Massimo: 6]", run(report, "105", "-4", "6", "9999").output.toString(), "Capstone rejects out-of-domain data before accumulation");
        answerIs("l24-q3", "Media 1, massimo 6");
        eq("[Media: 0, Massimo: 100]", run(report, "-100", "100", "9999").output.toString(), "Capstone inclusive endpoints");
        eq("[Media: 0, Massimo: 0]", run(report, "0", "9999").output.toString(), "Capstone one valid zero");
        Path results = root.resolve("results"); Files.createDirectories(results);
        byte[] reportBytes = FlowCodec.encode(report).getBytes(StandardCharsets.UTF_8);
        Files.write(results.resolve("report-di-misure.frociart"), reportBytes);
        Path example = root.resolve("../../examples/06_Report_di_misure.frociart").normalize();
        Files.write(example, reportBytes);
        Document reopened = FlowCodec.decode(new String(Files.readAllBytes(example), StandardCharsets.UTF_8));
        eq("Report di misure", reopened.title, "Delivered capstone file reopens under its practice title");
        eq("[Media: -5.5, Massimo: -3]", run(reopened, "-8", "-3", "9999").output.toString(), "Delivered capstone preserves runtime behavior");
        Files.write(results.resolve("executed-question-ids.txt"), String.join("\n", executedQuestions).getBytes(StandardCharsets.UTF_8));
        System.out.println("Evaluated lesson cases: " + executedQuestions.size() + " question IDs (37 through the production interpreter, 5 explicitly academic host calculations).");
        System.out.println("Capstone fixture: empty, one zero, only negatives, inclusive boundaries, rejected value; all pass the production interpreter.");
    }

    private static String snapshot(FlowLearningProgress p) {
        return p.completed() + "|" + p.activeId + "|" + p.question + "|" + p.answer + "|" + p.firstTry + "|" + p.mistake;
    }
    private static Set<String> saved(String... ids) { return new LinkedHashSet<>(Arrays.asList(ids)); }
    private static void progress() {
        FlowLearningProgress p = new FlowLearningProgress();
        eq(0, p.xp(), "New history has no XP"); eq("l01", p.recommended().id, "Start from fundamentals");
        String empty = snapshot(p); p.begin("unknown", true); p.start();
        check(!p.choose(0), "Cannot answer without a known active lesson"); check(!p.advance(), "Cannot advance without an answer");
        eq(empty, snapshot(p), "Invalid actions leave initial state unchanged");
        p.begin("l01", false); eq(-1, p.question, "Lesson begins with explanation");
        check(!p.choose(0), "Cannot answer before starting questions"); p.start(); p.start(); eq(0, p.question, "Repeated start is idempotent");
        String pristine = snapshot(p);
        check(!p.choose(-1), "Reject negative option"); check(!p.choose(3), "Reject out-of-range option");
        p.begin("unknown", true); eq(pristine, snapshot(p), "Invalid option/ID have no state effects");
        int correct = p.active().questions[0].correct, wrong = (correct + 1) % 3;
        check(p.choose(wrong), "First wrong attempt records feedback"); check(p.mistake, "Wrong attempt tracked");
        String wrongSnapshot = snapshot(p);
        check(!p.advance(), "Wrong answer cannot advance"); check(!p.choose(correct), "Repeated taps cannot overwrite selected answer");
        eq(wrongSnapshot, snapshot(p), "Rejected advancement/tap does not change state");
        FlowLearningProgress reloaded = new FlowLearningProgress();
        reloaded.restore(p.completed(), p.activeId, p.question, p.answer, p.mistake, p.firstTry);
        eq(wrongSnapshot, snapshot(reloaded), "Wrong feedback restored with selected option and mistake flag");
        p.retry(); eq(-1, p.answer, "Retry reopens current question"); check(p.mistake, "Retry retains evidence of prior attempt");
        check(p.choose(correct), "Retry can select the correct option");
        String afterRetry = snapshot(p); p.retry(); eq(afterRetry, snapshot(p), "Retry cannot erase an accepted correct answer");
        check(p.advance(), "Corrected answer may advance"); eq(0, p.firstTry, "Correct after retry is not counted as first-try success");
        eq(1, p.question, "Advance selects next question"); eq(0, p.xp(), "Partial lesson earns no completion XP");
        String inProgress = snapshot(p); p.begin("l01", false); eq(inProgress, snapshot(p), "Resume does not erase partial state");
        for (int i = 1; i < 3; i++) {
            check(!p.advance(), "Unanswered next question cannot be skipped");
            check(p.choose(p.active().questions[i].correct), "First-try correct answer accepted");
            check(p.advance(), "Answered question advances once"); check(!p.advance(), "Repeated advance cannot skip a question");
        }
        check(p.finished() && p.done("l01"), "Three accepted answers commit completion");
        eq(30, p.xp(), "Completed lesson earns 30 XP"); eq(2, p.firstTry, "First-try count excludes first question retry");
        eq("l02", p.recommended().id, "Next incomplete lesson recommended");
        reloaded.restore(p.completed(), p.activeId, p.question, p.answer, p.mistake, p.firstTry);
        eq(snapshot(p), snapshot(reloaded), "Completed screen and first-try summary restored together");
        boolean immutable = false; try { p.completed().add("l02"); } catch (UnsupportedOperationException expected) { immutable = true; }
        check(immutable, "Completion history cannot be changed through returned set");
        p.begin("l01", true); p.start();
        for (int i = 0; i < 3; i++) { p.choose(p.active().questions[i].correct); p.advance(); }
        eq(30, p.xp(), "Review does not double completion XP"); eq(3, p.firstTry, "Review has its own attempt summary");

        p.restore(saved("l01", "unknown"), "l02", 1, question("l02-q2").correct, true, 1);
        eq(saved("l01"), p.completed(), "Unknown completed ID discarded");
        eq("l02", p.activeId, "Active lesson restored"); eq(1, p.question, "Question restored");
        check(p.correct() && p.mistake, "Correct retry preserves mistake evidence across restart"); eq(1, p.firstTry, "Prior first-try count restored");
        check(p.advance(), "Restored correct choice advances"); eq(1, p.firstTry, "Restored retry does not gain first-try credit");
        p.restore(saved("l01"), "l02", 1, (question("l02-q2").correct + 1) % 3, false, 1);
        check(p.mistake, "Wrong stored choice forces consistent mistake flag even if saved flag is false");
        p.restore(saved("l01", "unknown"), "not-a-lesson", 88, 88, true, 88);
        eq("", p.activeId, "Unknown active lesson discarded"); eq(-1, p.question, "Unknown lesson restores no active question");
        eq(30, p.xp(), "Valid completed lessons survive independent corrupt active ID");
        p.restore(null, "l03", 999, 999, true, 999);
        eq(-1, p.question, "Uncommitted completed screen falls back to explanation"); eq(-1, p.answer, "Invalid selected option discarded");
        eq(0, p.firstTry, "Impossible first-try score sanitized"); check(!p.finished(), "Corrupt step cannot grant completion"); eq(0, p.xp(), "Corrupt data cannot create completion XP");
        p.restore(null, "l03", -99, 0, true, -10);
        eq(-1, p.question, "Negative step is bounded"); eq(-1, p.answer, "No choice exists on explanation screen"); eq(0, p.firstTry, "Negative first-try score is bounded");
        p.restore(null, "l03", 1, 999, false, 99);
        eq(-1, p.answer, "Out-of-range option removed from otherwise valid partial state"); eq(1, p.firstTry, "First-try score cannot exceed finished questions");
        check(!p.advance(), "Invalid restored answer does not unlock advancement");
        Set<String> complete = new LinkedHashSet<>(); for (FlowLearning.Lesson lesson : FlowLearning.lessons()) complete.add(lesson.id);
        p.restore(complete, "l24", 3, -1, false, 3);
        eq(720, p.xp(), "Entire curriculum has bounded one-time completion XP");
        eq("l01", p.recommended().id, "Completed curriculum still offers a valid lesson to review");
        System.out.println("Progress: invalid IDs/options, tap idempotence, wrong → retry → correct, gated advance, one-time 30 XP, review and full-state restore pass.");
    }

    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args.length == 0 ? "FrociartAndroid/tests/learning" : args[0]);
        dataset(root); executableContent(root); progress(); review();
        System.out.println("PASS: " + checks + " assertions. This host run does not replace native UI/layout testing or a general correctness proof.");
    }

    private static void review(){
        FlowLearningProgress p=new FlowLearningProgress();p.begin("l05",false);p.start();
        FlowLearning.Question q=p.active().questions[0];p.choose((q.correct+1)%q.options.length);
        eq(saved(q.id),p.review(),"Wrong answer remembers the exact question");p.retry();p.choose(q.correct);p.advance();
        check(p.review().contains(q.id),"An immediate retry retains a later review opportunity");
        FlowLearningProgress restored=new FlowLearningProgress();restored.restoreReview(saved(q.id,"unknown"));
        eq(saved(q.id),restored.review(),"Review survives restore and rejects unknown IDs");eq("l05",restored.reviewLesson().id,"Review chooses the lesson containing the real mistake");
        p.begin("l05",true);p.start();p.choose(q.correct);check(p.review().isEmpty(),"Clean retrieval on a later attempt clears review");
        boolean immutable=false;try{p.review().add("bad");}catch(UnsupportedOperationException expected){immutable=true;}check(immutable,"Review IDs cannot be mutated by a caller");
        restored.restoreReview(null);check(restored.review().isEmpty()&&restored.reviewLesson()==null,"Empty review has no fictitious lesson");
        System.out.println("Personalized review: exact mistake, retry retention, clean retrieval, persistence filtering and immutable IDs pass.");
    }
}
