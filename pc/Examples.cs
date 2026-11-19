using System;
using System.Collections.Generic;
using System.Linq;

namespace Frociart
{
    public static class Examples
    {
        public static List<Diagram> All()
        {
            return new List<Diagram> { Sum(), Parity(), Countdown(), Maximum(), MissingBranch(), CopyFile(), TurtleSquare() };
        }

        public static Diagram Named(string name)
        {
            Diagram diagram = All().FirstOrDefault(d => String.Equals(d.Title, name, StringComparison.OrdinalIgnoreCase));
            if (diagram == null) throw new ArgumentException("Esempio non trovato: " + name);
            return diagram;
        }

        private static FlowNode Node(Diagram d, string id, NodeKind kind, string text, float x, float y, string prompt = "")
        {
            float width = kind == NodeKind.Start || kind == NodeKind.End ? 164 :
                kind == NodeKind.Process ? 212 : kind == NodeKind.Decision ? 232 : 226;
            FlowNode node = new FlowNode { Id = id, Kind = kind, Text = text, Prompt = prompt, X = x + 116 - width / 2, Y = y };
            d.Nodes.Add(node);
            return node;
        }

        private static void Edge(Diagram d, string from, string to, string branch = "next")
        {
            d.Edges.Add(new FlowEdge { Id = from + "_" + branch + "_" + to, From = from, To = to, Branch = branch });
        }

        public static Diagram TurtleSquare(){var d=FlowPseudocode.Decode(FlowTurtle.Example);d.Title="Disegna un quadrato";return d;}

        public static Diagram CopyFile(){var d=new Diagram{Title="Copia un file di testo"};
        Node(d,"start",NodeKind.Start,"Inizio",350,40);
        Node(d,"line",NodeKind.Declare,"riga: TESTO",350,180);
        Node(d,"count",NodeKind.Declare,"righe: INTERO",350,320);
        Node(d,"open_read",NodeKind.OpenFile,"LETTURA \"dati.txt\"",350,460);
        Node(d,"open_write",NodeKind.OpenFile,"SCRITTURA \"copia.txt\"",350,600);
        Node(d,"check",NodeKind.Decision,"non EOF()",350,760);
        Node(d,"read",NodeKind.ReadFile,"riga",80,950);
        Node(d,"write",NodeKind.WriteFile,"riga",80,1090);
        Node(d,"increment",NodeKind.Process,"righe = righe + 1",80,1230);
        Node(d,"close_read",NodeKind.CloseFile,"LETTURA",620,950);
        Node(d,"close_write",NodeKind.CloseFile,"SCRITTURA",620,1090);
        Node(d,"result",NodeKind.Output,"\"Righe copiate: \" + righe",620,1230);
        Node(d,"end",NodeKind.End,"Fine",620,1370);
        Edge(d,"start","line");
        Edge(d,"line","count");
        Edge(d,"count","open_read");
        Edge(d,"open_read","open_write");
        Edge(d,"open_write","check");
        Edge(d,"check","read","true");
        Edge(d,"read","write");
        Edge(d,"write","increment");
        Edge(d,"increment","check");
        Edge(d,"check","close_read","false");
        Edge(d,"close_read","close_write");
        Edge(d,"close_write","result");
        Edge(d,"result","end");
        return d;
    }

        public static Diagram Sum()
        {
            Diagram d = new Diagram { Title = "Somma di due numeri" };
            Node(d, "start", NodeKind.Start, "Inizio", 350, 45);
            Node(d, "input_a", NodeKind.Input, "a", 350, 175, "Inserisci il primo numero");
            Node(d, "input_b", NodeKind.Input, "b", 350, 305, "Inserisci il secondo numero");
            Node(d, "sum", NodeKind.Process, "somma = a + b", 350, 435);
            Node(d, "output", NodeKind.Output, "\"Somma: \" + somma", 350, 565);
            Node(d, "end", NodeKind.End, "Fine", 350, 695);
            Edge(d, "start", "input_a");
            Edge(d, "input_a", "input_b");
            Edge(d, "input_b", "sum");
            Edge(d, "sum", "output");
            Edge(d, "output", "end");
            return d;
        }

        public static Diagram Parity()
        {
            Diagram d = new Diagram { Title = "Pari o dispari" };
            Node(d, "start", NodeKind.Start, "Inizio", 350, 45);
            Node(d, "input", NodeKind.Input, "numero", 350, 175, "Inserisci un numero intero");
            Node(d, "check", NodeKind.Decision, "numero % 2 = 0", 350, 325);
            Node(d, "even", NodeKind.Output, "\"Pari\"", 90, 495);
            Node(d, "odd", NodeKind.Output, "\"Dispari\"", 610, 495);
            Node(d, "end", NodeKind.End, "Fine", 350, 665);
            Edge(d, "start", "input");
            Edge(d, "input", "check");
            Edge(d, "check", "even", "true");
            Edge(d, "check", "odd", "false");
            Edge(d, "even", "end");
            Edge(d, "odd", "end");
            return d;
        }

        public static Diagram Countdown()
        {
            Diagram d = new Diagram { Title = "Conto alla rovescia" };
            Node(d, "start", NodeKind.Start, "Inizio", 350, 45);
            Node(d, "input", NodeKind.Input, "n", 350, 175, "Da quale numero intero vuoi partire?");
            Node(d, "check", NodeKind.Decision, "n > 0", 350, 325);
            Node(d, "count", NodeKind.Output, "n", 80, 485);
            Node(d, "decrement", NodeKind.Process, "n = n - 1", 80, 625);
            Node(d, "go", NodeKind.Output, "\"Via!\"", 610, 485);
            Node(d, "end", NodeKind.End, "Fine", 610, 625);
            Edge(d, "start", "input");
            Edge(d, "input", "check");
            Edge(d, "check", "count", "true");
            Edge(d, "check", "go", "false");
            Edge(d, "count", "decrement");
            Edge(d, "decrement", "check");
            Edge(d, "go", "end");
            return d;
        }

        public static Diagram Maximum()
        {
            Diagram d = new Diagram { Title = "Maggiore tra due numeri" };
            Node(d, "start", NodeKind.Start, "Inizio", 350, 45);
            Node(d, "input_a", NodeKind.Input, "a", 350, 175, "Inserisci il primo numero");
            Node(d, "input_b", NodeKind.Input, "b", 350, 305, "Inserisci il secondo numero");
            Node(d, "check", NodeKind.Decision, "a >= b", 350, 455);
            Node(d, "a_greater", NodeKind.Output, "\"Maggiore: \" + a", 90, 625);
            Node(d, "b_greater", NodeKind.Output, "\"Maggiore: \" + b", 610, 625);
            Node(d, "end", NodeKind.End, "Fine", 350, 795);
            Edge(d, "start", "input_a");
            Edge(d, "input_a", "input_b");
            Edge(d, "input_b", "check");
            Edge(d, "check", "a_greater", "true");
            Edge(d, "check", "b_greater", "false");
            Edge(d, "a_greater", "end");
            Edge(d, "b_greater", "end");
            return d;
        }

        public static Diagram MissingBranch()
        {
            Diagram d = new Diagram { Title = "Esercizio: trova l'errore" };
            Node(d, "start", NodeKind.Start, "Inizio", 350, 45);
            Node(d, "input", NodeKind.Input, "eta", 350, 175, "Quanti anni hai?");
            Node(d, "check", NodeKind.Decision, "eta >= 18", 350, 325);
            Node(d, "adult", NodeKind.Output, "\"Maggiorenne\"", 90, 495);
            Node(d, "minor", NodeKind.Output, "\"Minorenne\"", 610, 495);
            Node(d, "end", NodeKind.End, "Fine", 350, 665);
            Edge(d, "start", "input");
            Edge(d, "input", "check");
            Edge(d, "check", "adult", "true");
            Edge(d, "adult", "end");
            Edge(d, "minor", "end");
            return d;
        }
    }
}
