using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace JVP
{
    // All chapters for all videos, kept in one JSON file under %AppData%\JVP.
    // Videos are keyed by a hash of their contents so renaming or moving a file keeps its chapters.
    internal class ChapterStore
    {
        // Size plus the first and last 1 MB is enough to tell videos apart without reading the whole file
        private const int sampleSize = 1024 * 1024;

        private readonly string path;
        private StoreData data;

        public ChapterStore()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JVP");
            path = Path.Combine(folder, "chapters.json");
            load();
        }

        public static string keyFor(string videoPath)
        {
            using (FileStream fs = new FileStream(videoPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha = SHA256.Create())
            {
                long length = fs.Length;
                byte[] lengthBytes = BitConverter.GetBytes(length);
                sha.TransformBlock(lengthBytes, 0, lengthBytes.Length, null, 0);

                byte[] buffer = new byte[sampleSize];
                int read = readFully(fs, buffer);
                sha.TransformBlock(buffer, 0, read, null, 0);

                // Small files were already read in full by the first sample
                if (length > sampleSize * 2)
                {
                    fs.Seek(-sampleSize, SeekOrigin.End);
                    read = readFully(fs, buffer);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }
                else if (length > sampleSize)
                {
                    read = readFully(fs, buffer);
                    sha.TransformBlock(buffer, 0, read, null, 0);
                }

                sha.TransformFinalBlock(new byte[0], 0, 0);
                StringBuilder sb = new StringBuilder();
                foreach (byte b in sha.Hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public List<Chapter> getChapters(string key)
        {
            VideoEntry entry = find(key);
            List<Chapter> result = new List<Chapter>();
            if (entry == null) return result;
            foreach (StoredChapter c in entry.chapters)
            {
                result.Add(new Chapter(c.label, c.seconds));
            }
            return result;
        }

        // Replaces the stored chapters for a video and writes the file straight away
        public void setChapters(string key, string videoPath, List<Chapter> chapters)
        {
            VideoEntry entry = find(key);
            if (chapters.Count == 0)
            {
                if (entry != null) data.videos.Remove(entry);
            }
            else
            {
                if (entry == null)
                {
                    entry = new VideoEntry() { key = key };
                    data.videos.Add(entry);
                }
                // Not used for lookup; just makes the JSON readable
                entry.fileName = Path.GetFileName(videoPath);
                entry.chapters = new List<StoredChapter>();
                foreach (Chapter c in chapters)
                {
                    entry.chapters.Add(new StoredChapter() { label = c.label, seconds = c.value });
                }
            }
            save();
        }

        private VideoEntry find(string key)
        {
            foreach (VideoEntry entry in data.videos)
            {
                if (entry.key == key) return entry;
            }
            return null;
        }

        private void load()
        {
            data = new StoreData();
            if (!File.Exists(path)) return;
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    data = (StoreData)new DataContractJsonSerializer(typeof(StoreData)).ReadObject(fs);
                }
                if (data.videos == null) data.videos = new List<VideoEntry>();
                foreach (VideoEntry entry in data.videos)
                {
                    if (entry.chapters == null) entry.chapters = new List<StoredChapter>();
                }
            }
            catch (Exception ex) when (ex is SerializationException || ex is IOException)
            {
                // Keep the unreadable file rather than overwriting it on the next save
                string backup = path + ".bad";
                File.Copy(path, backup, true);
                data = new StoreData();
                System.Windows.Forms.MessageBox.Show($"The chapters file could not be read and was copied to {backup}. Starting with no chapters.", "Chapters");
            }
        }

        private void save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            // Write to a temp file first so a crash mid-save can't leave a half-written chapters.json
            string temp = path + ".tmp";
            using (FileStream fs = File.Create(temp))
            // Indented so the file is easy to read and hand-edit
            using (System.Xml.XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(fs, Encoding.UTF8, false, true, "  "))
            {
                new DataContractJsonSerializer(typeof(StoreData)).WriteObject(writer, data);
            }
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }

        private static int readFully(Stream stream, byte[] buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer, total, buffer.Length - total);
                if (n == 0) break;
                total += n;
            }
            return total;
        }

        [DataContract]
        private class StoreData
        {
            [DataMember(Order = 0)]
            public List<VideoEntry> videos = new List<VideoEntry>();
        }

        [DataContract]
        private class VideoEntry
        {
            [DataMember(Order = 0)]
            public string key;

            [DataMember(Order = 1)]
            public string fileName;

            [DataMember(Order = 2)]
            public List<StoredChapter> chapters = new List<StoredChapter>();
        }

        [DataContract]
        private class StoredChapter
        {
            [DataMember(Order = 0)]
            public string label;

            [DataMember(Order = 1)]
            public int seconds;
        }
    }
}
