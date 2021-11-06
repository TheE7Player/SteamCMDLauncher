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
        private string app_id;
        private string output_path;
        private int revisions = 1;
        private const int COMPRESS_LEVEL = 9;

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

        public Archive(string file_location, string appid)
        {
            this.app_id = appid;
            this.output_path = file_location;

            file_location = null; appid = null;
        }

        private byte[] CreateMetaDataFile()
        {
            byte[] output = null;
            using (MemoryStream stringBuffer = new MemoryStream())
            {
                using (StreamWriter sW = new StreamWriter(stringBuffer, UnicodeEncoding.Unicode))
                {
                    sW.WriteLine($"DATE={DateTime.UtcNow}");
                    sW.WriteLine($"GAME={app_id}");
                    sW.WriteLine($"REVI={revisions}");
                    sW.Flush();
                    stringBuffer.Position = 0;
                    output = stringBuffer.ToArray();
                }              
            }

            return output;
        }

        //Dictionary<string, string[]> colection
        public void SaveFile(string fileloc)
        {
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

            System.IO.File.WriteAllBytes(fileloc, outputMemStream.GetBuffer());
        }

        public ArchiveData LoadFile(string fileloc)
        {
            ArchiveData self = new ArchiveData();

            using (var fs = new FileStream(fileloc, FileMode.Open, FileAccess.Read))
            {
                using (var zf = new ZipFile(fs))
                {
                    foreach (ZipEntry ze in zf)
                    {
                        if (ze.IsDirectory)
                        {
                            self.Folders++;
                            continue; 
                        }
                        
                        self.Files++;

                        if (ze.Name == META_DATA_FILE)
                        {
                            using Stream s = zf.GetInputStream(ze);
                            using StreamReader sr = new StreamReader(s);
                            string[] eh = sr.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

                            string outp;
                            for (int i = 0; i < eh.Length; i++)
                            {
                                outp = eh[i][5..].Trim();

                                if (eh[i].StartsWith("DATE"))
                                {
                                    self.LastRead = DateTime.Parse(outp);
                                }

                                if(eh[i].StartsWith("GAME"))
                                {
                                    self.GameID = outp;
                                }

                                if (eh[i].StartsWith("REVI"))
                                {
                                    self.Revisions = Convert.ToInt32(outp);
                                }
                            }
                        }
                    }
                }
            }

            return self;
        }
    }

    public struct ArchiveData
    {
        public string GameID;
        public DateTime LastRead;
        public int Revisions;

        public string[] Translations;

        public int Files;
        public int Folders;
    }
}
