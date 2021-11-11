using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SteamCMDLauncher.Component
{
    public class Archive
    {
        /// <summary>
        /// Instructs the class if its good to clean up the data
        /// </summary>
        public bool Cleanup { get; set; }

        private bool IsLoaded;

        private ArchiveData self_data;

        private const int COMPRESS_LEVEL = 9;

        /// <summary>
        /// TMP folder for extracting or reading the files
        /// </summary>
        public static string CACHE_PATH = Path.Combine(Path.GetTempPath(), "TheE7Player", "SteamCMDLauncher", "temp");

        private string DEFAULT_EXTENTION_CFG = ".smdcg",
        DEFAULT_EXTENTION_SETTING = ".smds",
        META_DATA_FILE = ".metadata",
        LOCALIZATION_PATH = "/lang/";

        ~Archive()
        {
            ForceClear();
        }

        public Archive(string file_location, string appid, bool performLoad = false)
        {
            IsLoaded = false;

            this.self_data = new ArchiveData();
            this.self_data.locationReference = file_location;
            this.self_data.GameID = appid;
            this.self_data.tempReference = Path.Combine(CACHE_PATH, Path.GetFileNameWithoutExtension(this.self_data.locationReference));

            // Create the tmp folder if it doesn't exist already
            if (!Directory.Exists(CACHE_PATH))
            {
                Config.Log($"[AD] Creating TMP folder in: {CACHE_PATH}");
                Directory.CreateDirectory(CACHE_PATH);
            }

            // Clean up any old file (if any)
            if (Directory.Exists(this.self_data.tempReference))
            {
                Directory.Delete(this.self_data.tempReference, true);
            }

            if (!File.Exists(file_location))
            { 
                this.self_data.Revisions = 0;
            }
            else
            {
                if( performLoad ) LoadFile();
            }

            file_location = null; appid = null;
        }
        
        private void Clear()
        {
            if (Directory.Exists(self_data.tempReference))
                Directory.Delete(self_data.tempReference, true);
        }

        public void ForceClear()
        {
            Console.WriteLine("AAAAAAAAAAAA");

            DEFAULT_EXTENTION_CFG = null;
            DEFAULT_EXTENTION_SETTING = null;
            META_DATA_FILE = null;
            LOCALIZATION_PATH = null;

            if (Cleanup && IsLoaded)
            {
                Console.WriteLine("AAAAAAAAAAAA x2");
                Clear();
            }
        }

        [Obsolete("Maybe remove if this method has no purpose")]
        private void InitialseConstants()
        {
             DEFAULT_EXTENTION_CFG = ".smdcg";
             DEFAULT_EXTENTION_SETTING = ".smds";
             META_DATA_FILE = ".metadata";
             LOCALIZATION_PATH = "/lang/";   
        }

        private byte[] CreateMetaDataFile()
        {
            byte[] output = null;
          
            this.self_data.LastRead = DateTime.UtcNow;

            using (MemoryStream stringBuffer = new MemoryStream())
            {
                using (StreamWriter sW = new StreamWriter(stringBuffer, UnicodeEncoding.Unicode))
                {
                    sW.WriteLine($"DATE={this.self_data.LastRead}");
                    sW.WriteLine($"GAME={this.self_data.GameID}");
                    sW.WriteLine($"REVI={this.self_data.Revisions}");
                    sW.Flush();
                    stringBuffer.Position = 0;
                    output = stringBuffer.ToArray();
                }
            }

            return output;
        }

        public void SaveFile()
        {
            if (!Directory.Exists(this.self_data.tempReference))
            {
                Config.Log($"[AD] Creating ARCHIVE TMP folder: {this.self_data.tempReference}");
                Directory.CreateDirectory(this.self_data.tempReference);
            }

            this.self_data.Revisions++;

            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(COMPRESS_LEVEL);

            byte[] bytes = null;

            // Create the metadata file first

            DateTime dateProcessed = DateTime.Now;

            ZipEntry metadata_entry = new ZipEntry(META_DATA_FILE);
            metadata_entry.DateTime = dateProcessed;

            zipStream.PutNextEntry(metadata_entry);

            bytes = CreateMetaDataFile();

            zipStream.Write(bytes, 0, bytes.Length);
           
            string realitive = string.Empty;

            // Now we do each other file stored in temp
            foreach (string file in Directory.GetFiles(self_data.tempReference, "*.*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file) == ".metadata") continue;

                realitive = Path.GetRelativePath(self_data.tempReference, file);
                
                metadata_entry = new ZipEntry(realitive);
                metadata_entry.DateTime = dateProcessed;
                
                zipStream.PutNextEntry(metadata_entry);

                bytes = File.ReadAllBytes(file);
                zipStream.Write(bytes, 0, bytes.Length);
            }
            
            zipStream.IsStreamOwner = false;
            zipStream.Close();
            realitive = null;
            System.IO.File.WriteAllBytes(this.self_data.locationReference, outputMemStream.GetBuffer());
        }

        public void LoadFile()
        {

            if(!Directory.Exists(this.self_data.tempReference))
            {
                Config.Log($"[AD] Creating ARCHIVE TMP folder: {this.self_data.tempReference}");
                Directory.CreateDirectory(this.self_data.tempReference);
            }

            using (var fs = new FileStream(this.self_data.locationReference, FileMode.Open, FileAccess.Read))
            {
                using (var zf = new ZipFile(fs))
                {
                    string fi = string.Empty;
                    foreach (ZipEntry ze in zf)
                    {
                        /*if (ze.IsDirectory)
                        {
                            continue; 
                        }*/
                        using Stream s = zf.GetInputStream(ze);
                        using StreamReader sr = new StreamReader(s);
                        fi = sr.ReadToEnd();
                        
                        if (ze.Name == META_DATA_FILE)
                        {
                            string[] eh = fi.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

                            string outp;

                            for (int i = 0; i < eh.Length; i++)
                            {
                                outp = eh[i][5..].Trim();

                                if (eh[i].StartsWith("DATE"))
                                {
                                    this.self_data.LastRead = DateTime.Parse(outp);
                                }

                                if(eh[i].StartsWith("GAME"))
                                {
                                    this.self_data.GameID = outp;
                                }

                                if (eh[i].StartsWith("REVI"))
                                {
                                    this.self_data.Revisions = Convert.ToInt32(outp);
                                }
                            }                
                        }

                        File.WriteAllText(Path.Combine(this.self_data.tempReference, ze.Name), fi);
                        
                        fi = null;
                    }
                }
            }

            IsLoaded = true;
        }

        /// <summary>
        /// A type which returns a result based on if it was able to read the tmp file required
        /// </summary>
        /// <param name="target">The internal file to attempt to access</param>
        /// <returns>A tuple, T1 being the result - T2 being the error reason or content of the file</returns>
        public (int, string) GetFileContents(string target)
        {
            string temp_contents = string.Empty;
            try
            {
                if (!IsLoaded) 
                    return (-1, "No archive was loaded to read from a file.");

                if(!Directory.Exists(self_data.tempReference))
                    return (-1, "The requested temp file folder does not exist");

                temp_contents = Path.Combine(self_data.tempReference, target);

                if (!File.Exists(temp_contents))
                    return (-1, "The requested temp file folder does not exist");

                return (0, File.ReadAllText(temp_contents));

            }
            finally
            {
                target = null;
                temp_contents = null;
            }        
        }
    
        public (bool, string) SetFileContents(string target, string contents)
        {
            string temp_contents = string.Empty;

            try
            {
                if (!IsLoaded)
                    return (false, "No archive was loaded to read from a file.");

                if (!Directory.Exists(self_data.tempReference))
                    return (false, "The requested temp file folder does not exist");

                // Now we process the query, Folder depth is denoted by '>'
                if(target.Contains('>'))
                {
                    StringBuilder sb = new StringBuilder();
                    string[] structure = target.Trim().Split('>');

                    string path = string.Empty;
                    int idx = 0;
                    int depth = structure.Length - 1;

                    while (idx != depth)
                    {
                        path = Path.Combine(self_data.tempReference, structure[idx]);

                        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                        
                        sb.Append($"{structure[idx]}/");

                        idx++;
                    }
                    sb.Append(structure[idx]);
                    temp_contents = Path.Combine(self_data.tempReference, sb.ToString());
                    structure = null;
                    sb = null;
                    path = null;
                }
                else
                {
                    temp_contents = Path.Combine(self_data.tempReference, target);
                }

                File.WriteAllText(temp_contents, contents);

                return (true, string.Empty);

            }
            finally
            {
                target = null;
                contents = null;
                temp_contents = null;
            }
        }
    }

    public struct ArchiveData
    {
        public DateTime LastRead;
        
        public int Revisions;
        
        public string GameID;
        public string tempReference;
        public string locationReference;
    }
}
