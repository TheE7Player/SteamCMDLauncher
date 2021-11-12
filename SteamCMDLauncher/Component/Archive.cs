using ICSharpCode.SharpZipLib.Zip;
using System;
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

        // If the intention is for reading the archive
        private bool IsReadOnly { get; set; }
        
        private bool IsLoaded;

        private bool IsSafed;

        private ArchiveData self_data;

        private const int COMPRESS_LEVEL = 9;

        /// <summary>
        /// TMP folder for extracting or reading the files
        /// </summary>
        public static string CACHE_PATH = Path.Combine(Path.GetTempPath(), "TheE7Player", "SteamCMDLauncher", "temp");

        public const string DEFAULT_EXTENTION_CFG = ".smdcg";
        public const string DEFAULT_EXTENTION_SETTING = ".smds";

        private string META_DATA_FILE = ".metadata";

        /// <summary>
        /// Create a new archive to save or load configurations
        /// </summary>
        /// <param name="file_location">The archive's current location</param>
        /// <param name="appid">The AppID the config is for</param>
        /// <param name="performLoad">If after the constructor is finished, to auto-call Load() method</param>
        public Archive(string file_location, string appid, bool performLoad = false)
        {
            // Set the default flags
            IsLoaded = false;
            IsSafed = true;

            // Create the new archive data struct
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

            // Check if this may be the first time this archive has been created
            if (!File.Exists(file_location))
            { 
                this.self_data.Revisions = 0;
            }
            
            // If not and auto-call for loading is enabled, load the archive
            if( performLoad ) LoadFile();
            
            // Clear the strings as we don't need them anymore
            file_location = null; appid = null;
        }
       
        // View the archive in read-only mode
        public Archive(string file_location)
        {
            IsLoaded = true;
            
            // Create the new archive data struct
            this.self_data = new ArchiveData();
            this.self_data.locationReference = file_location;
            
            IsReadOnly = true;
            
            LoadFile();
        }

        ~Archive() { ForceClear(); }
      
        /// <summary>
        /// Deletes the temp folder if it exists
        /// </summary>
        private void Clear()
        {
            // If the tmp folder exists, delete it recursively.
            if (Directory.Exists(self_data.tempReference))
                Directory.Delete(self_data.tempReference, true);
        }

        /// <summary>
        /// Force clear-up the items
        /// </summary>
        public void ForceClear()
        {
            if(!IsReadOnly)
            { 
                // If the 'IsSafed' flag isn't true, force the save to prevent data loss
                if (!IsSafed) SaveFile();

                // If the 'Cleanup' flag is true and 'IsLoaded' flag is true, then start to destroy unmanaged objects
                if (Cleanup && IsLoaded)
                {
                    META_DATA_FILE = null;
                    Clear();
                }
            }
        }

        /// <summary>
        /// Creates the byte array for the '.metadata' file
        /// </summary>
        private byte[] CreateMetaDataFile()
        {
            // Create an byte array for the data
            byte[] output = null;
          
            // Set the last read to the current time
            this.self_data.LastRead = DateTime.UtcNow;

            // Create a memorystream buffer
            using (MemoryStream stringBuffer = new MemoryStream())
            {
                // Create a streamwriter buffer to write to the memorystream in unicode
                using (StreamWriter sW = new StreamWriter(stringBuffer, UnicodeEncoding.Unicode))
                {
                    // Append the metadata required
                    sW.WriteLine($"DATE={this.self_data.LastRead}");
                    sW.WriteLine($"GAME={this.self_data.GameID}");
                    sW.WriteLine($"REVI={this.self_data.Revisions}");
                    
                    // Tell the buffer we're finish, and push it to the main stream and clear
                    sW.Flush();
                    
                    // Reset the memorystream position back to zero
                    stringBuffer.Position = 0;
                    
                    // Finally, store the input of the stream to the string 'output'
                    output = stringBuffer.ToArray();
                }
            }

            // Then return the stream back to the caller
            return output;
        }

        /// <summary>
        /// Saves the temp folder into the archive
        /// </summary>
        public void SaveFile()
        {
            // If the tmp file doesn't exist already, do so.
            if (!Directory.Exists(this.self_data.tempReference))
            {
                Config.Log($"[AD] Creating ARCHIVE TMP folder: {this.self_data.tempReference}");
                Directory.CreateDirectory(this.self_data.tempReference);
            }

            // Increament the revisions number
            this.self_data.Revisions++;

            // Crate the required streams necessary
            MemoryStream outputMemStream = new MemoryStream();
            ZipOutputStream zipStream = new ZipOutputStream(outputMemStream);

            // Set the compression level to the highest possible compression
            zipStream.SetLevel(COMPRESS_LEVEL);

            // Create an byte array for the stream
            byte[] bytes = null;

            // Create the metadata file first

            // Set the current process date to current time/date
            DateTime dateProcessed = DateTime.UtcNow;

            // Create a new entry for the archive
            ZipEntry metadata_entry = new ZipEntry(META_DATA_FILE)
            {
                DateTime = dateProcessed
            };

            // Push the entry onto the stream
            zipStream.PutNextEntry(metadata_entry);

            // Generate the '.metadata' file
            bytes = CreateMetaDataFile();

            // Write the file onto the archive buffer
            zipStream.Write(bytes, 0, bytes.Length);
           
            // Create a string for realitive pathing for the files
            string realitive = string.Empty;

            // Now we do each other file stored in temp
            foreach (string file in Directory.GetFiles(self_data.tempReference, "*.*", SearchOption.AllDirectories))
            {
                // Ignore if its a metadata file, as this is already included
                if (Path.GetFileName(file) == ".metadata") continue;

                // Get the relative path based on the current tmp folder is reading from
                realitive = Path.GetRelativePath(self_data.tempReference, file);
                
                // Create the new file entry from the current file being processed
                metadata_entry = new ZipEntry(realitive) { DateTime = dateProcessed };
                
                // Push the file into the archive
                zipStream.PutNextEntry(metadata_entry);

                // Get the current file bytes onto the array
                bytes = File.ReadAllBytes(file);

                // Write the file bytes into the stream
                zipStream.Write(bytes, 0, bytes.Length);
            }
            
            // Close the stream as we're now finished saving everything
            zipStream.IsStreamOwner = false;
            zipStream.Close();
            
            // Clear away any unmanaged objects
            realitive = null;
            metadata_entry = null;
            bytes = null;

            // Write the file by using the bytes from the memory stream
            System.IO.File.WriteAllBytes(this.self_data.locationReference, outputMemStream.GetBuffer());
           
            // Set the flag true as the archive has been saved
            IsSafed = true;
        }

        /// <summary>
        /// Loads the archive contents into a temp folder
        /// </summary>
        public void LoadFile()
        {
            // If the tmp folder doesn't exist, do so
            if(!Directory.Exists(this.self_data.tempReference) && !IsReadOnly)
            {
                Config.Log($"[AD] Creating ARCHIVE TMP folder: {this.self_data.tempReference}");
                Directory.CreateDirectory(this.self_data.tempReference);
            }

            // What if the load is for a new file to be created?
            if(!File.Exists(this.self_data.locationReference) && !IsReadOnly) { IsLoaded = true; return; }

            // Create a disposable FileStream to read the archive
            using (FileStream fs = new FileStream(this.self_data.locationReference, FileMode.Open, FileAccess.Read))
            {
                // Create a disposable ZipFile, each will read each file
                using (ZipFile zf = new ZipFile(fs))
                {
                    // Setup a string to hold the file contents
                    string fi = string.Empty;

                    // For each entry found...
                    foreach (ZipEntry ze in zf)
                    {
                        // Create a stream from the current file being processed
                        using Stream s = zf.GetInputStream(ze);
                        
                        // Create a stream reader to read the current stream
                        using StreamReader sr = new StreamReader(s);
                        
                        // Push the stream into a string and store it into 'fi'
                        fi = sr.ReadToEnd();
                        
                        // If the current file is the metadata file (.metadata)
                        if (ze.Name == META_DATA_FILE)
                        {
                            // Create an string array with the newline splitter, exclude any empty entries if any
                            string[] eh = fi.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

                            // Create a string which holds the trimmed string to compare
                            string outp;

                            // For each element possible
                            for (int i = 0; i < eh.Length; i++)
                            {
                                // Trim to get the value only
                                outp = eh[i][5..].Trim();

                                // If its the last read date..
                                if (eh[i].StartsWith("DATE"))
                                {
                                    this.self_data.LastRead = DateTime.Parse(outp);
                                }

                                // If its game appid...
                                if(eh[i].StartsWith("GAME"))
                                {
                                    this.self_data.GameID = outp;
                                }

                                // If its how many revisions made during its lifespan
                                if (eh[i].StartsWith("REVI"))
                                {
                                    this.self_data.Revisions = Convert.ToInt32(outp);
                                }
                            }

                            outp = null;
                            eh = null;

                            if (IsReadOnly) break;
                        }
                        if (IsReadOnly) break;
                        // Then cache the file location where this file will be created in temp
                        string file = Path.Combine(this.self_data.tempReference, ze.Name);

                        // Get the current folder path where this file will be stored
                        string folder = Path.GetDirectoryName(file);

                        // If the current folder the tmp will be under doesn't exist, create it
                        if (!Directory.Exists(folder))
                        {
                            Directory.CreateDirectory(folder);
                        }

                        // Then finally write to the contents of the file
                        File.WriteAllText(file, fi);

                        // Then clean up any unmanaged objects
                        file = null;
                        folder = null;
                        fi = null;
                    }
                }
            }

            // Then set the 'IsLoaded' flag to true
            IsLoaded = true;
        }

        /// <summary>
        /// Iterate through the archive without caching
        /// </summary>
        /// <returns>A typle where T1 is the file name, T2 being the contents</returns>
        public IEnumerable<(string, string)> GetFiles()
        {
            // Check if its possible to read the archive in the first place
            if (!File.Exists(this.self_data.locationReference)) 
                yield break;

            using (FileStream fs = new FileStream(this.self_data.locationReference, FileMode.Open, FileAccess.Read))
            {
                // Create a disposable ZipFile, each will read each file
                using (ZipFile zf = new ZipFile(fs))
                {
                    // Setup a string to hold the file contents
                    string fi = string.Empty;

                    // For each entry found...
                    foreach (ZipEntry ze in zf)
                    {
                        // Create a stream from the current file being processed
                        using Stream s = zf.GetInputStream(ze);

                        // Create a stream reader to read the current stream
                        using StreamReader sr = new StreamReader(s);

                        // Push the stream into a string and store it into 'fi'
                        fi = sr.ReadToEnd();

                        yield return (ze.Name, fi);
                    }
                }
            }
        }

        /// <summary>
        /// A type which returns a result based on if it was able to read the tmp file required
        /// </summary>
        /// <param name="target">The internal file to attempt to access</param>
        /// <returns>A tuple, T1 being the result - T2 being the error reason or content of the file</returns>
        public (int, string) GetFileContents(string target)
        {
            // A string variable which holds the tmp file location
            string temp_contents = string.Empty;
            
            try
            {
                // If the archive ain't loaded and the user is tempting to get a file from it
                if (!IsLoaded) return (-1, "No archive was loaded to read from a file.");

                // If the current temp folder doesn't exist
                if(!Directory.Exists(self_data.tempReference)) return (-1, "The requested temp file folder does not exist");

                // Create the cached path of where the temp file should be located at
                temp_contents = Path.Combine(self_data.tempReference, target);

                // If this file doesn't exist in the temp folder...
                if (!File.Exists(temp_contents)) return (-1, "The requested temp file folder does not exist");

                // Finally, return the string of the contents of the found file
                return (0, File.ReadAllText(temp_contents));
            }
            finally
            {
                // Clean up any unmanaged objects
                target = null;
                temp_contents = null;
            }        
        }
    
        /// <summary>
        /// Sets a file (with folder support) in the temp, to be archived onced saved.
        /// </summary>
        /// <param name="target">The relative path of the file (> to create folder path)</param>
        /// <param name="contents">The contents for this line</param>
        /// <returns>A tuple where T1 is the result, and T2 being an error message if any</returns>
        /// <example>
        /// Example with Target: Make a new folder called 'NewFolder' with a file named 'NewFile.txt'
        /// <code>
        /// >NewFolder>NewFile.txt
        /// </code>
        /// </example>
        public (bool, string) SetFileContents(string target, string contents)
        {
            // A string to hold the temp location file
            string temp_contents = string.Empty;          

            // Set 'IsSafed' to false as we are appending a new or overwritten item
            IsSafed = false;

            // If the tmp folder doesn't exist, do so
            if (!Directory.Exists(this.self_data.tempReference))
            {
                Config.Log($"[AD] Creating ARCHIVE TMP folder: {this.self_data.tempReference}");
                Directory.CreateDirectory(this.self_data.tempReference);
            }

            try
            {
                // If the archive isn't loaded yet
                if (!IsLoaded) return (false, "No archive was loaded to read from a file.");

                // If the temp folder for this archive isn't created yet
                if (!Directory.Exists(self_data.tempReference)) return (false, "The requested temp file folder does not exist");

                // Now we process the query, Folder depth is denoted by '>'
                if(target.Contains('>'))
                {                  
                    // Create an string array based on the folders to create
                    string[] structure = target.Trim().Split('>');

                    // This variable will hold the current folder to create
                    string path = self_data.tempReference;

                    // This holds a pointer to keep its position tracked
                    int idx = 0;

                    // This holds a pointer to where we can stop creating folders.
                    int depth = structure.Length - 1;

                    // While we haven't reach the last element
                    while (idx != depth)
                    {
                        // Store the current folder to create
                        // Increment the tracking pointer                    
                        path = Path.Combine(path, structure[idx++]);
                    }
                    
                    // If the current folder doesn't exist yet, do so.
                    if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                    
                    // Finally append the file to the end of the path             
                    // Then store the absolute path for the temp folder
                    temp_contents = Path.Combine(path, structure[idx]);
                    
                    // Then clear unmanaged objects
                    structure = null;
                    path = null;
                }
                else
                {
                    // We are not processing a folder query, so we can just amend the temp path
                    temp_contents = Path.Combine(self_data.tempReference, target);
                }

                // Then we write to the temp file
                File.WriteAllText(temp_contents, contents);

                // Then return true as we successfully made the file, without any errors
                return (true, string.Empty);
            }
            finally
            {
                // Clear unmanaged objects
                target = null;
                contents = null;
                temp_contents = null;
            }
        }

        /// <summary>
        /// Property to get the current archive details
        /// </summary>
        public ArchiveData GetArchiveDetails => self_data;
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
