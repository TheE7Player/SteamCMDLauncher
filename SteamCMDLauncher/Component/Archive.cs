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
        private ArchiveData self_data;

        private const int COMPRESS_LEVEL = 9;
        public static string CACHE_PATH = Path.Combine(Path.GetTempPath(), "TheE7Player", "SteamCMDLauncher", "temp");

        private string DEFAULT_EXTENTION_CFG = ".smdcg";
        private string DEFAULT_EXTENTION_SETTING = ".smds";
        private string META_DATA_FILE = ".metadata";
        private string LOCALIZATION_PATH = "/lang/";

        ~Archive()
        {
            DEFAULT_EXTENTION_CFG = null;
            DEFAULT_EXTENTION_SETTING = null;
            META_DATA_FILE = null;
            LOCALIZATION_PATH = null;
        }

        public Archive(string file_location, string appid, bool performLoad = false)
        {
            this.self_data = new ArchiveData();
            this.self_data.locationReference = file_location;
            this.self_data.GameID = appid;

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

        //Dictionary<string, string[]> colection
        public void SaveFile()
        {
            this.self_data.Revisions++;

            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            zipStream.SetLevel(COMPRESS_LEVEL);

            byte[] bytes = null;

            // Create the metadata file first
            var metadata_entry = new ZipEntry(META_DATA_FILE);
            metadata_entry.DateTime = DateTime.Now;

            zipStream.PutNextEntry(metadata_entry);

            bytes = CreateMetaDataFile();

            zipStream.Write(bytes, 0, bytes.Length);

            zipStream.IsStreamOwner = false;
            zipStream.Close();

            System.IO.File.WriteAllBytes(this.self_data.locationReference, outputMemStream.GetBuffer());
        }

        public void LoadFile()
        {
            if (!Directory.Exists(CACHE_PATH)) 
            {
                Config.Log($"[AD] Creating TMP folder in: {CACHE_PATH}");
                Directory.CreateDirectory(CACHE_PATH); 
            }

            string zip_path_folder = Path.Combine(CACHE_PATH, Path.GetFileNameWithoutExtension(this.self_data.locationReference));

            if(Directory.Exists(zip_path_folder))
            {
                Directory.Delete(zip_path_folder, true);
            }

            Directory.CreateDirectory(zip_path_folder);

            this.self_data.tempReference = zip_path_folder;

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

                        File.WriteAllText(Path.Combine(zip_path_folder, ze.Name), fi);
                        
                        fi = null;
                    }
                }
            }
           
           // return this.self_data;
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
