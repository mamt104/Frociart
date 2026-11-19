using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Frociart
{
    // A live window owns a separate slot and a persistent OS file lease. A
    // process crash releases the lease without removing its saved document.
    internal sealed class FlowRecoverySession : IDisposable
    {
        internal readonly string DataFolder, Folder, ArchiveFolder, SnapshotPath;
        private readonly string[] legacy;
        private FileStream lease;
        private bool disposed;

        internal FlowRecoverySession()
        {
            string isolated = Environment.GetEnvironmentVariable("FROCIART_DATA_DIR");
            DataFolder = Path.GetFullPath(isolated ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frociart"));
            Folder = Path.Combine(DataFolder, "Recuperi"); ArchiveFolder = Path.Combine(Folder, "Archivio");
            SnapshotPath = Path.Combine(Folder, "session-" + Guid.NewGuid().ToString("N") + ".frociart");
            var old = new List<string> { Path.Combine(DataFolder, "recupero.frociart"), Path.Combine(DataFolder, "recupero.flow") };
            if (isolated == null) old.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlowLab", "recupero.flow"));
            legacy = old.ToArray();
        }

        internal void EnsureLease()
        {
            if (disposed) throw new ObjectDisposedException("FlowRecoverySession");
            if (lease != null) return;
            EnsureDirectory(Folder);
            lease = new FileStream(SnapshotPath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        internal void WriteInfo(string title) { TryWriteInfo(SnapshotPath, title); }

        internal sealed class Candidate
        {
            internal string Path, Title;
            internal DateTime ChangedUtc;
            internal bool Archived, Legacy;
            internal string Label { get { return ShortTitle(Title) + " · " + ChangedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture); } }
            internal string Detail { get { return (Archived ? "Copia già recuperata. " : "Lavoro non salvato. ") + "Apri una copia modificabile; i file originali restano intatti."; } }
        }

        internal List<Candidate> Find(bool includeArchived)
        {
            var candidates = new List<Candidate>();
            foreach (string folder in includeArchived ? new[] { Folder, ArchiveFolder } : new[] { Folder })
            {
                if (!PlainDirectory(folder)) continue;
                foreach (string path in Directory.EnumerateFiles(folder, "session-*.frociart"))
                {
                    try
                    {
                        if (!ValidSlot(path) || String.Equals(path, SnapshotPath, StringComparison.OrdinalIgnoreCase) || !PlainFile(path) || !Available(path)) continue;
                        var info = new FileInfo(path);
                        candidates.Add(new Candidate { Path = path, Title = ReadInfo(path), ChangedUtc = info.LastWriteTimeUtc, Archived = String.Equals(folder, ArchiveFolder, StringComparison.OrdinalIgnoreCase) });
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                }
            }
            foreach (string path in legacy)
            {
                if (!PlainFile(path)) continue;
                string title = "Progetto precedente";
                try { title = DiagramIO.DecodeDocument(ReadBounded(path)).Title; }
                catch (IOException) { } catch (UnauthorizedAccessException) { } catch (System.Runtime.Serialization.SerializationException) { } catch (ArgumentException) { } catch (FlowException) { }
                candidates.Add(new Candidate { Path = path, Title = title, ChangedUtc = File.GetLastWriteTimeUtc(path), Legacy = true });
            }
            return candidates.OrderBy(c => c.Archived).ThenByDescending(c => c.ChangedUtc).ToList();
        }

        internal Claim Open(Candidate candidate)
        {
            if (candidate == null || !Allowed(candidate.Path)) throw new InvalidDataException("La copia di recupero non è valida.");
            return new Claim(this, candidate);
        }

        internal sealed class Claim : IDisposable
        {
            private readonly FlowRecoverySession owner;
            private readonly Candidate candidate;
            private Mutex gate;
            private FileStream sourceLease;
            private byte[] original;
            private bool held;

            internal Claim(FlowRecoverySession owner, Candidate candidate)
            {
                this.owner = owner; this.candidate = candidate;
                try
                {
                    gate = new Mutex(false, MutexName(candidate.Path));
                    try { held = gate.WaitOne(0); } catch (AbandonedMutexException) { held = true; }
                    if (!held) throw new IOException("Questa copia è usata da un'altra finestra. Riprova dopo averla chiusa.");
                    if (!candidate.Legacy) sourceLease = new FileStream(candidate.Path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    if (!owner.Allowed(candidate.Path) || !PlainFile(candidate.Path)) throw new IOException("La copia non è più disponibile. Riapri Recupera lavoro.");
                    original = ReadBounded(candidate.Path);
                }
                catch { Dispose(); throw; }
            }

            internal Diagram Document() { return DiagramIO.DecodeDocument(original); }

            // The caller must first save the recovered document to its own slot.
            // Archive instead of deleting: the prior bytes remain recoverable.
            internal void Archive()
            {
                if (candidate.Archived) return;
                if (!owner.Allowed(candidate.Path) || !ReadBounded(candidate.Path).SequenceEqual(original)) throw new IOException("La copia è cambiata. È stata aperta, ma non è stata archiviata.");
                EnsureDirectory(owner.Folder);EnsureDirectory(owner.ArchiveFolder);
                string target = Path.Combine(owner.ArchiveFolder, "session-" + Guid.NewGuid().ToString("N") + ".frociart");
                if (!Within(target, owner.ArchiveFolder)) throw new IOException("Destinazione di recupero non valida.");
                File.Move(candidate.Path, target);
                TryWriteInfo(target, candidate.Title);
                if (!candidate.Legacy) TryDelete(candidate.Path + ".info");
            }

            public void Dispose()
            {
                if (sourceLease != null) { sourceLease.Dispose(); sourceLease = null; }
                if (!candidate.Legacy && !File.Exists(candidate.Path)) TryDelete(candidate.Path + ".lock");
                if (gate != null) { if (held) gate.ReleaseMutex(); gate.Dispose(); gate = null; held = false; }
            }
        }

        internal static string MutexName(string path)
        {
            using (var hash = SHA256.Create()) return "Local\\Frociart.Recovery." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
        }

        internal static byte[] ReadBounded(string path)
        { return ReadBounded(path, DiagramIO.MaxFileBytes); }

        private static byte[] ReadBounded(string path, int limit)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length <= 0 || file.Length > limit) throw new InvalidDataException("La copia è vuota o supera il limite consentito.");
                byte[] bytes = new byte[(int)file.Length]; int offset = 0;
                while (offset < bytes.Length) { int count = file.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new InvalidDataException("La copia è incompleta."); offset += count; }
                return bytes;
            }
        }

        private bool Allowed(string path)
        {
            if (String.Equals(path, SnapshotPath, StringComparison.OrdinalIgnoreCase)) return false;
            if (legacy.Any(p => String.Equals(p, path, StringComparison.OrdinalIgnoreCase))) return true;
            return ValidSlot(path) && (Within(path, Folder) && PlainDirectory(Folder) || Within(path, ArchiveFolder) && PlainDirectory(ArchiveFolder));
        }
        private static bool Within(string path, string folder) { return String.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase); }
        private static bool ValidSlot(string path) { string name = Path.GetFileNameWithoutExtension(path); Guid id; return name.StartsWith("session-", StringComparison.Ordinal) && Guid.TryParseExact(name.Substring(8), "N", out id) && String.Equals(Path.GetExtension(path), ".frociart", StringComparison.OrdinalIgnoreCase); }
        private static bool PlainFile(string path) { return File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; }
        private static bool PlainDirectory(string path) { return Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0; }
        private static void EnsureDirectory(string path) { Directory.CreateDirectory(path); if (!PlainDirectory(path)) throw new IOException("La cartella di recupero non è una cartella locale regolare."); }
        private static bool Available(string path)
        {
            if (!File.Exists(path + ".lock")) return true;
            try { using (var file = new FileStream(path + ".lock", FileMode.Open, FileAccess.ReadWrite, FileShare.None)) return true; }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
        private static string ShortTitle(string title)
        {
            string text = (title ?? "Progetto").Replace('\r', ' ').Replace('\n', ' ').Trim(); if (text.Length == 0) text = "Progetto";
            int[] elements = StringInfo.ParseCombiningCharacters(text);
            return elements.Length <= 42 ? text : text.Substring(0, elements[42]) + "…";
        }
        private static string ReadInfo(string path)
        {
            string info = path + ".info";
            try
            {
                if (!PlainFile(info)) return "Progetto recuperabile";
                using(var reader=new StringReader(new UTF8Encoding(false,true).GetString(ReadBounded(info,4096)))) {
                    string header=reader.ReadLine(), title=reader.ReadLine();
                    if(header!="FROCIART_RECOVERY_1"||title==null||reader.ReadLine()!=null)return "Progetto recuperabile";
                    return new UTF8Encoding(false,true).GetString(Convert.FromBase64String(title));
                }
            }
            catch (IOException) { } catch (InvalidDataException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { } catch (FormatException) { }
            return "Progetto recuperabile";
        }
        private static void TryWriteInfo(string path, string title)
        {
            try { if(File.Exists(path+".info")&&!PlainFile(path+".info"))return;File.WriteAllText(path + ".info", "FROCIART_RECOVERY_1\n" + Convert.ToBase64String(Encoding.UTF8.GetBytes(title ?? "Progetto")), new UTF8Encoding(false)); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        private static void TryDelete(string path) { try { if (PlainFile(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (lease != null) { lease.Dispose(); lease = null; }
            if (!File.Exists(SnapshotPath)) { TryDelete(SnapshotPath + ".info"); TryDelete(SnapshotPath + ".lock"); }
        }
    }
}
