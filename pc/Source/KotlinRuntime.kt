// Support for generated Kotlin/JVM programs. No flowchart parser or interpreter.
private class Problem(message: String) : IllegalArgumentException(message)
private fun problem(message: String): Nothing = throw Problem(message)
private fun finite(value: Double): Double { if (!value.isFinite()) problem("Il risultato non è un numero finito: controlla dominio e grandezza dei valori."); return value }
private fun lower(value: String): String = value.lowercase(java.util.Locale.ROOT)
private fun shortText(value: String, length: Int = 60): String = if (value.length <= length) value else value.substring(0, length - 3) + "..."
private fun roundAway(value: Double, digits: Int): Double { if (kotlin.math.abs(value) >= 1e16) return value; val factor = Math.pow(10.0, digits.toDouble()); val scaled = value * factor; val absolute = kotlin.math.abs(scaled); var whole = Math.floor(absolute); if (absolute - whole >= .5) whole += 1; return Math.copySign(whole, scaled) / factor }

private fun formatNumber(value: Double): String {
    if(value==0.0)return "0"
    if(Math.abs(value)<1e15&&value==Math.rint(value))return value.toLong().toString()
    val n=java.math.BigDecimal(value).round(java.math.MathContext(15,java.math.RoundingMode.HALF_UP)).stripTrailingZeros()
    val exponent=n.precision()-n.scale()-1
    if(exponent>=15||exponent< -4){val digits=n.unscaledValue().abs().toString();return (if(n.signum()<0)"-" else "")+digits.substring(0,1)+(if(digits.length>1)"."+digits.substring(1) else "")+"E"+(if(exponent>=0)"+" else "-")+Math.abs(exponent).toString().padStart(2,'0')}
    return n.toPlainString()
}
private class Value private constructor(val raw: Any) {
    companion object {
        fun number(value: Double): Value = Value(finite(value))
        fun text(value: String): Value { if (value.length > 32768) problem("Il testo supera il limite di 32768 caratteri."); return Value(value) }
        fun bool(value: Boolean): Value = Value(value)
        fun vector(values: List<Value>): Value = Value(Vector(values))
        fun reference(cell: ArrayCell): Value = Value(ArrayArgument(cell))
    }
    fun isNumber(): Boolean = raw is Double
    fun isText(): Boolean = raw is String
    fun isBoolean(): Boolean = raw is Boolean
    fun isVector(): Boolean = raw is Vector
    fun asNumber(): Double { if (raw !is Double) problem("Serve un numero, ma il valore è «" + shortText(format()) + "»."); return raw }
    fun asBoolean(): Boolean = when (raw) { is Boolean -> raw; is Double -> raw != 0.0; else -> problem("La condizione deve restituire vero/falso o un numero, non un testo.") }
    fun vector(): Vector = raw as? Vector ?: problem("Serve un vettore, per esempio valori = [10, 20, 30].")
    fun vectorSize(): Int = vector().items.size
    fun format(): String = when (raw) {
        is String -> raw
        is Boolean -> if (raw) "vero" else "falso"
        is Vector -> raw.format()
        is Double -> formatNumber(raw)
        else -> problem("Il riferimento a un vettore può essere usato solo come parametro.")
    }
    override fun toString(): String = format()
}
private class Vector(values: List<Value>) {
    val items: List<Value>
    init { if (values.size > 1024) problem("Un vettore può contenere al massimo 1024 elementi."); var kind = -1; var total = 2; val copy = values.toList(); for (value in copy) { val next = scalar(value); if (kind < 0) kind = next else if (kind != next) problem("Tutti gli elementi del vettore devono avere lo stesso tipo."); total += elementText(value).length + 2; if (total > 32768) problem("Il contenuto del vettore supera 32768 caratteri.") }; items = java.util.Collections.unmodifiableList(copy) }
    companion object {
        fun scalar(value: Value): Int = when { value.isNumber() -> 0; value.isBoolean() -> 1; value.isText() -> 2; else -> problem("I vettori contengono numeri, testi o valori logici; i vettori annidati non sono supportati.") }
        fun elementText(value: Value): String = if (value.isText()) "\"" + (value.raw as String).replace("\\", "\\\\").replace("\"", "\\\"").replace("\n", "\\n").replace("\r", "\\r").replace("\t", "\\t") + "\"" else value.format()
        fun integer(value: Value, label: String): Int { val n = value.asNumber(); if (n < 0 || n > Int.MAX_VALUE || n != Math.floor(n)) problem(label + " deve essere un intero maggiore o uguale a 0."); return n.toInt() }
    }
    fun format(): String = items.joinToString(", ", "[", "]") { elementText(it) }
    fun index(value: Value): Int { val i = integer(value, "L’indice"); if (i >= items.size) problem(if (items.isEmpty()) "Il vettore è vuoto: non contiene elementi." else "Indice $i fuori dal vettore. Usa un indice da 0 a ${items.size - 1}."); return i }
    fun at(index: Int): Value = items[index(Value.number(index.toDouble()))]
    fun set(index: Int, value: Value): Value { if (scalar(at(index)) != scalar(value)) problem("Il nuovo valore deve avere lo stesso tipo degli elementi del vettore."); val next = items.toMutableList(); next[index] = value; return Value.vector(next) }
    override fun equals(other: Any?): Boolean = other is Vector && items.size == other.items.size && items.indices.all { i -> val a = items[i]; val b = other.items[i]; if (a.isNumber() && b.isNumber()) a.asNumber() == b.asNumber() else a.raw == b.raw }
    override fun hashCode(): Int = items.fold(17) { hash, value -> hash * 31 + value.raw.hashCode() }
}
private class Variables(source: Variables? = null) {
    private val data = java.util.LinkedHashMap<String, Value>()
    private val keys = HashMap<String, String>()
    init { if (source != null) for ((key, value) in source.entries) put(key, value) }
    val entries: Set<Map.Entry<String, Value>> get() = data.entries
    val values: Collection<Value> get() = data.values
    val size: Int get() = data.size
    fun put(key: String, value: Value) { val normalized = lower(key); val original = keys.getOrPut(normalized) { key }; data[original] = value }
    fun get(key: String): Value? = keys[lower(key)]?.let { data[it] }
    fun containsKey(key: String): Boolean = keys.containsKey(lower(key))
}
private object Intrinsics {
    private val names = setOf("sin", "cos", "tan", "arcsin", "arccos", "arctan", "int", "ln", "log", "log10", "sgn", "random", "size", "char", "tochar", "tocode", "tofixed", "tointeger", "toreal", "tostring")
    private val whole = Regex("^[+-]?[0-9]+$")
    private val real = Regex("^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$")
    fun handles(name: String): Boolean = name in names
    fun text(value: Value): String = value.raw as? String ?: problem("Questa funzione richiede un testo tra virgolette o una variabile di testo.")
    fun characters(text: String): List<String> { val out = ArrayList<String>(); var i = 0; while (i < text.length) { val ch = text[i]; if (Character.isHighSurrogate(ch)) { if (i + 1 >= text.length || !Character.isLowSurrogate(text[i + 1])) problem("Il testo contiene un carattere Unicode incompleto."); out.add(text.substring(i, i + 2)); i += 2 } else { if (Character.isLowSurrogate(ch)) problem("Il testo contiene un carattere Unicode incompleto."); out.add(ch.toString()); i++ } }; return out }
    fun integer(value: Value, minimum: Int, maximum: Int, label: String): Int { val n = value.asNumber(); if (n < minimum || n > maximum || n != Math.floor(n)) problem("$label deve essere un intero da $minimum a $maximum."); return n.toInt() }
    fun evaluate(name: String, args: Array<out Value>): Value {
        val first = args[0]
        if (name == "size") return Value.number(first.vectorSize().toDouble())
        if (name == "char") { val chars = characters(text(first)); val index = integer(args[1], 0, Int.MAX_VALUE, "L’indice"); if (index >= chars.size) problem(if (chars.isEmpty()) "Il testo è vuoto: non contiene caratteri." else "Indice fuori dal testo. Usa un indice da 0 a ${chars.size - 1}."); return Value.text(chars[index]) }
        if (name == "tocode") { val chars = characters(text(first)); if (chars.size != 1) problem("tocode richiede esattamente un carattere."); return Value.number(chars[0].codePointAt(0).toDouble()) }
        if (name == "int" && args.size == 2) { if (!args[1].isText() || !args[1].format().equals("passo", true)) problem("Il secondo argomento di int deve essere \"passo\" per controllare il passo di un ciclo."); if (!first.isNumber()) problem("Il passo del ciclo deve essere un numero intero diverso da zero."); val n = first.asNumber(); if (n == 0.0 || n != Math.rint(n) || n < Int.MIN_VALUE || n > Int.MAX_VALUE) problem("Il passo del ciclo deve essere un intero diverso da zero, da -2147483648 a 2147483647."); return Value.number(n) }
        if (name == "tointeger" || name == "toreal") { val source = trimInput(text(first)); val integer = name == "tointeger"; if (!(if (integer) whole else real).matches(source)) problem(if (integer) "Scrivi un testo che contenga un numero intero, per esempio \"42\"." else "Scrivi un testo che contenga un numero, per esempio \"12.5\"."); val value = finite(source.replace(',', '.').toDoubleOrNull() ?: problem("Il numero da convertire non è valido.")); if (integer && (value < Int.MIN_VALUE || value > Int.MAX_VALUE)) problem("tointeger accetta valori da -2147483648 a 2147483647."); return Value.number(value) }
        val n = first.asNumber()
        return when (name) {
            "sin" -> Value.number(Math.sin(n)); "cos" -> Value.number(Math.cos(n)); "tan" -> Value.number(Math.tan(n))
            "arcsin", "arccos" -> { if (n < -1 || n > 1) problem("$name richiede un numero tra -1 e 1."); Value.number(if (name == "arcsin") Math.asin(n) else Math.acos(n)) }
            "arctan" -> Value.number(Math.atan(n)); "int" -> Value.number(if (n < 0) Math.ceil(n) else Math.floor(n)); "sgn" -> Value.number(if (n < 0) -1.0 else if (n > 0) 1.0 else 0.0)
            "ln", "log", "log10" -> { if (n <= 0) problem("Il logaritmo richiede un numero maggiore di 0."); Value.number(if (name == "log10") Math.log10(n) else Math.log(n)) }
            "random" -> Value.number(java.util.concurrent.ThreadLocalRandom.current().nextInt(integer(first, 1, Int.MAX_VALUE, "Il limite di random")).toDouble())
            "tochar" -> { val code = integer(first, 0, 0x10ffff, "Il codice Unicode"); if (code in 0xd800..0xdfff) problem("Il codice non rappresenta un carattere Unicode completo."); Value.text(String(Character.toChars(code))) }
            "tostring" -> Value.text(first.format())
            "tofixed" -> { val digits = integer(args[1], 0, 15, "Le cifre decimali"); Value.text(java.math.BigDecimal(roundAway(n, digits)).setScale(digits, java.math.RoundingMode.HALF_UP).toPlainString()) }
            else -> problem("Funzione sconosciuta.")
        }
    }
}
private fun inputSpace(c: Char): Boolean = c in '\u0009'..'\u000d' || c == '\u0020' || c == '\u0085' || c == '\u00a0' || c == '\u1680' || c in '\u2000'..'\u200a' || c == '\u2028' || c == '\u2029' || c == '\u202f' || c == '\u205f' || c == '\u3000'
private fun trimInput(value: String): String = value.trim(::inputSpace)
private fun numericInputText(value: String): String = value.trimEnd('\u0000').trimEnd { it == ' ' || it in '\u0009'..'\u000d' }
private fun parseInput(raw: String): Value {
    if (raw.length > 32768) problem("Il valore inserito è troppo lungo (massimo 32768 caratteri).")
    val text = trimInput(raw)
    val numeric = numericInputText(text)
    if (Regex("^[+-]?(?:[0-9]+(?:[.,][0-9]*)?|[.,][0-9]+)(?:[eE][+-]?[0-9]+)?$").matches(numeric)) { val value = numeric.replace(',', '.').toDoubleOrNull() ?: problem("Il numero inserito è troppo grande. Usa un numero finito più piccolo."); if (!value.isFinite() && numeric != text) return Value.text(raw); return Value.number(value) }
    return when (lower(text)) { "vero", "true" -> Value.bool(true); "falso", "false" -> Value.bool(false); "nan", "infinity", "+infinity", "-infinity", "infinito" -> problem("Inserisci un numero finito."); else -> Value.text(raw) }
}
private class TypeSpec(val kind: Int, val array: Boolean, val length: Int)
private class ArrayCell(var value: Value, var kind: Int)
private class ArrayArgument(val cell: ArrayCell) { val snapshot: Value = cell.value }

private class TextFiles {
    interface Journal { fun append(text: String); fun close(); fun saved(): Boolean }
    var journalFactory: ((String) -> Journal)? = null
    private var journal: Journal? = null
    private var input: String? = null
    private var readName: String? = null
    private var writeName: String? = null
    private var position = 0
    private var output: StringBuilder? = null
    private var outputBytes = 0L
    fun hasRead(): Boolean = input != null
    fun hasWrite(): Boolean = output != null
    fun writeName(): String = writeName ?: problem("Apri prima un file in scrittura.")
    fun outputDraft(): String? = output?.toString()
    fun closeJournal() { journal?.close() }
    private fun requireRead(): String = input ?: problem("Apri prima un file in lettura. EOF() controlla quel file.")
    fun eof(): Boolean = position >= requireRead().length
    companion object {
        const val MAX_BYTES = 4194304
        const val MAX_CHARACTERS = 2097152
        const val MAX_LINE = 32768
        fun name(value: Value): String { if (!value.isText()) problem("Il nome del file deve essere un testo da 1 a 256 caratteri."); val name = value.format(); if (name.trim().isEmpty() || name.length > 256) problem("Il nome del file deve essere un testo da 1 a 256 caratteri."); if (name.any { Character.isISOControl(it) }) problem("Il nome del file non può contenere righe nuove o caratteri di controllo."); utf8(name); return name }
        fun utf8(text: String): ByteArray { try { val buffer = java.nio.charset.StandardCharsets.UTF_8.newEncoder().onMalformedInput(java.nio.charset.CodingErrorAction.REPORT).onUnmappableCharacter(java.nio.charset.CodingErrorAction.REPORT).encode(java.nio.CharBuffer.wrap(text)); return ByteArray(buffer.remaining()).also { buffer.get(it) } } catch (_: java.nio.charset.CharacterCodingException) { problem("Il file contiene caratteri Unicode non validi.") } }
        fun validate(text: String) { if (text.length > MAX_CHARACTERS) problem("Il file supera 2 milioni di caratteri."); if (utf8(text).size > MAX_BYTES) problem("Il file supera 4 MiB.") }
        fun readText(stream: java.io.InputStream): String { val bytes = java.io.ByteArrayOutputStream(); val buffer = ByteArray(8192); while (true) { val count = stream.read(buffer); if (count < 0) break; if (bytes.size().toLong() + count > MAX_BYTES) problem("Il file supera 4 MiB."); bytes.write(buffer, 0, count) }; val data = bytes.toByteArray(); var start = 0; var encoding = java.nio.charset.StandardCharsets.UTF_8; fun byte(at: Int): Int = data[at].toInt() and 255
            if (data.size >= 3 && byte(0) == 239 && byte(1) == 187 && byte(2) == 191) start = 3
            else if (data.size >= 2 && byte(0) == 255 && byte(1) == 254) { start = 2; encoding = java.nio.charset.StandardCharsets.UTF_16LE }
            else if (data.size >= 2 && byte(0) == 254 && byte(1) == 255) { start = 2; encoding = java.nio.charset.StandardCharsets.UTF_16BE }
            try { val text = encoding.newDecoder().onMalformedInput(java.nio.charset.CodingErrorAction.REPORT).onUnmappableCharacter(java.nio.charset.CodingErrorAction.REPORT).decode(java.nio.ByteBuffer.wrap(data, start, data.size - start)).toString(); validate(text); return text } catch (_: java.nio.charset.CharacterCodingException) { problem("Il file non è un testo UTF-8 o UTF-16 valido. Salvalo come UTF-8 e riprova.") }
        }
    }
    fun openRead(name: String, text: String) { if (hasRead()) problem("Un file è già aperto in lettura. Chiudilo prima di aprirne un altro."); validate(text); input = text; position = 0; readName = name }
    fun openWrite(name: String) { if (hasWrite()) problem("Un file è già aperto in scrittura. Chiudilo e salvalo prima di aprirne un altro."); try { journal = journalFactory?.invoke(name) } catch (_: java.io.IOException) { problem("Non riesco a preparare la bozza locale.") }; output = StringBuilder(); outputBytes = 0; writeName = name }
    fun peekLine(): String { val text = requireRead(); if (eof()) problem("Il file è terminato. Usa non EOF() nella condizione del ciclo prima di leggere."); var end = position; while (end < text.length && text[end] != '\r' && text[end] != '\n') { if (end - position >= MAX_LINE) problem("Una riga del file supera 32768 caratteri."); end++ }; return text.substring(position, end) }
    fun commitRead() { val length = peekLine().length; val text = requireRead(); position += length; if (position < text.length && text[position] == '\r') { position++; if (position < text.length && text[position] == '\n') position++ } else if (position < text.length && text[position] == '\n') position++ }
    fun write(value: String, newline: Boolean) { val buffer = output ?: problem("Apri prima un file in scrittura."); val text = value + if (newline) "\n" else ""; val bytes = utf8(text).size; if (buffer.length.toLong() + text.length > MAX_CHARACTERS || outputBytes + bytes > MAX_BYTES) problem("Il file generato supera il limite di 4 MiB o 2 milioni di caratteri."); try { journal?.append(text) } catch (_: java.io.IOException) { problem("Scrittura interrotta: non riesco a conservare la bozza locale.") }; buffer.append(text); outputBytes += bytes }
    fun closeRead() { requireRead(); input = null; position = 0; readName = null }
    fun saved() { journal?.saved(); journal = null; output = null; outputBytes = 0; writeName = null }
}
private class Turtle {
    class Segment(val x1: Double, val y1: Double, val x2: Double, val y2: Double, val color: String)
    class Snapshot(owner: Turtle) { val x = owner.x; val y = owner.y; val heading = owner.heading; val color = owner.color; val revision = owner.revision; val segments = owner.lines.toList() }
    private val lines = ArrayList<Segment>()
    var x = 0.0; private set
    var y = 0.0; private set
    var heading = 90.0; private set
    var color = "#000000"; private set
    var revision = 0L; private set
    var used = false; private set
    fun segments(): List<Segment> = java.util.Collections.unmodifiableList(lines)
    fun capture(): Snapshot = Snapshot(this)
    fun apply(kind: Int, value: Value?, flag: Boolean) {
        when (kind) {
            12 -> { val distance = number(value); val radians = heading * Math.PI / 180; val cos = when (heading) { 90.0, 270.0 -> 0.0; 0.0 -> 1.0; 180.0 -> -1.0; else -> Math.cos(radians) }; val sin = when (heading) { 0.0, 180.0 -> 0.0; 90.0 -> 1.0; 270.0 -> -1.0; else -> Math.sin(radians) }; val nx = x + distance * cos; val ny = y + distance * sin; coordinate(nx); coordinate(ny); if (flag && (nx != x || ny != y)) { if (lines.size >= 10000) problem("Il disegno supera 10000 tratti. Controlla il ciclo o usa Pulisci disegno."); lines.add(Segment(x, y, nx, ny, color)) }; x = nx; y = ny }
            13 -> { val angle = number(value) % 360; heading = ((heading + if (flag) angle else -angle) % 360 + 360) % 360 }
            14 -> { x = 0.0; y = 0.0; heading = 90.0 }
            15 -> { lines.clear(); x = 0.0; y = 0.0; heading = 90.0 }
            16 -> color = parseColor(value)
            else -> problem("Comando tartaruga non riconosciuto.")
        }; used = true; revision++
    }
    companion object {
        private fun number(value: Value?): Double { if (value == null || !value.isNumber()) problem("La distanza e l'angolo richiedono un numero."); return finite(value.asNumber()) }
        private fun coordinate(value: Double) { if (!value.isFinite() || Math.abs(value) > 1000000) problem("Il disegno supera lo spazio disponibile: coordinate tra -1000000 e 1000000.") }
        private val colors = arrayOf(arrayOf("nero","black","#000000"),arrayOf("bianco","white","#FFFFFF"),arrayOf("grigio","gray","#808080"),arrayOf("rosso","red","#FF0000"),arrayOf("arancione","orange","#FF8C00"),arrayOf("giallo","yellow","#FFFF00"),arrayOf("verde","green","#008000"),arrayOf("lime","lime","#00FF00"),arrayOf("turchese","teal","#008080"),arrayOf("ciano","cyan","#00FFFF"),arrayOf("blu","blue","#0000FF"),arrayOf("azzurro","skyblue","#00BFFF"),arrayOf("viola","purple","#800080"),arrayOf("magenta","magenta","#FF00FF"),arrayOf("rosa","pink","#FF69B4"),arrayOf("marrone","brown","#A52A2A"))
        fun parseColor(value: Value?): String { if (value == null || !value.isText()) problem("Il colore richiede un testo, per esempio \"blu\" o \"#3366FF\"."); val text = value.format().trim(); for (row in colors) if (row[0].equals(text, true) || row[1].equals(text, true)) return row[2]; if (text.equals("grey", true)) return "#808080"; if (Regex("#[0-9a-fA-F]{6}").matches(text)) return text.uppercase(java.util.Locale.ROOT); if (Regex("#[0-9a-fA-F]{3}").matches(text)) return ("#" + text[1] + text[1] + text[2] + text[2] + text[3] + text[3]).uppercase(java.util.Locale.ROOT); problem("Colore non riconosciuto. Usa un nome, per esempio \"rosso\", oppure #RRGGBB fra virgolette.") }
        fun svg(snapshot: Snapshot): String { var minX = Math.min(0.0, snapshot.x); var maxX = Math.max(0.0, snapshot.x); var minY = Math.min(0.0, snapshot.y); var maxY = Math.max(0.0, snapshot.y); for (s in snapshot.segments) { minX = minOf(minX, s.x1, s.x2); maxX = maxOf(maxX, s.x1, s.x2); minY = minOf(minY, s.y1, s.y2); maxY = maxOf(maxY, s.y1, s.y2) }; val margin = maxOf(10.0, maxOf(maxX - minX, maxY - minY) * .05); val width = maxOf(1.0, maxX - minX) + 2 * margin; val height = maxOf(1.0, maxY - minY) + 2 * margin; val b = StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"${minX-margin} ${-maxY-margin} $width $height\"><title>Disegno tartaruga — Frociart</title><rect x=\"${minX-margin}\" y=\"${-maxY-margin}\" width=\"$width\" height=\"$height\" fill=\"white\"/>"); for (s in snapshot.segments) b.append("<line x1=\"${s.x1}\" y1=\"${-s.y1}\" x2=\"${s.x2}\" y2=\"${-s.y2}\" stroke=\"${s.color}\" stroke-width=\"2\" stroke-linecap=\"round\" vector-effect=\"non-scaling-stroke\"/>"); return b.append("</svg>").toString() }
    }
}

private class FcRuntime(
    input: ((String) -> String?)? = null,
    val writer: ((String) -> Unit)? = null,
    readFile: ((String) -> String)? = null,
    saveFile: ((String, String) -> Boolean)? = null
) {
    val files = TextFiles()
    val turtle = Turtle()
    val output = ArrayList<String>()
    val outputNewlines = ArrayList<Boolean>()
    val frames = ArrayList<Frame>()
    var lastVariables = Variables(); private set
    var errorVariables: Variables? = null; private set
    var steps = 0; private set
    private var outputSize = 0L
    var maxSteps = 10000
    private var references = false
    private val console = java.io.BufferedReader(java.io.InputStreamReader(System.`in`, java.nio.charset.StandardCharsets.UTF_8))
    val reader: (String) -> String? = input ?: { prompt -> consoleRead(prompt) }
    var fileReader: (String) -> String = readFile ?: { name -> readPath(name) }
    var fileSaver: (String, String) -> Boolean = saveFile ?: { name, text -> savePath(name, text) }
    private var recoveryPath: java.nio.file.Path? = null
    init { if (saveFile == null) files.journalFactory = { name -> journal(name) } }
    class Frame(val scope: String?) {
        var variables = Variables()
        var transaction: Variables? = null
        val retained = HashMap<Int, MutableList<Value>>()
        var assignment: Value? = null
        val bindings = java.util.TreeMap<String, ArrayCell>(String.CASE_INSENSITIVE_ORDER)
        val newArrays = java.util.TreeMap<String, ArrayCell>(String.CASE_INSENSITIVE_ORDER)
        val overlays = HashMap<ArrayCell, ArrayCell>()
        val wholeWrites = java.util.TreeSet<String>(String.CASE_INSENSITIVE_ORDER)
    }
    private fun consoleRead(prompt: String): String? { print(prompt); System.out.flush(); return try { console.readLine() } catch (_: java.io.IOException) { problem("Non riesco a leggere l’input.") } }
    fun guard() { if (steps >= maxSteps) problem("Limite di esecuzione raggiunto. Controlla la condizione del ciclo.") }
    fun step() { guard(); steps++ }
    fun test(expression: () -> Value): Boolean { step(); return expression().asBoolean() }
    fun guardCall() { if (frames.size >= 128) problem("Troppe chiamate annidate: massimo 128 livelli.") }
    fun enter(scope: String?, names: Array<String>, values: Array<Value>, withReferences: Boolean = false) { guardCall(); references = references || withReferences; val f = Frame(scope); for (i in names.indices) { var value = values[i]; val argument = value.raw as? ArrayArgument; if (argument != null) { f.bindings[names[i]] = argument.cell; value = argument.cell.value }; f.variables.put(names[i], value) }; frames.add(f) }
    fun frame(): Frame = frames.last()
    fun lookup(name: String): Value { val f = frame(); return value((f.transaction ?: f.variables).get(name), name) }
    fun value(value: Value?, name: String): Value = value ?: if (name.equals("pi", true) || name == "π") Value.number(Math.PI) else problem("La variabile «$name» non ha ancora un valore.")
    fun requireValue(value: Value?): Value = value ?: problem("La funzione non restituisce un valore.")
    fun reference(name: String, declaration: TypeSpec?, parameter: TypeSpec?): Value {
        val f = frame(); val value = lookup(name); value.vector(); val kind = (declaration ?: parameter)?.kind ?: 0
        if (f.transaction != null && (name in f.wholeWrites || !f.variables.containsKey(name))) { val cell = f.newArrays.getOrPut(name) { ArrayCell(value, kind) }; return Value.reference(cell) }
        val cell = f.bindings.getOrPut(name) { val original = requireValue(f.variables.get(name)); original.vector(); ArrayCell(original, kind) }
        if (f.transaction != null) { f.overlays[cell]?.let { return Value.reference(it) }; if (value !== cell.value) return Value.reference(overlay(f, cell, value)) }
        return Value.reference(cell)
    }
    fun checkReference(argument: Value, kind: Int): Value { val address = argument.raw as? ArrayArgument ?: problem("Parametro per riferimento non valido."); checkType(address.snapshot, kind, true); val cell = address.cell; if (cell.kind != 0 && kind != 0 && cell.kind != kind) problem("Il tipo del vettore per riferimento deve coincidere con quello originale."); checkType(cell.value, kind, true); if (cell.kind == 0 && kind != 0) cell.kind = kind; return cell.value }
    private fun overlay(f: Frame, cell: ArrayCell, value: Value): ArrayCell = f.overlays.getOrPut(cell) { ArrayCell(value, cell.kind) }
    private fun propagate(cell: ArrayCell) { for (f in frames) { for ((name, bound) in f.bindings) if (bound === cell) { f.variables.put(name, cell.value); if (f.transaction != null && name !in f.wholeWrites) f.transaction!!.put(name, f.overlays[cell]?.value ?: cell.value) }; val transaction = f.transaction; if (transaction != null) { for ((name, bound) in f.newArrays) if (bound === cell) transaction.put(name, cell.value); for ((original, staged) in f.overlays) if (staged === cell) for ((name, bound) in f.bindings) if (bound === original && name !in f.wholeWrites) transaction.put(name, cell.value) } } }
    private fun checkCell(cell: ArrayCell, value: Value) { if (value.vectorSize() != cell.value.vectorSize()) problem("Un vettore per riferimento conserva la dimensione originale."); checkType(value, cell.kind, true) }
    fun captureError() { if (errorVariables == null && frames.isNotEmpty()) errorVariables = Variables(frame().variables) }
    fun leave() { val f = frame(); if (f.scope == null) lastVariables = Variables(f.variables); frames.removeAt(frames.lastIndex) }
    private fun valueSize(value: Value?): Long { if (value == null) return 8; val raw = value.raw; if (raw is ArrayArgument) return valueSize(raw.snapshot); if (raw is String) return raw.length.toLong(); if (raw is Vector) return raw.items.size * 8L + raw.items.sumOf { if (it.raw is String) it.raw.length.toLong() else 0L }; return 8 }
    fun checkMemory() { var size = outputSize; var slots = 0L; for (f in frames) { for (values in listOfNotNull(f.variables, f.transaction)) { slots += values.size; for (v in values.values) size += valueSize(v) }; for (retained in f.retained.values) for (v in retained) size += valueSize(v); size += if (f.assignment == null) 0 else valueSize(f.assignment); if (size > 2097152 || slots > 16384) problem("La memoria dei valori supera il limite di esecuzione.") } }
    fun begin() { val f = frame(); f.transaction = Variables(f.variables); if (references) { f.overlays.clear(); f.newArrays.clear(); f.wholeWrites.clear() } }
    fun stage(name: String, value: Value, check: Boolean = true, indexed: Boolean = false): Value {
        val f = frame(); val transaction = f.transaction ?: problem("Assegnazione fuori da un blocco.")
        if (!references) transaction.put(name, value)
        else if (!indexed) { transaction.put(name, value); f.wholeWrites.add(name); f.newArrays.remove(name) }
        else if (name in f.wholeWrites && f.newArrays[name] != null) { val cell = f.newArrays[name]!!; checkCell(cell, value); cell.value = value; propagate(cell) }
        else if (name !in f.wholeWrites && f.bindings[name] != null) { val cell = overlay(f, f.bindings[name]!!, requireValue(transaction.get(name))); checkCell(cell, value); cell.value = value; propagate(cell) }
        else transaction.put(name, value)
        if (check) checkMemory(); return value
    }
    fun staged(name: String): Value? = frame().transaction?.get(name)
    fun variable(name: String): Value? = frame().variables.get(name)
    fun commit(check: Boolean = false) { if (check) checkMemory(); val f = frame(); if (references) { for ((original, staged) in f.overlays) checkCell(original, staged.value); for (cell in f.newArrays.values) checkCell(cell, cell.value); for (name in f.wholeWrites) f.bindings.remove(name); for ((original, staged) in f.overlays) { original.value = staged.value; propagate(original) }; f.bindings.putAll(f.newArrays) }; f.variables = f.transaction ?: problem("Assegnazione fuori da un blocco."); f.transaction = null; f.overlays.clear(); f.newArrays.clear(); f.wholeWrites.clear() }
    fun acceptInput(name: String, value: Value, indexed: Boolean) { begin(); try { stage(name, value, true, indexed); commit() } finally { frame().transaction = null } }
    fun <T : Value?> hold(key: Int, value: T): T { if (value != null) frame().retained.getOrPut(key) { ArrayList() }.add(value); return value }
    fun <T : Value?> release(key: Int, value: T): T { frame().retained.remove(key); return value }
    fun <T : Value?> call(key: Int, invocation: () -> T): T { val value = invocation(); release(key, value); hold(-1, value); try { checkMemory() } finally { release(-1, value) }; return value }
    fun unary(op: String, value: Value): Value = if (op == "not") Value.bool(!value.asBoolean()) else Value.number(if (op == "-") -value.asNumber() else value.asNumber())
    fun binary(op: String, a: Value, b: Value): Value {
        if (op == "&" || op == "+" && (a.isText() || b.isText())) return Value.text(a.format() + b.format())
        if (op in setOf("=", "==", "!=", "<>")) { val same = if (a.isNumber() && b.isNumber()) a.asNumber() == b.asNumber() else a.raw == b.raw; return Value.bool(if (op == "!=" || op == "<>") !same else same) }
        if (op in setOf("<", "<=", ">", ">=")) { val c = if (a.isText() && b.isText()) (a.raw as String).compareTo(b.raw as String) else java.lang.Double.compare(if (a.asNumber() == 0.0) 0.0 else a.asNumber(), if (b.asNumber() == 0.0) 0.0 else b.asNumber()); return Value.bool(when (op) { "<" -> c < 0; "<=" -> c <= 0; ">" -> c > 0; else -> c >= 0 }) }
        val x = a.asNumber(); val y = b.asNumber(); return Value.number(when (op) { "+" -> x + y; "-" -> x - y; "*" -> x * y; "/" -> { if (y == 0.0) problem("Divisione per zero."); x / y }; "%" -> { if (y == 0.0) problem("Resto con divisore zero."); x % y }; "^" -> Math.pow(x, y); else -> problem("Operatore non riconosciuto.") })
    }
    fun at(target: Value, index: () -> Value): Value { val v = target.vector(); return v.at(v.index(index())) }
    fun atName(name: String, index: () -> Value): Value { lookup(name).vector(); val i = index(); val v = lookup(name).vector(); return v.at(v.index(i)) }
    fun setAfter(value: Value, target: Value, index: () -> Value): Value { val f = frame(); f.assignment = value; try { val v = target.vector(); return v.set(v.index(index()), value) } finally { f.assignment = null } }
    fun setAfterName(value: Value, name: String, index: () -> Value): Value { lookup(name).vector(); val f = frame(); f.assignment = value; try { val i = index(); val v = lookup(name).vector(); return v.set(v.index(i), value) } finally { f.assignment = null } }
    fun checkType(value: Value?, kind: Int, array: Boolean): Value { val v = value ?: problem("Il valore non corrisponde al tipo dichiarato."); if (array) { v.vector(); for (item in v.vector().items) checkType(item, kind, false); return v }; val valid = when (kind) { 1 -> v.isNumber() && v.asNumber() == Math.rint(v.asNumber()); 2 -> v.isNumber(); 3 -> v.isText(); 4 -> v.isBoolean(); 5 -> false; else -> !v.isVector() }; if (!valid) problem("Il valore non corrisponde al tipo dichiarato."); return v }
    private fun convertScalar(value: Value, kind: Int): Value = if (kind == 1 && value.isNumber()) Value.number(if (value.asNumber() < 0) Math.ceil(value.asNumber()) else Math.floor(value.asNumber())) else checkType(value, kind, false)
    fun convert(original: Value, declaration: TypeSpec?, parameter: TypeSpec?): Value { var value = original; if (declaration != null) { value = if (declaration.array) { if (!value.isVector() || value.vectorSize() != declaration.length) problem("La dimensione del vettore dichiarato non può cambiare."); Value.vector(value.vector().items.map { convertScalar(it, declaration.kind) }) } else convertScalar(value, declaration.kind) }; if (parameter != null) value = checkType(value, parameter.kind, parameter.array); return value }
    fun initial(kind: Int, array: Boolean, length: Int): Value { val value = when (kind) { 3 -> Value.text(""); 4 -> Value.bool(false); else -> Value.number(0.0) }; return if (array) Value.vector(List(length) { value }) else value }
    private fun wantsText(declaration: TypeSpec?, parameter: TypeSpec?, indexed: Boolean): Boolean { val spec = declaration ?: parameter; return spec != null && spec.kind == 3 && (indexed || !spec.array) }
    fun read(name: String, question: String?, declaration: TypeSpec?, parameter: TypeSpec?, vector: Value?, indexExpression: (() -> Value)?): Value { guard(); var display = name; var index: Int? = null; if (indexExpression != null) { var v = requireValue(vector).vector(); val i = indexExpression(); if (references) v = lookup(name).vector(); index = v.index(i); display += "[$index]" }; guard(); val prompt = if (question.isNullOrBlank() || question == "Inserisci un valore per x") "Inserisci un valore per $display" else question
        while (true) { val raw = reader("$prompt: ") ?: problem("Input terminato prima di ricevere un valore valido."); try { var value = if (wantsText(declaration, parameter, index != null)) Value.text(raw) else parseInput(raw); if (index != null) value = (if (references) lookup(name) else requireValue(vector)).vector().set(index, value); value = convert(value, declaration, parameter); acceptInput(name, value, index != null); return value } catch (failure: Problem) { System.err.println(failure.message) } }
    }
    fun emit(value: Value?, newline: Boolean = true) { val text = value?.format() ?: ""; if (outputSize + text.length > 262144) problem("Il risultato supera 256 KiB."); output.add(text); outputNewlines.add(newline); outputSize += text.length; val sink = writer; if (sink == null) { if (newline) println(text) else print(text); System.out.flush() } else sink(text) }
    fun intrinsic(name: String, vararg args: Value): Value { if (name == "eof") return Value.bool(files.eof()); val first = args[0]; if (name == "array" || name == "vettore") { val length = Vector.integer(first, "La dimensione"); if (length > 1024) problem("Un vettore può contenere al massimo 1024 elementi."); val initial = if (args.size == 2) args[1] else Value.number(0.0); Vector.scalar(initial); return Value.vector(List(length) { initial }) }; if (name == "len") return when { first.isVector() -> Value.number(first.vectorSize().toDouble()); first.isText() -> Value.number(Intrinsics.characters(first.format()).size.toDouble()); else -> problem("len richiede un testo o un vettore.") }; if (name == "sum" || name == "somma" || (name == "min" || name == "max") && args.size == 1) { val vector = first.vector(); if (vector.items.isEmpty() && name != "sum" && name != "somma") problem("Il vettore è vuoto."); var n = 0.0; for ((index, value) in vector.items.withIndex()) { val v = value.asNumber(); n = when (name) { "min" -> if (index == 0) v else Math.min(n, v); "max" -> if (index == 0) v else Math.max(n, v); else -> finite(n + v) } }; return Value.number(n) }; if (Intrinsics.handles(name)) return Intrinsics.evaluate(name, args); var n = first.asNumber(); return when (name) { "abs" -> Value.number(Math.abs(n)); "sqrt" -> { if (n < 0) problem("La radice richiede un numero non negativo."); Value.number(Math.sqrt(n)) }; "floor" -> Value.number(Math.floor(n)); "round" -> Value.number(roundAway(n, if (args.size == 1) 0 else Intrinsics.integer(args[1], 0, 15, "Le cifre decimali"))); "min", "max" -> { for (value in args.drop(1)) n = if (name == "min") Math.min(n, value.asNumber()) else Math.max(n, value.asNumber()); Value.number(n) }; else -> problem("Funzione sconosciuta.") } }
    fun requireClosed(reading: Boolean) { if (if (reading) files.hasRead() else files.hasWrite()) problem("Chiudi il file precedente prima di aprirne un altro.") }
    fun requireWrite() { if (!files.hasWrite()) problem("Apri prima un file in scrittura.") }
    fun openFile(reading: Boolean, value: Value) { requireClosed(reading); val name = TextFiles.name(value); if (reading) files.openRead(name, fileReader(name)) else files.openWrite(name) }
    fun readFile(name: String, declaration: TypeSpec?, parameter: TypeSpec?, vector: Value?, indexExpression: (() -> Value)?): Value { var index: Int? = null; if (indexExpression != null) { var v = requireValue(vector).vector(); val i = indexExpression(); if (references) v = lookup(name).vector(); index = v.index(i) }; val raw = files.peekLine(); var value = if (wantsText(declaration, parameter, index != null)) Value.text(raw) else parseInput(raw); if (index != null) value = (if (references) lookup(name) else requireValue(vector)).vector().set(index, value); value = convert(value, declaration, parameter); acceptInput(name, value, index != null); files.commitRead(); return value }
    fun writeFile(value: Value, newline: Boolean) { files.write(value.format(), newline) }
    fun closeFile(reading: Boolean) { if (reading) files.closeRead() else { requireWrite(); if (!fileSaver(files.writeName(), files.outputDraft()!!)) problem("Salvataggio annullato: la bozza resta disponibile."); files.saved() } }
    fun finishFiles() { if (files.hasWrite()) closeFile(false); if (files.hasRead()) files.closeRead() }
    fun turtle(kind: Int, value: Value?, flag: Boolean) { turtle.apply(kind, value, flag) }
    private fun readPath(name: String): String { try { return java.nio.file.Files.newInputStream(java.nio.file.Paths.get(name)).use { TextFiles.readText(it) } } catch (_: java.io.IOException) { problem("Non riesco a leggere il file «$name».") } catch (_: SecurityException) { problem("Non riesco a leggere il file «$name».") } }
    private fun journal(name: String): TextFiles.Journal { val target = java.nio.file.Paths.get(name).toAbsolutePath(); val parent = target.parent; if (parent == null || !java.nio.file.Files.isDirectory(parent)) throw java.io.IOException("Cartella non disponibile"); val draft = java.nio.file.Files.createTempFile(parent, ".frociart-recovery-", ".txt"); recoveryPath = draft; val channel = java.nio.channels.FileChannel.open(draft, java.nio.file.StandardOpenOption.WRITE); return object : TextFiles.Journal { override fun append(text: String) { val bytes = java.nio.ByteBuffer.wrap(TextFiles.utf8(text)); while (bytes.hasRemaining()) channel.write(bytes); channel.force(true) }; override fun close() { channel.close() }; override fun saved(): Boolean = try { channel.close(); true } catch (_: java.io.IOException) { false } } }
    private fun savePath(name: String, @Suppress("UNUSED_PARAMETER") text: String): Boolean { try { files.closeJournal(); val target = java.nio.file.Paths.get(name).toAbsolutePath(); val draft = recoveryPath ?: problem("La bozza del file non è disponibile."); if (java.nio.file.Files.exists(target, java.nio.file.LinkOption.NOFOLLOW_LINKS)) { val answer = reader("Il file «$name» esiste. Sostituirlo? [s/N] "); if (!answer?.trim().equals("s", true)) return false; java.nio.file.Files.move(draft, target, java.nio.file.StandardCopyOption.REPLACE_EXISTING, java.nio.file.StandardCopyOption.ATOMIC_MOVE) } else { java.nio.channels.FileChannel.open(target, java.nio.file.StandardOpenOption.CREATE_NEW, java.nio.file.StandardOpenOption.WRITE).use { out -> java.nio.channels.FileChannel.open(draft, java.nio.file.StandardOpenOption.READ).use { input -> val buffer = java.nio.ByteBuffer.allocate(8192); while (input.read(buffer) >= 0) { buffer.flip(); while (buffer.hasRemaining()) out.write(buffer); buffer.clear() }; out.force(true) } }; java.nio.file.Files.delete(draft) }; recoveryPath = null; return true } catch (_: java.io.IOException) { problem("File non salvato. La bozza è conservata in $recoveryPath.") } catch (_: SecurityException) { problem("File non salvato. La bozza è conservata in $recoveryPath.") } }
    fun drawing(width: Int, height: Int): java.awt.image.BufferedImage { val image = java.awt.image.BufferedImage(width, height, java.awt.image.BufferedImage.TYPE_INT_RGB); val g = image.createGraphics(); try { g.color = java.awt.Color.WHITE; g.fillRect(0, 0, width, height); g.setRenderingHint(java.awt.RenderingHints.KEY_ANTIALIASING, java.awt.RenderingHints.VALUE_ANTIALIAS_ON); g.stroke = java.awt.BasicStroke(2f, java.awt.BasicStroke.CAP_ROUND, java.awt.BasicStroke.JOIN_ROUND); val lines = turtle.segments(); var minX = minOf(0.0, turtle.x); var maxX = maxOf(0.0, turtle.x); var minY = minOf(0.0, turtle.y); var maxY = maxOf(0.0, turtle.y); for (s in lines) { minX = minOf(minX, s.x1, s.x2); maxX = maxOf(maxX, s.x1, s.x2); minY = minOf(minY, s.y1, s.y2); maxY = maxOf(maxY, s.y1, s.y2) }; val scale = minOf((width - 48) / maxOf(1.0, maxX - minX), (height - 48) / maxOf(1.0, maxY - minY)); val cx = (minX + maxX) / 2; val cy = (minY + maxY) / 2; for (s in lines) { g.color = java.awt.Color.decode(s.color); g.draw(java.awt.geom.Line2D.Double(width / 2.0 + (s.x1 - cx) * scale, height / 2.0 - (s.y1 - cy) * scale, width / 2.0 + (s.x2 - cx) * scale, height / 2.0 - (s.y2 - cy) * scale)) } } finally { g.dispose() }; return image }
    private fun showDrawing() { if (!turtle.used) return; if (java.awt.GraphicsEnvironment.isHeadless()) { System.err.println("Il disegno richiede un ambiente grafico JVM."); return }; javax.swing.SwingUtilities.invokeLater { val window = javax.swing.JFrame("Disegno — Frociart"); window.defaultCloseOperation = javax.swing.JFrame.DISPOSE_ON_CLOSE; val panel = object : javax.swing.JPanel() { override fun paintComponent(g: java.awt.Graphics) { super.paintComponent(g); g.drawImage(drawing(maxOf(50, width), maxOf(50, height)), 0, 0, null) } }; panel.preferredSize = java.awt.Dimension(800, 600); window.contentPane = panel; window.pack(); window.setLocationByPlatform(true); window.isVisible = true } }
    fun run(main: () -> Value?, drawing: Boolean = true): Boolean { try { main(); if (drawing) showDrawing(); return true } catch (error: RuntimeException) { System.err.println(error.message ?: "Esecuzione interrotta."); return false } catch (error: StackOverflowError) { System.err.println(error.message ?: "Esecuzione interrotta."); return false } finally { try { files.closeJournal() } catch (_: java.io.IOException) { System.err.println("Non riesco a chiudere la bozza.") }; if (recoveryPath != null) System.err.println("Bozza recuperabile: $recoveryPath") } }
}
