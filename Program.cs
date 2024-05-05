﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Windows.Forms;
using System.Collections.Generic;
#pragma warning disable CS1591
#pragma warning disable CS1587


/// <summary> A Small Library For Building .gp4 Files Used In The PS4 .pkg Creation Process, And Reading Info From Already Created Ones
///</summary>
namespace libgp4 { // ver 1.26.100

    // GP4Reader Class Start
    /// <summary>
    /// Small Class For Reading Data From .gp4 Projects.<br/><br/>
    /// Usage:<br/>
    ///  1. Create A New Instance To Parse And Return All Relevant Data From The .gp4 File.<br/>
    ///  (All Variables Are Read During Class Intialization, And The File Handle For The .gp4 Disposed Before Returning)<br/><br/>
    ///  2. Alternatively, Use The A Static Method To Read A Specific Attribute From The .gp4, Rather Than Reading Them All To Grab 2 Things. <br/>
    ///  (Everything But The Expected Node(s) Is/Are Skipped, And The File Handle For The .gp4 Disposed Before Returning)
    ///</summary>
    public class GP4Reader {

        /// <summary>
        ///  Create A New Instance Of The GP4Reader Class With A Given .gp4 File.<br/><br/>
        ///  Skips Passed The First Line To Avoid A Version Conflict (The XmlReader Class Doesn't Like 1.1),<br/>
        ///  Then Parses The Given Project File For All Relevant .gp4 Data. Also Checks For Possible Errors.
        /// </summary>
        /// <param name="GP4Path"> The Absolute Path To The .gp4 Project File </param>
        /// Determines Whether Or Not To Throw An Assertion If An Error Is Found In The .gp4 File,<br/>
        /// Or Only When The Integrity Is Checked Through VerifyGP4Integrity()
        /// </param>
        public GP4Reader(string GP4Path) {
            using(var gp4_file = new StreamReader(GP4Path)) {
                gp4_file.ReadLine();                  // Skip First Line To Avoid A Version Conflict
                ParseGP4(XmlReader.Create(gp4_file)); // Read All Data Someone Might Want To Grab From The .gp4 For Whatevr Reason
                
                gp4_path = GP4Path;
            }
        }
        
        /// <summary>
        ///  Small Struct For Scenario Node Attributes.
        ///  <br/><br/>
        ///  Members:
        ///  <br/> [string] Type
        ///  <br/> [string] Label
        ///  <br/> [int] Id
        ///  <br/> [int] InitialChunkCount
        ///  <br/> [string] ChunkRange
        ///</summary>
        public struct Scenario {
            // TODO: TRY TO ADD MORE DESCRIPTIVE SUMMARIES
            public Scenario(XmlReader gp4Stream) {
                Type = gp4Stream.GetAttribute("type");
                Label = gp4Stream.GetAttribute("label");
                Id = int.Parse(gp4Stream.GetAttribute("id"));
                InitialChunkCount = int.Parse(gp4Stream.GetAttribute("initial_chunk_count"));
                ChunkRange = gp4Stream.ReadInnerXml();
            }

            /// <summary>
            ///  The Type Of The Selected Game Scenario. (E.G. sp / mp)
            /// </summary>
            public string Type;

            /// <summary>
            ///  The Label/Name Of The Selected Game Scenario.
            /// </summary>
            public string Label;

            /// <summary>
            ///  Id Of The Selected Game Scenario.
            /// </summary>
            public int Id;

            /// <summary>
            ///  The Initial Chunk Count Of The Selected Game Scenario.
            /// </summary>
            public int InitialChunkCount;

            ///  <summary>
            ///  The Chunk Range For The Selected Game Scenario.
            ///  <br/><br/>
            ///  NOTE: No Idea If My Own Tool Creates This Attribute Properly,
            ///  <br/>But If It Doesn't, It Won't Matter Unless You're Trying To Burn The Created .pkg To A Disc, Anyway
            ///  </summary>
            public string ChunkRange;
        }


        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Internal Variables / Methods     --\\\
        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\
        #region Internal Variables / Methods

        /// <summary> Files That Aren't Meant To Be Added To A .pkg.
        ///</summary>
        private readonly string[] project_file_blacklist = new string[] {
                  // Drunk Canadian Guy
                    "right.sprx",
                    "sce_discmap.plt",
                    "sce_discmap_patch.plt",
                    @"sce_sys\playgo-chunk",
                    @"sce_sys\psreserved.dat",
                    @"sce_sys\playgo-manifest.xml",
                    @"sce_sys\origin-deltainfo.dat",
                  // Al Azif
                    @"sce_sys\.metas",
                    @"sce_sys\.digests",
                    @"sce_sys\.image_key",
                    @"sce_sys\license.dat",
                    @"sce_sys\.entry_keys",
                    @"sce_sys\.entry_names",
                    @"sce_sys\license.info",
                    @"sce_sys\selfinfo.dat",
                    @"sce_sys\imageinfo.dat",
                    @"sce_sys\.unknown_0x21",
                    @"sce_sys\.unknown_0xC0",
                    @"sce_sys\pubtoolinfo.dat",
                    @"sce_sys\app\playgo-chunk",
                    @"sce_sys\.general_digests",
                    @"sce_sys\target-deltainfo.dat",
                    @"sce_sys\app\playgo-manifest.xml"
        };

        /// <summary> Default Assertion Message Text For Formatting. </summary>
        private static readonly string assertion_base = $"An Error Occured When Reading Attribute From The Following Node: $|$\nMessage: %|%";

        /// <summary> Catch DLog Errors, Disabling Whichever Output Threw The Error.
        ///</summary>
        private static readonly bool[] enable_output_channel = new bool[] { true, true };

        /// <summary> Backup Of The GP4's File Path For Various Methods
        ///</summary>
        private readonly string gp4_path;

        /// <summary> The Content Id In The Named File. (Redundancy To Hopefully Catch A Mismatched Id)
        ///</summary>
        private string sfo_content_id, playgo_content_id;




        /// <summary> Console Logging Method. </summary>
        private static void DLog(object o) {
#if DEBUG
            if(enable_output_channel[0])
                try { Debug.WriteLine("#libgp4.dll: " + o); }
                catch(Exception) { enable_output_channel[0] = false; }

            if(!Console.IsOutputRedirected && enable_output_channel[1]) // Avoid Duplicate Writes
                try { Console.WriteLine("#libgp4.dll: " + o); }
                catch(Exception) { enable_output_channel[1] = false; }
#endif
        }

        /// <summary> Error Logging Method. </summary>
        private static void ELog(string NodeName, string Error) {
            var Message = (assertion_base.Replace("$|$", NodeName)).Replace("%|%", Error);

            try { Debug.WriteLine("libgp4.dll: " + Message); }
            catch(Exception) { enable_output_channel[0] = false; }

            if(!Console.IsOutputRedirected) // Avoid Duplicate Writes
                try { Console.WriteLine("libgp4.dll: " + Message); }
                catch(Exception) { enable_output_channel[1] = false; }
        }

        /// <summary>
        /// Check Whether Or Not The .gp4 Path Given Points To A Valid File.<br/><br/>
        /// Checks It As Both An Absolute Path, And As A Relative Path.
        /// </summary>
        ///
        /// <param name="GP4Path">
        /// An Absolute Or Relative Path To The .gp4 File.
        /// </param>
        /// 
        /// <returns>
        /// The GP4Path, Either Unmodified If Valid On It's Own, Or With The Current Directory Prepended If It's A Valid Relative Path.<br/>
        /// string.Empty Otherwise
        /// </returns>
        private static string CheckGP4Path(string GP4Path) {

            // Absolute And Second Relative Path Checks || (In Case The User Excluded The First Backslash, idfk Why)
            if(!File.Exists(GP4Path))

                // Bad Path
                if(!File.Exists($@"{Directory.GetCurrentDirectory()}\{GP4Path}"))
                        DLog($"Invalid .gp4 Path Given, Please Provide A Valid Absolute Or Relative Path To A .gp4 Project File.\n(Given Path: {GP4Path})");

                // Relative Path Checks Out
                else return $@"{Directory.GetCurrentDirectory()}\{GP4Path}";

            // Path Good As-Is
            return GP4Path;
        }


        /// <summary> Open A .gp4 at The Specified Path and Read The The Value Of "AttributeName" At The Given Parent Node. </summary>
        /// 
        /// <param name="GP4Path"> The Absolute Or Relative Path To The .gp4 File. </param>
        /// <param name="NodeName"> Attribute's Parent Node. </param>
        /// <param name="AttributeName"> The Attribute To Read And Return. </param>
        /// <returns> The Value Of The Specified Attribute If Successfully Found; string.Empty Otherwise.
        ///</returns>
        private static string GetAttribute(string GP4Path, string NodeName, string AttributeName) {
            if((GP4Path = CheckGP4Path(GP4Path)) != string.Empty)
                using(StreamReader GP4File = new StreamReader(GP4Path)) {
                    string Out;
                    
                    GP4File.ReadLine(); // Skip Version Confilct

                    using(var gp4 = XmlReader.Create(GP4File))
                        while(gp4.Read())
                            if(gp4.LocalName == NodeName && (Out = gp4.GetAttribute(AttributeName)) != null)
                                return Out;
                }

            DLog($"Attribute \"{AttributeName}\" Not Found");
            return string.Empty;
        }


        /// <summary> Open A .gp4 at The Specified GP4Path and Read All Nodes With AttributeName. </summary>
        /// 
        /// <param name="GP4Path"> An Absolute Or Relative Path To The .gp4 File</param>
        /// <param name="NodeName"> Attribute's Parent Node To Cgeck Every Instance Of </param>
        /// <param name="AttributeName"> The Attribute To Read And Add To The List </param>
        /// 
        /// <returns> A String Array Containing The Value Of Each Instance Of AttributeName, Or An Empty String Array Otherwise.
        ///</returns>
        private static string[] GetAttributes(string GP4Path, string NodeName, string AttributeName) {
            if((GP4Path = CheckGP4Path(GP4Path)) != string.Empty)
                using(StreamReader GP4File = new StreamReader(GP4Path)) {
                    var Out = new List<string>();
                    string tmp;

                    GP4File.ReadLine(); // Skip Version Confilct

                    using(var gp4 = XmlReader.Create(GP4File))
                        while(gp4.Read())
                            if(gp4.LocalName == NodeName && (tmp = gp4.GetAttribute(AttributeName)) != null)
                                Out.Add(tmp);

                    if(Out.Count > 0)
                        return Out.ToArray();
                }

            DLog($"Attribute \"{AttributeName}\" Not Found");
            return Array.Empty<string>();
        }


        /// <summary> Open A .gp4 at The Specified GP4Path and Read The Inner Xml Contents Of The Specified Node. </summary>
        /// 
        /// <param name="GP4Path"> An Absolute Or Relative Path To The .gp4 File. </param>
        /// <param name="NodeName"> Attribute's Parent Node To Cgeck Every Instance Of. </param>
        /// 
        /// <returns> The Inner Xml Data Of the Given Node If It's Successfully Found; string.Empty Otherwise.
        ///</returns>
        private static string GetInnerXMLData(string GP4Path, string NodeName) {
            if ((GP4Path = CheckGP4Path(GP4Path)) != string.Empty)
                using(StreamReader GP4File = new StreamReader(GP4Path)) {
                    GP4File.ReadLine(); // Skip Version Confilct

                    using(var gp4 = XmlReader.Create(GP4File))
                        while(gp4.Read())
                            if(gp4.LocalName == NodeName)
                                return gp4.ReadInnerXml();

                }
            else return string.Empty;


            DLog($"Node \"{NodeName}\" Not Found");
            return string.Empty;
        }


        /// <summary>
        /// Parse Each .gp4 Node For Relevant PS4 .gp4 Project Data.
        /// <br/><br/>
        /// Throws An InvalidDataException If Invalid Attribute Values Are Found.<br/>
        /// (For Example: If Invalid Files Have Been Added To The .gp4's File Listing, Or There Are Conflicting Variables For<br/>
        /// The Package Type Because Of gengp4.exe Being A Pile Of Arse.)
        /// <br/><br/>
        /// Variables Prepended With @ Are Only Read For .gp4 Integrity Verification
        /// </summary>
        /// 
        /// <exception cref="InvalidDataException"/>
        private void ParseGP4(XmlReader gp4) {
            while(gp4.Read() && !(gp4.MoveToContent() == XmlNodeType.EndElement && gp4.LocalName == "psproject")) {

                if(gp4.NodeType == XmlNodeType.Element) {
                    int i;

                    switch(gp4.LocalName) { //! REMOVE SWITCH CASE

                        default:
                            continue;

                        // Save Format & Version For Integrity Check
                        case "psproject":
                            format = gp4.GetAttribute("fmt");
                            version = int.Parse(gp4.GetAttribute("version"));
                            break;

                        // Get The Current Package Type
                        ///
                        case "volume_type": {
                            IsPatchProject = (volume_type = gp4.ReadInnerXml()) == "pkg_ps4_patch";

                            // Check .gp4 Integrity
                            if(!IsPatchProject && volume_type != "pkg_ps4_app")
                                ELog(gp4.LocalName, $"Unexpacted Volume Type For PS4 Package: {volume_type}");

                            break;
                        }

                        // Save Volume Id For Integrity Check
                        case "volume_id":
                            volume_id = gp4.ReadInnerXml();
                            break;

                        // Read The Volume Timestamp
                        case "volume_ts":
                            Timestamp = gp4.ReadInnerXml();
                            break;


                        // Parse The Contents Of "package" Node
                        ///
                        case "package": {
                            // Read App Path & Check .gp4 Integrity
                            if((BaseAppPkgPath = gp4.GetAttribute("app_path")) == string.Empty && IsPatchProject)
                                ELog(gp4.LocalName, $"Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)");

                            ContentID = gp4.GetAttribute("content_id");
                            Passcode = gp4.GetAttribute("passcode");
                            storage_type = gp4.GetAttribute("storage_type");
                            app_type = gp4.GetAttribute("app_type");

                            // Check .gp4 Integrity
                            if(Passcode.Length < 32)
                                ELog(gp4.LocalName, $"Passcode Length Was Less Than 32 Characters.");

                            break;
                        }


                        // Parse The Expected Chunk And Scenario Counts From The "chunk_info" Node
                        case "chunk_info": {
                            ScenarioCount = int.Parse(gp4.GetAttribute("scenario_count"));
                            ChunkCount = int.Parse(gp4.GetAttribute("chunk_count"));

                            // Check .gp4 Integrity
                            if(ScenarioCount == 0 || ChunkCount == 0)
                                ELog(gp4.LocalName, $"Scenario And/Or Chunk Counts Were 0 (Scnarios: {ScenarioCount}, Chunks: {ChunkCount})");

                            break;
                        }


                        // Parse The Contents Of "chunks" Node And Add All Chunks To The Chunks Str Array
                        ///
                        case "chunks": {
                            gp4.Read();
                            i = 0;
                            var Chunks = new List<string>();

                            // Read All Chunks
                            while(gp4.Read()) { //! remove log output
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "chunk") {
                                    if(gp4.LocalName == "chunks")
                                        break;

                                    continue;
                                }

                                Chunks.Add(gp4.GetAttribute("label"));
                                ++i;
                            }

                            // Check .gp4 Integrity
                            if(i != ChunkCount)
                                ELog(gp4.LocalName, $"ERORR: \"chunk_count\" Attribute Did Not Match Amount Of Chunk Nodes ({i} != {ChunkCount})");

                            this.Chunks = Chunks.ToArray();
                            break;
                        }


                        // Parse The Contents Of "Files" Node
                        ///
                        case "scenarios": {
                            i = 0;
                            var Scenarios = new List<Scenario>();

                            DefaultScenarioId = int.Parse(gp4.GetAttribute("default_id"));

                            // Read All Scenarios
                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "scenario") { // Check For End Of "scenario" Nodes
                                    if(gp4.LocalName == "scenarios")
                                        break;

                                    continue;
                                }

                                Scenarios.Add(new Scenario(gp4));
                                ++i;
                            }

                            // Check .gp4 Integrity
                            if(i != ScenarioCount)
                                ELog(gp4.LocalName, $"\"scenario_count\" Attribute Did Not Match Amount Of Scenario Nodes ({i} != {ScenarioCount})");

                            this.Scenarios = Scenarios.ToArray();
                            break;
                        }


                        // Parse The Contents Of "Files" Node
                        ///
                        case "files": {
                            gp4.Read();
                            i = 0;
                            var Files = new List<string>();
                            var InvalidFiles = string.Empty;

                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "file") {// Check For End Of "file" Nodes
                                    if(gp4.LocalName == "files")
                                        break;

                                    continue;
                                }

                                Files.Add(gp4.GetAttribute("orig_path"));
                                FileCount++;

                                // * Check .gp4 Integrity
                                if(project_file_blacklist.Contains(Files.Last())) {
                                    InvalidFiles += $"{Files.Last()}\n";
                                    ++i;
                                }
                            }

                            // Print Any Invalid File Paths
                            if(i > 0)
                                ELog(gp4.LocalName, $"Invalid File{(i > 1 ? "s " : " ")}In .gp4 Project: {InvalidFiles}");

                            this.Files = Files.ToArray();
                            break;
                        }

                        // Parse The Contents Of "Files" Node
                        ///
                        case "rootdir": {
                            i = 0;
                            var Subfolders = new List<string>();
                            var SubfolderNames = new List<string>();

                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "dir") {// Check For End Of "dir" Nodes
                                    if(gp4.LocalName == "rootdir")
                                        break;

                                    continue;
                                }
                                SubfolderNames.Add(gp4.GetAttribute("targ_name"));

                                if(i > 0 && gp4.Depth > i) // Append Subfolder Name To Parent
                                    Subfolders.Add($@"{Subfolders[Subfolders.Count - 1]}\{SubfolderNames.Last()}");
                                else
                                    Subfolders.Add(SubfolderNames.Last()); // Add As New Folder

                                SubfolderCount++;
                                i = gp4.Depth;
                            }

                            this.SubfolderNames = SubfolderNames.ToArray();
                            this.Subfolders = Subfolders.ToArray();
                            break;
                        }
                    }
                }
            }
        }

        #endregion
        /////////////////////////////////////|





        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     GP4 Attributes / Values     --\\\
        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region GP4 Attributes / Values

        /// <summary>
        /// Volume Timestamp / .gp4 Creation Time <br/><br/>
        /// Note:<br/>gengp4.exe Does Not Record The Time, Instead Only The Current YY/MM/DD, And 00:00:00 <br/>
        /// </summary>
        public string Timestamp { get; private set; }


        /// <summary>
        /// (Applies To Patch Packages Only)
        /// <br/><br/>
        /// 
        /// An Absolute Path To The Base Game Package The Patch .pkg's Going To Be Married And Installed To.<br/>
        /// </summary>
        public string BaseAppPkgPath { get; private set; }


        /// <summary> Password To Be Used In Pkg Creation
        ///</summary>
        public string Passcode { get; private set; }


        /// <summary> True If The .gp4 Project Is For A Patch .pkg, False Otherwise.
        ///</summary>
        public bool IsPatchProject { get; private set; }


        /// <summary>
        /// Content ID Of The .gp4 Project's Game
        /// <br/>
        /// (Title &amp; Title ID)
        /// </summary>
        public string ContentID { get; private set; }


        /// <summary> Array Of All Files Listed In The .gp4 Project
        ///</summary>
        public string[] Files { get; private set; }
        public int FileCount  { get; private set; }


        /// <summary> Array Containing The Names Of Each Folder/Subfolder Within The Project Folder
        ///</summary>
        public string[] SubfolderNames { get; private set; }

        /// <summary> Array Containing The Full Path For Each Folder/Subfolder Within The Project Folder
        ///</summary>
        public string[] Subfolders { get; private set; }
        public int SubfolderCount  { get; private set; }


        /// <summary> Array Containing Chunk Data For The Selected .gp4 Project File
        ///</summary>
        public string[] Chunks { get; private set; }
        public int ChunkCount  { get; private set; }


        /// <summary> Array Of Scenario Data For The .gp4 Project.
        /// </summary>
        public Scenario[] Scenarios { get; private set; }
        public int ScenarioCount    { get; private set; }

        /// <summary> The Default Scenario ID Of The .gp4 Project.
        /// </summary>
        public int DefaultScenarioId { get; private set; }



        /// <summary> Varible Used Only In .gp4 Integrity Verification.
        /// </summary>
        private string
            format,
            volume_type,
            volume_id,
            storage_type,
            app_type
        ;

        /// <summary> Varible Used Only In .gp4 Integrity Verification.
        /// </summary>
        private int version;

        #endregion



        /////////////////\\\\\\\\\\\\\\\\\
        ///--     User Functions     --\\\
        /////////////////\\\\\\\\\\\\\\\\\
        #region User Functions





        /// <summary> Check Various Parts Of The .gp4 To Try And Find Any Possible Errors In The Project File.
        ///</summary>
        /// <returns> False If Nothing's Wrong. Throws An InvalidDataException If The Class Was Initialized With Assertions Enabled. True Otherwise. </returns>
        public void CheckGP4Integrity() {
            var Errors = string.Empty;
            int i;

            // Check "psproject" Node Attributes
            if(format != "gp4" || version != 1000)
                Errors += $"Invalid Attribute Values In \"psproject\" Node.\nFormat: {format} != gp4 || Version: {version} != 1000 \n\n";


            // Check "volume_id" Node Attributes
            if(volume_id != "PS4VOLUME")
                Errors += $"Invalid volume_id Attribute in .gp4, Should Be PS4VOLUME. (Read: {volume_id})\n\n";



            //======================================\\
            //| Check For Invalid Volume Type Data |\\
            //======================================\\
            {
                if(volume_type != "pkg_ps4_app" && volume_type != "pkg_ps4_patch")
                    Errors += $"Invalid Volume Type:\n ({volume_type})\n\n";

                if(IsPatchProject && volume_type != "pkg_ps4_patch")
                    Errors += $"Unexpacted Volume Type For PS4 Patch Package: {volume_type}";

                else if(!IsPatchProject && volume_type != "pkg_ps4_app")
                    Errors += $"Unexpacted Volume Type For PS4 App Package: {volume_type}";

                if(BaseAppPkgPath == string.Empty && IsPatchProject)
                    Errors += "Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)\n\n";
            }



            //===================================\\
            //| Check "package" Node Attributes |\\
            //===================================\\
            {

                if((!IsPatchProject && storage_type != "digital50") || (IsPatchProject && storage_type != "digital25"))
                    Errors +=
                        $"Unexpected Storage Type For {(IsPatchProject ? "Patch" : "Full Game")} Package\n" +
                        $"(Expected: {(IsPatchProject ? "digital25" : "digital50")}\nRead: {storage_type})\n\n";

                if(app_type != "full")
                    Errors += $"Invalid Application Type In .gp4 Project\n(Expected: full\nRead: {app_type})\n\n";

                if(Passcode.Length != 32)
                    Errors += $"Incorrect Passcode Length, Must Be 32 Characters (Actual Length: {Passcode.Length})\n\n";



                #region Lazy Content Id Chwck
                var buff = new byte[36];
                string[] arr;
                string p1 = string.Empty, p2;
                StringBuilder Builder;
                int ind, byteIndex = 0;


                foreach(var file in Files)
                    if(file.Contains("param.sfo"))
                        p1 = file;

                if(p1 == "")
                    Errors += $"Param.sfo File Not Found In .gp4 File Listing (Mandatory For Package Creation)";

                else if(File.Exists(p1)) {
                    using(var sfo = File.OpenRead(p1)) {
                        buff = new byte[4];
                        sfo.Position = 0x8;
                        sfo.Read(buff, 0, 4);
                        var i0 = BitConverter.ToInt32(buff, 0);

                        sfo.Position = 0x0C;
                        sfo.Read(buff, 0, 4);
                        var i1 = BitConverter.ToInt32(buff, 0);

                        sfo.Position = 0x10;
                        sfo.Read(buff, 0, 4);
                        var i2 = BitConverter.ToInt32(buff, 0);

                        arr = new string[i2];
                        var iA = new int[i2];
                        buff = new byte[i1 - i0];
                        sfo.Position = i0;
                        sfo.Read(buff, 0, buff.Length);

                        for(ind = 0; ind < arr.Length; ind++) {
                            Builder = new StringBuilder();

                            while(buff[byteIndex] != 0)
                                Builder.Append(Encoding.UTF8.GetString(new byte[] { buff[byteIndex++] })); // Just Take A Byte, You Fussy Prick

                            byteIndex++;
                            arr[ind] = Builder.ToString();
                        }

                        sfo.Position = 0x20;
                        buff = new byte[4];
                        for(ind = 0; ind < i2; sfo.Position += 0x10 - buff.Length) {
                            sfo.Read(buff, 0, 4);
                            iA[ind] = i1 + BitConverter.ToInt32(buff, 0);
                            ind++;
                        }

                        for(ind = 0; ind < i2; ind++) {
                            if(arr[ind] != "CONTENT_ID")
                                continue;

                            buff = new byte[36];
                            sfo.Position = iA[ind];
                            sfo.Read(buff, 0, 36);

                            sfo_content_id = Encoding.UTF8.GetString(buff);
                        }
                    }


                    if(File.Exists(p2 = $"{p1.Remove(p1.LastIndexOf('\\') + 1)}playgo-chunk.dat")) {
                        using(var playgo = File.OpenRead(p2)) {
                            playgo.Position = 0x40;
                            playgo.Read(buff, 0, 36);
                            playgo_content_id = Encoding.UTF8.GetString(buff);
                        }

                        if(ContentID == playgo_content_id && playgo_content_id == sfo_content_id) // Check For Mismatched Content Id's
                            Errors += $"Content Id Mismatch In .gp4 Project.\n{ContentID}\n .dat{playgo_content_id}\n .sfo: {sfo_content_id}\n\n";
                    }
                }
                #endregion
            }
            //|===============================================================================================================================|\\



            //============================\\
            //| Check Chunk Related Shit |\\
            //============================\\
            {
                // Check Default Scenario Id
                if(DefaultScenarioId > Scenarios.Length - 1)
                    Errors += $"Default Scenario Id Was Out Of Bounds ({DefaultScenarioId} > {Scenarios.Length - 1})\n\n";

                // Check Chunk Data Integrity
                if(Chunks.Length != ChunkCount)
                    Errors += "Number Of Chunks Listed In .gp4 Project Doesn't Match ChunkCount Attribute\n\n";


                // Check Scenario Data Integrity
                if(Scenarios.Length != ScenarioCount)
                    Errors += "Number Of Scenarios Listed In .gp4 Project Doesn't Match ScenarioCount Attribute\n\n";

                // Check .gp4 Integrity
                if(ScenarioCount == 0 || ChunkCount == 0)
                    Errors += $"Scenario And/Or Chunk Counts Were 0 (Scnarios: {ScenarioCount}, Chunks: {ChunkCount})\n\n";

                // Check Scenarios
                if(Scenarios == null)
                    Errors += "Application Was Missing Scenario Elements. (GP4Reader.Scenarios Was Null)";

                else {
                    i = 1;
                    foreach(var Sc in Scenarios) {
                        if(Sc.Id > Scenarios.Length - 1)
                            Errors += $"Scenario Id Was Out Of Bounds In Scenario #{i} ({DefaultScenarioId} > {Scenarios.Length - 1})\n\n";

                        if(Sc.Type != "mp" && Sc.Type != "sp")
                            Errors += $"Unexpected Scenario Type For Scenario #{i}\n(Expected: sp || mp\nRead: {Sc.Type})\n\n";

                        if(Sc.InitialChunkCount > Chunks.Length)
                            Errors += $"Initial Chunk Count For Scenario #{i} Was Larger Than The Actual Chunk Count {Sc.InitialChunkCount} > {Chunks.Length}\n\n";

                        if(Sc.Label == "")
                            Errors += $"Empty Scenario Label In Scenario #{i}\n\n";

                        var RangeChk = int.Parse(Sc.ChunkRange.Substring(Sc.ChunkRange.LastIndexOf('-') + 1));
                        if(RangeChk >= ChunkCount)
                            Errors += $"Invalid Maximum Value For Chunk Range In Scenario #{i}\n ({RangeChk} >= {ChunkCount})\n\n";

                        RangeChk = int.Parse(Sc.ChunkRange.Remove(Sc.ChunkRange.LastIndexOf('-')));
                        if(RangeChk >= ChunkCount)
                            Errors += $"Invalid Minimum Value For Chunk Range In Scenario #{i}\n ({RangeChk} >= {ChunkCount})\n\n";

                        ++i;
                    }
                }
            }
            //|===============================================================================================================================|\\



            // Check File List To Ensure No Files That Should Be Excluded Have Been Added To The .gp4
            i = 0;
            var Base = $" Invalid File(s) Included In .gp4 Project:\n";
            foreach(var file in Files)
                if(project_file_blacklist.Contains(file)) {
                    Base += $"{file}\n";
                    ++i;
                }

            if(i != 0)
                Errors += i + $"{Base}\n";



            //===========================\\
            //| No Errors Were Detected |\\
            //===========================\\
            if(Errors == string.Empty) {
                return;
            }



            //==================================================\\
            //| Throw An Exception If Any Errors Were Detected |\\
            //==================================================\\

            string Message;
            var ErrorCount = (Errors.Length - Errors.Replace("\n\n", "").Length) / 2;

            if(ErrorCount == 1)
                Message = $"The Following Error Was Found In The .gp4 Project File At {gp4_path}:\n{Errors}";

            else
                Message = $"The Following {ErrorCount} Errors Were Found In The .gp4 Project File At {gp4_path}:\n{Errors}";

            DLog(Message);

            throw new InvalidDataException(Message);
        }

        /// <summary>
        /// Check Whether Or Not The .gp4 Project File Is For A Patch .pkg
        /// </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> True If The Volume Type Is pkg_ps4_patch. </returns>
        public static bool IsPatchPackage(string GP4Path) { return GetInnerXMLData(GP4Path, "volume_type") == "pkg_ps4_patch"; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns>  </returns>
        public static string GetTimestamp(string GP4Path) => GetInnerXMLData(GP4Path, "volume_ts");


        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> The Passcode The .pkg Will Be Encrypted With (Pointless On fpkg's, Does Not Prevent Dumping, Only orbis-pub-chk extraction)
        ///</returns>
        public static string GetPkgPasscode(string GP4Path) => GetAttribute(GP4Path, "package", "passcode");

        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> The Path Of The Base Game Package The .gp4 Project File's Patch Is To Be Married With
        ///</returns>
        public static string GetBasePkgPath(string GP4Path) => GetAttribute(GP4Path, "package", "app_path");

        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> .gp4 Project File
        ///</returns>
        public static string GetChunkCount(string GP4Path) => GetAttribute(GP4Path, "chunk_info", "chunk_count");

        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> .gp4 Project File
        ///</returns>
        public static string GetScenarioCount(string GP4Path) => GetAttribute(GP4Path, "chunk_info", "scenario_count");


        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Game Chunks Listed In The .gp4 Project
        ///</returns>
        public static string[] GetChunkListing(string GP4Path) => GetAttributes(GP4Path, "chunk", "label");

        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Game Scenarios Listed In The .gp4 Project
        ///</returns>
        public static Scenario[] GetScenarioListing(string GP4Path) {
            using(StreamReader GP4File = new StreamReader(GP4Path)) {
            
                GP4File.ReadLine(); // Skip Version Confilct
                var Scenarios = new List<Scenario>();

                using(var gp4 = XmlReader.Create(GP4File)) {
                    while(gp4.Read()) {

                        // Check For End Of "scenario" Nodes
                        if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "scenario")
                            if(gp4.LocalName == "scenarios" && gp4.NodeType == XmlNodeType.EndElement)
                                break;

                            else continue;

                        Scenarios.Add(new Scenario(gp4));
                    }

                    // Check .gp4 Integrity
                    if(Scenarios.Count == 0) {
                        var Error = $"Could Not Find Any Scenarios In The Selected .gp4 Project";

                        ELog(gp4.LocalName, Error);
                        throw new InvalidDataException(Error);
                    }
                }

                return Scenarios.ToArray();
            }

        }


        ///<summary>
        ///
        ///</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Project Files Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFileListing(string GP4Path) => GetAttributes(GP4Path, "file", "targ_path");

        /// <summary>
        /// Read And Return the Names Of All Subfolders Located Wiithin The Project Folder
        /// </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Just The Names Of All Subfolders Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFolderNames(string GP4Path) => GetAttributes(GP4Path, "dir", "targ_name");

        /// <summary>
        /// Read And Return All Subfolders Located Wiithin The Project Folder
        /// </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Subfolders Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFolderListing(string GP4Path) {
            using(StreamReader GP4File = new StreamReader(GP4Path)) {

                GP4File.ReadLine(); // Skip Version Confilct

                var PrevNodeIndentation = 0;
                List<string>
                    SubfolderNames = new List<string>(),
                    Subfolders     = new List<string>()
                ;
                
                using(var gp4 = XmlReader.Create(GP4File)) {
                    while(gp4.Read()) {

                        // Check For End Of "dir" Nodes
                        if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "dir") {
                            if(gp4.LocalName == "rootdir" && gp4.NodeType == XmlNodeType.EndElement)
                                break;

                            continue;
                        }

                        // Read Value Of The Current Directory Node
                        var DirName = gp4.GetAttribute("targ_name");

                        // Append Subfolder Name To Parent If Node Depth Is Deeper Than The Previous Iteration
                        if(PrevNodeIndentation > 0 && gp4.Depth > PrevNodeIndentation)
                            Subfolders.Add($@"{Subfolders[Subfolders.Count - 1]}\{DirName}");

                        else // Add As New Folder Otherwise
                            Subfolders.Add(DirName);

                        SubfolderNames.Add(DirName); // Save Directory Name Alone

                        PrevNodeIndentation = gp4.Depth; // Save Node Indentation For The Following Iteration
                        
                    }
                }

                return Subfolders.ToArray();
            }
        }



        /// <summary> Check Various Parts Of The .gp4 To Try And Find Any Possible Errors In The Project File.
        ///</summary>
        /// <returns> False If Nothing's Wrong. </returns>
        public static void CheckGP4Integrity(string GP4Path) => new GP4Reader(GP4Path).CheckGP4Integrity();
        #endregion
    }


    /// <summary> A Small Class For Creating new .gp4 Files From Raw PS4 Gamedata, With A Few Options Related To .pkg Creation.
    ///</summary>
    public partial class GP4Creator {

        /// <summary>
        /// Initialize A New Instance Of The GP4Creator Class With Which To Build A New .gp4 Project With Various Settings.<br/>
        /// [DOESN'T ACTUALLY DO THIS RN] Parses The param.sfo &amp; playgo-chunks.dat As Well As The Project Files/Folders Without Building The .gp4.
        /// </summary>
        /// 
        /// <param name="GamedataFolder"> The Folder Containing The Gamedata To Create A .gp4 Project File For. </param>
        public GP4Creator(string GamedataFolder) {
            gamedata_folder = GamedataFolder;
            Passcode = "00000000000000000000000000000000";
            Keystone = true;
            gp4 = new XmlDocument();
            // DO SOMETHING EXTRA (for your own app, decide whtehr to inculude itin the releas verison ;ater on)
        }


        /// <summary>
        /// Initialize A New Instance Of The GP4Creator Class With Which To Edit<br/>
        /// Parses The param.sfo &amp; playgo-chunks.dat As Well As The Project Files/Folders Without Building The .gp4.
        /// </summary>
        /// 
        public GP4Creator() {
            Passcode = "00000000000000000000000000000000";
            Keystone = true;
            gp4 = new XmlDocument();
        }




        ///////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     Basic Internal Variables     --\\\
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region Basic Internal Variables

        /// <summary> Main GP4 Structure Refference.
        ///</summary>
        private readonly XmlDocument gp4;

        /// <summary> Root Gamedata Directory To Be Parsed. (Should Contain At Least An Executable And sce_sys Folder)
        ///</summary>
        private string gamedata_folder;

        /// <summary> Names Of Files That Are Always To Be Excluded From .gp4 Projects By Default.
        ///</summary>
        public readonly string[] DefaultBlacklist = new string[] {
                  // Drunk Canadian Guy
                    "right.sprx",
                    "sce_discmap.plt",
                    "sce_discmap_patch.plt",
                    @"sce_sys\playgo-chunk",
                    @"sce_sys\psreserved.dat",
                    @"sce_sys\playgo-manifest.xml",
                    @"sce_sys\origin-deltainfo.dat",
                  // Al Azif
                    @"sce_sys\.metas",
                    @"sce_sys\.digests",
                    @"sce_sys\.image_key",
                    @"sce_sys\license.dat",
                    @"sce_sys\.entry_keys",
                    @"sce_sys\.entry_names",
                    @"sce_sys\license.info",
                    @"sce_sys\selfinfo.dat",
                    @"sce_sys\imageinfo.dat",
                    @"sce_sys\.unknown_0x21",
                    @"sce_sys\.unknown_0xC0",
                    @"sce_sys\pubtoolinfo.dat",
                    @"sce_sys\app\playgo-chunk",
                    @"sce_sys\.general_digests",
                    @"sce_sys\target-deltainfo.dat",
                    @"sce_sys\app\playgo-manifest.xml"
        };


#if DEBUG
        // Collection of Parameters Parsed From The Last of Us Part II, Kept For Testing Purposes
        string[] DEBUG_misc_sfo_variables = new string[] {
                        "APP_TYPE",
                        "APP_VER",
                        "ATTRIBUTE",
                        "ATTRIBUTE2",
                        "CATEGORY",
                        "CONTENT_ID",
                        "DEV_FLAG",
                        "DOWNLOAD_DATA_SIZE",
                        "FORMAT",
                        "PARENTAL_LEVEL",
                        "PUBTOOLINFO",
                        "PUBTOOLMINVER",
                        "PUBTOOLVER",
                        "REMOTE_PLAY_KEY_ASSIGN",
                        "SERVICE_ID_ADDCONT_ADD_1",
                        "SERVICE_ID_ADDCONT_ADD_2",
                        "SERVICE_ID_ADDCONT_ADD_3",
                        "SERVICE_ID_ADDCONT_ADD_4",
                        "SERVICE_ID_ADDCONT_ADD_5",
                        "SERVICE_ID_ADDCONT_ADD_6",
                        "SERVICE_ID_ADDCONT_ADD_7",
                        "SYSTEM_VER",
                        "TARGET_APP_VER",
                        "TITLE",
                        "TITLE_00",
                        "TITLE_03",
                        "TITLE_05",
                        "TITLE_07",
                        "TITLE_08",
                        "TITLE_17",
                        "TITLE_20",
                        "TITLE_ID",
                        "USER_DEFINED_PARAM_1",
                        "VERSION"
        };
#endif


        /// <summary> List Of Additional Files To Include In The Project, Added by The User.
        ///</summary>
        private string[][] extra_files;


        /// <summary>
        /// Output Log Messages To A Custom Output Method (GP4Creator.LoggingMethod(string)), And/Or To The Console If Applicable.<br/><br/>
        /// Duplicates Message To Standard Console Output As Well.
        ///</summary>
        private void WLog(object o, bool Verbosity) {
#if Log
            if(LoggingMethod != null && !(VerboseLogging ^= Verbosity))
                LoggingMethod(o as string);
#endif

#if DEBUG
            DLog(o);
#endif
        }

        /// <summary> Console Logging Method.
        ///</summary>
        private string DLog(object o) {
#if DEBUG
            try { Debug.WriteLine(o); }
            catch(Exception){}

            if(!Console.IsOutputRedirected)
                try { Console.WriteLine(o); }
                catch(Exception){}
#endif
            return o as string;
        }
        #endregion




        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        //--     User Options For GP4 Creation     --\\
        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        #region User Options For GP4 Creation

        /// <summary>
        /// Include The Keystone File Used For Savedata Creation/Usage In The .gp4's File Listing.
        /// <br/> Including The Original Is Recommended To Maintain Support For Savedata Created By The Original Application.
        /// <br/><br/> (True By Default)
        /// </summary>
        public bool Keystone;

        /// <summary>
        /// The 32-bit Key Used To Encrypt The .pkg. Required For Extraction With orbis-pub-chk.
        /// <br/><br/> (No Effect On Dumping)
        /// </summary>
        public string Passcode;

        /// <summary>
        /// An Array Containing The Names Of Any Files Or Folders That Are To Be Excluded From The .gp4 Project.
        /// </summary>
        public string[] BlacklistedFilesOrFolders;

        /// <summary>
        /// Path To The Base Application Package The New Package Is To Be Married To.
        /// </summary>
        public string SourcePkgPath;

        /// <summary>
        /// Set Whether Or Not To Use Absolute Or Relative Pathnames For The .gp4 Project's File Listing 
        /// <br/><br/> (True By Default)
        /// </summary>
        public bool AbsoluteFilePaths;

#if Log
        /// <summary>
        /// Optional Method To Use For Logging. [Function(string s)]
        /// </summary>
        public Action<object> LoggingMethod = null;

        /// <summary>
        /// Set GP4 Log Verbosity.
        /// </summary>
        public bool VerboseLogging;
#endif


#if GUIExtras
        /// <summary>
        /// The Application's Default Name, Read From The param.sfo In The Provided Gamedata Folder.
        /// </summary>
        public string AppTitle { get; private set; }

        /// <summary>
        /// The Various Titles Of The Application, If There Are Titles Passed The Default (e.g. Title_XX). Left null Otherwise.
        /// </summary>
        public List<string> AppTitles { get; private set; }

        /// <summary>
        /// The Application's Intended Package Type.
        /// </summary>
        public int AppType { get; private set; }

        public string TargetAppVer { get; private set; }
        public string CreationDate { get; private set; }
        public string SdkVersion   { get; private set; }
#endif
        #endregion





        /////////////////\\\\\\\\\\\\\\\\\
        ///--     User Functions     --\\\
        /////////////////\\\\\\\\\\\\\\\\\
        #region User Functions

        /// <summary>
        /// Add External Files To The Project's File Listing (wip, this wouldn't work the way it is lol)
        /// </summary>
        public void AddFiles(string[] TargetPaths, string[] OriginalPaths) {
            if(extra_files == null) {
                extra_files = new string[OriginalPaths.Length][];

                for(var i = 0; i < extra_files.Length; ++i) {
                    extra_files[i][0] = TargetPaths[i];
                    extra_files[i][1] = OriginalPaths[i];
                }
                return;
            }


            var buffer = extra_files;
            buffer.CopyTo(extra_files = new string[buffer.Length + OriginalPaths.Length][], 0);

            for(var i = buffer.Length; i < extra_files.Length; ++i) {
                extra_files[i][0] = TargetPaths[i];
                extra_files[i][1] = OriginalPaths[i];
            }
        }

        /// <summary>
        /// Add An External File To The Project's File Listing (wip, this wouldn't work the way it is lol)
        /// </summary>
        public void AddFile(string TargetPath, string OriginalPath) {
            if(extra_files != null) {
                var buffer = extra_files;
                buffer.CopyTo(extra_files = new string[buffer.Length + 1][], 0);

                extra_files[extra_files.Length - 1][0] = OriginalPath;
                extra_files[extra_files.Length - 1][1] = TargetPath;
                return;
            }

            extra_files = new string[][] { new string[] { OriginalPath, TargetPath } };
        }


        /// <summary>
        /// Build A New .gp4 Project File For The Provided Gamedata With The Current Options/Settings, And Save It In The Specified OutputDirectory.<br/><br/>
        /// First, Parses gamedata_folder\sce_sys\playgo-chunks.dat &amp; gamedata_folder\sce_sys\param.sfo For Parameters Required For .gp4 Creation,<br/>
        /// Then Saves All File/Subdirectory Paths In The Gamedata Folder
        /// </summary>
        /// 
        /// <param name="GP4OutputPath"> Folder In Which To Place The Newly Build .gp4 Project File. </param>
        /// <param name="VerifyIntegrity"> Set Whether Or Not To Abort The Creation Process If An Error Is Found That Would Cause .pkg Creation To Fail, Or Simply Log It To The Standard Console Output And/Or LogOutput(string) Action. </param>
        public void CreateGP4(string GP4OutputPath, bool VerifyIntegrity) {
            
            // Timestamp For GP4, Same Format Sony Used Though Sony's Technically Only Tracks The Date,
            // With The Time Left As 00:00, But Imma Just Add The Time. It Doesn't Break Anything).
            var gp4_timestamp = DateTime.Now.GetDateTimeFormats()[78];

            int
                chunk_count,    // Amount Of Chunks In The Application
                scenario_count, // Amount Of Scenarios In The Application
                default_scenario_id // Id/Index Of The Application's Default Scenario
            ;

            int[]
                scenario_types,       // The Types Of Each Scenario (SP / MP)
                scenario_chunk_range, // Array Of Chunk Ranges For Each Scenario
                initial_chunk_count   // The Initial Chunk Count Of Each Scenario
            ;

            string
                app_ver = null,     // App Patch Version
                version = null,     // Remaster Ver
                playgo_content_id = null, // Content Id From sce_sys/playgo-chunks.dat To Check Against Content Id In sce_sys/param.sfo
                content_id = null,  // Content Id From sce_sys/param.sfo
                title_id = null,    // Application's Title Id
                category = null,    // Category Of The PS4 Application (gd / gp)
                storage_type = null // Storage Type For The Package (25gb/50gb)
            ;

            string[]
                file_paths,     // Array Of All Files In The Project Folder (Excluding Blacklisted Files/Directories)
                chunk_labels,   // Array Of All Chunk Names
                scenario_labels // Array Of All Scenario Names
            ;

            XmlNode[] base_elements;



#if Log
            WLog($"Starting .gp4 Creation.", false);
            WLog($"PKG Passcode: {Passcode}\nSource .pkg Path: {SourcePkgPath}\n", true);
#endif




            /* Parse playgo-chunks.dat For Required .gp4 Variables.
            | ========================= |
            | Sets The Following Values:
            |
            | chunk_count
            | chunk_labels
            | scenario_count
            | scenario_types
            | scenario_labels
            | initial_chunk_count
            | scenario_chunk_range
            | default_id
            | content_id
            | 
            | ========================= | */
            using(var playgo = File.OpenRead($@"{gamedata_folder}\sce_sys\playgo-chunk.dat")) {
                WLog($"Parsing playgo-chunk.dat File\nPath: {gamedata_folder}\\sce_sys\\playgo-chunk.dat", true);

                var buffer = new byte[4];

                void ConvertbufferToStringArray(string[] StringArray) {
                    int byteIndex = 0, index;
                    StringBuilder Builder;

                    for(index = 0; index < StringArray.Length; index++) {
                        Builder = new StringBuilder();

                        while(buffer[byteIndex] != 0)
                            Builder.Append(Encoding.UTF8.GetString(new byte[] { buffer[byteIndex++] })); // Just Take A Byte, You Fussy Prick

                        byteIndex++;
                        StringArray[index] = Builder.ToString();
                    }
                }


                // Check playgo-chunk.dat File Magic
                playgo.Read(buffer, 0, 4);
                if(BitConverter.ToInt32(buffer, 0) != 1869048944)
                    throw new InvalidDataException($"File Magic For .dat Wasn't Valid ([Expected: 70-6C-67-6F] != [Read: {BitConverter.ToString(buffer)}])");


                // Read Chunk Count
                playgo.Position = 0x0A;
                chunk_count = (byte)playgo.ReadByte();
                chunk_labels = new string[chunk_count];
#if Log
                WLog($"{chunk_count} Chunks in Project File", true);
#endif


                // Read Scenario Count, An Initialize Related Arrays
                playgo.Position = 0x0E;
                scenario_count = (byte)playgo.ReadByte();
                scenario_types = new int[scenario_count];
                scenario_labels = new string[scenario_count];
                initial_chunk_count = new int[scenario_count];
                scenario_chunk_range = new int[scenario_count];
#if Log
                WLog($"{scenario_count} Scenarios in Project File", true);
#endif


                // Read Default Scenario Id
                playgo.Position = 0x14;
                default_scenario_id = (byte)playgo.ReadByte();


                // Read Content ID
                buffer = new byte[36];
                playgo.Position = 0x40;
                playgo.Read(buffer, 0, 36);
                playgo_content_id = Encoding.UTF8.GetString(buffer);


                // Read Chunk Label Start Address From Pointer
                buffer = new byte[4];
                playgo.Position = 0xD0;
                playgo.Read(buffer, 0, 4);
                var chunk_label_pointer = BitConverter.ToInt32(buffer, 0);


                // Read Length Of Chunk Label Byte Array
                playgo.Position = 0xD4;
                playgo.Read(buffer, 0, 4);
                var chunk_label_array_length = BitConverter.ToInt32(buffer, 0);


                // Load Scenario(s)
                playgo.Position = 0xE0;
                playgo.Read(buffer, 0, 4);
                var scenarioPointer = BitConverter.ToInt32(buffer, 0);
                for(short index = 0; index < scenario_count; index++, scenarioPointer += 0x20) {

                    // Read Scenario Type
                    playgo.Position = scenarioPointer;
                    scenario_types[index] = (byte)playgo.ReadByte();

                    // Read Scenario initial_chunk_count
                    playgo.Position = (scenarioPointer + 0x14);
                    playgo.Read(buffer, 2, 2);
                    initial_chunk_count[index] = BitConverter.ToInt16(buffer, 2);

                    playgo.Read(buffer, 2, 2);
                    scenario_chunk_range[index] = BitConverter.ToInt16(buffer, 2);
                }

#if Log
                DLog($"Default Scenario Type = {scenario_types[default_scenario_id]}");
#endif

                // Load Scenario Label Array Byte Length
                buffer = new byte[2];
                playgo.Position = 0xF4;
                playgo.Read(buffer, 0, 2);
                var scenario_label_array_length = BitConverter.ToInt16(buffer, 0);


                // Load Scenario Label Pointer
                playgo.Position = 0xF0;
                buffer = new byte[4];
                playgo.Read(buffer, 0, 4);
                var scenario_label_array_pointer = BitConverter.ToInt32(buffer, 0);


                // Load Scenario Labels
                playgo.Position = scenario_label_array_pointer;
                buffer = new byte[scenario_label_array_length];
                playgo.Read(buffer, 0, buffer.Length);
                ConvertbufferToStringArray(scenario_labels);


                // Load Chunk Labels
                buffer = new byte[chunk_label_array_length];
                playgo.Position = chunk_label_pointer;
                playgo.Read(buffer, 0, buffer.Length);
                ConvertbufferToStringArray(chunk_labels);

                DLog('\n');
            }


            /* Parse param.sfo For Required .gp4 Variables.
            | =======================
            | Sets The Following Values:
            |
            | parameter_labels
            | app_ver
            | version
            | category
            | title_id
            | content_id (Read Again For Error Checking)
            |
            | ========================= | */
            using(var sfo = File.OpenRead($@"{gamedata_folder}\sce_sys\param.sfo")) {
                WLog($"Parsing param.sfo File\nPath: {gamedata_folder}\\sce_sys\\param.sfo", true);

                var buffer = new byte[12];
                int[] ParamOffsets, DataTypes, ParamLengths;


                // Check PSF File Magic, + 4 Bytes To Skip Label Base Ptr
                sfo.Read(buffer, 0, 12);
                if(BitConverter.ToInt64(buffer, 0) != 1104986460160)
                    throw new InvalidDataException($"File Magic For .sfo Wasn't Valid ([Expected: 00-50-53-46-01-01-00-00] != [Read: {BitConverter.ToString(buffer)}])");


                // Read Base Pointer For .sfo Parameters
                sfo.Read(buffer = new byte[4], 0, 4);
                var ParamVariablesPointer = BitConverter.ToInt32(buffer, 0);
                DLog($"Base Pointer For Parameters: {ParamVariablesPointer:X}");

                // Read PSF Parameter Count
                sfo.Read(buffer, 0, 4);
                var ParameterCount = BitConverter.ToInt32(buffer, 0);
                WLog($"{ParameterCount} Parameters In .sfo", true);


                // Initialize Arrays
                var SfoParams = new object[ParameterCount];
                var SfoParamLabels = new string[ParameterCount];
                DataTypes = new int[ParameterCount];
                ParamLengths = new int[ParameterCount];
                ParamOffsets = new int[ParameterCount];

                // Load Related Data For Each Parameter
                DLog($"Reading Param Data Starting At: {sfo.Position:X}");
                for(int i = 0; i < ParameterCount; ++i) { // Skip Param Offset Each Run

                    sfo.Position += 3; // Skip Label Offset

                    // Read And Check Data Type (4 = Int32, 2 = UTf8, 0 = Rsv4 )
                    if((DataTypes[i] = sfo.ReadByte()) == 2 || DataTypes[i] == 4) {
                        sfo.Read(buffer, 0, 4);
                        ParamLengths[i] = BitConverter.ToInt32(buffer, 0);

                        sfo.Position += 4;

                        sfo.Read(buffer, 0, 4);
                        ParamOffsets[i] = BitConverter.ToInt32(buffer, 0);
                    }
                }


                // Load Parameter Labels
                for(int index = 0, @byte; index < ParameterCount; ++index) {
                    var ByteList = new List<byte>();

                    // Read To End Of Label
                    for(; (@byte = sfo.ReadByte()) != 0; ByteList.Add((byte)@byte)) ;

                    SfoParamLabels[index] = Encoding.UTF8.GetString(ByteList.ToArray());
                }


                // Load Parameter Values
                sfo.Position = ParamVariablesPointer;
                DLog($"Reading Parameters Starting At: {sfo.Position:X}");
                
                for(int i = 0; i < ParameterCount; ++i) {

                    sfo.Position = ParamVariablesPointer + ParamOffsets[i];

                    DLog($"Sfo Param: {SfoParamLabels[i]} at {sfo.Position:X} ({ParamVariablesPointer:X} + {ParamOffsets[i]:X}) | len={ParamLengths[i]}");

                    sfo.Read(buffer = new byte[ParamLengths[i]], 0, ParamLengths[i]);

                    DLog($"Label: {SfoParamLabels[i]} ({i})");


                    // Datatype = string
                    if(DataTypes[i] == 2) {
                        if(ParamLengths[i] > 1 && buffer[ParamLengths[i] - 1] == 0)
                            SfoParams[i] = Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
                        else
                            SfoParams[i] = Encoding.UTF8.GetString(buffer);

                        DLog($"Param: {SfoParams[i]} ({i})");
                    }

                    // Datatype = Int32
                    else if(DataTypes[i] == 4) {
                        SfoParams[i] = BitConverter.ToInt32(buffer, 0);
                        DLog($"Param: {SfoParams[i]} ({i})");
                    }

                    DLog('\n');
                }


                // Store Required Parameters
                for(int i = 0; i < SfoParamLabels.Length; ++i) {
                    switch(SfoParamLabels[i]) {
                        case "APP_VER":
                            app_ver = (string)SfoParams[i];
                            continue;
                        case "CATEGORY":
                            category = (string)SfoParams[i];
                            continue;
                        case "CONTENT_ID":
                            content_id = (string)SfoParams[i];
                            continue;
                        case "VERSION":
                            version = (string)SfoParams[i];
                            continue;
                        case "TITLE_ID":
                            title_id = ((string)SfoParams[i]);
                            continue;

                        case "PUBTOOLINFO":
#if GUIExtras
                            Debug.WriteLine((string)SfoParams[i]);
                            var arr = ((string)SfoParams[i]).Split(',');
                            foreach(var v in arr)
                                Debug.WriteLine(v);
                            CreationDate = arr[0].Substring(arr[0].IndexOf('='));
                            SdkVersion = arr[1].Substring(arr[1].IndexOf('='));
                            storage_type = arr[2].Substring(arr[2].IndexOf('=')); // (digital25 / bd50)
#else

                            storage_type = ((string)SfoParams[i]).Split(',')[2]; // (digital25 / bd50)

#endif
                            continue;

#if GUIExtras
                        // Store Some Extra Things I May Use In My .gp4 GUI
                        case "APP_TYPE":
                            AppType = (int)SfoParams[i];
                            continue;

                        case "TITLE":
                            (AppTitles = new List<string>()).Add(AppTitle = ((string)SfoParams[i]));
                            continue;

                        default:
                            if(SfoParamLabels[i].Contains("Title_"))
                                AppTitles.Add((string)SfoParams[i]);
                            continue;

                        case "TARGET_APP_VER":
                            TargetAppVer = ((string)SfoParams[i]);
                            continue;
#endif
                    }
                }
            }


            // Get The Paths Of All Project Files & Subdirectories In The Given Project Folder. 
            var file_info = new DirectoryInfo(gamedata_folder).GetFiles(".", SearchOption.AllDirectories);
            file_paths = new string[file_info.Length];

            for(var index = 0; index < file_info.Length; index++)
                file_paths[index] = file_info[index].FullName;
            //\\



            if(Directory.Exists(GP4OutputPath))
                GP4OutputPath = $@"{GP4OutputPath}\{title_id}-{((category == "gd") ? "app" : "patch")}.gp4";


            // Check The Parsed Data For Any Potential Errors Before Building The .gp4 With It
            if(VerifyIntegrity)
                VerifyGP4(gamedata_folder, playgo_content_id, content_id, category, app_ver);



            // Create Base .gp4 Elements (Up To Chunk/Scenario Data)
            base_elements = CreateBaseElements(category, gp4_timestamp, content_id, Passcode, SourcePkgPath, app_ver, version, chunk_count, scenario_count);

            // Create The Actual .go4 Structure
            BuildGp4Elements(
                gp4.CreateXmlDeclaration("1.1", "utf-8", "yes"),
                psproject: base_elements[0],
                volume: base_elements[1],
                volume_type: base_elements[2],
                volume_id: base_elements[3],
                volume_ts: base_elements[4],
                package: base_elements[5],
                chunk_info: base_elements[6],
                files: CreateFilesElement(extra_files, file_paths, chunk_count, gamedata_folder),
                chunks: CreateChunksElement(chunk_labels, chunk_count),
                scenarios: CreateScenariosElement(default_scenario_id, scenario_count, initial_chunk_count, scenario_types, scenario_labels, scenario_chunk_range),
                rootdir: CreateRootDirectoryElement(gamedata_folder)
            );


            // Write The .go4 File To The Provided Folder / As The Provided Filename
            gp4.Save(GP4OutputPath);
#if Log
            WLog($"GP4 Creation Successful, File Saved As {GP4OutputPath}", false);
#endif
        }
        #endregion



        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Main Application Functions     --\\\
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        #region Main Application Functions


        private void VerifyGP4(string gamedata_folder, string playgo_content_id, string content_id, string category, string app_ver) {
            var Errors = string.Empty;
            int ErrorCount;

            if(!Directory.Exists(gamedata_folder))
                Errors += $"Could Not Find The Provided Game Data Directory.\n\nPath Provided:\n\"{gamedata_folder}\"\n\n";

            if(playgo_content_id != content_id)
                Errors += $"Content ID Mismatch Detected, Process Aborted\n[playgo-chunks.dat: {playgo_content_id} != param.sfo: {content_id}]\n\n";



            // Catch Conflicting Project Type Information
            if(category == "gp" && app_ver == "01.00")
                Errors += $"Invalid App Version For Patch Package. App Version Must Be Passed 1.00.\n\n";

            else if(category == "gd" && app_ver != "01.00")
                Errors += $"Invalid App Version For Application Package. App Version Was {app_ver}, Must Be 1.00.\n\n";



            if(Passcode.Length < 32)
                Errors += $"Invalid Password Length, Must Be A 32-Character String.\n\n";



            if(SourcePkgPath != null && SourcePkgPath[SourcePkgPath.Length - 1] == '\\')
                Errors += $"Invalid Base Application .pkg Path.\nDirectory \"{SourcePkgPath}\" Was Given.\n\n";



            //  No Errors Detected  \\
            if(Errors == string.Empty)
                return;


            //==================================================\\
            //| Throw An Exception If Any Errors Were Detected |\\
            //==================================================\\

            if((ErrorCount = (Errors.Length - Errors.Replace("\n\n", "").Length) / 2) == 1)
                Errors = $"The Following Error Was Found During The .gp4 Project Creation With Gamedata In: {gamedata_folder}.\n{Errors}";
            else
                Errors = $"The Following {ErrorCount} Errors Were Found During The .gp4 Project Creation With Gamedata In: {gamedata_folder}.\n{Errors}";


            WLog(Errors, true);


            throw new InvalidDataException(Errors);
        }


        /// <summary>
        /// Check Whether The filepath Containts A Blacklisted String.<br/>
        /// Checks Both A Default String[] Of Blacklisted Files, As Well As Any User Blacklisted Files/Folders.
        /// </summary>
        /// <returns> True If The File in filepath Shouldn't Be Included In The .gp4 </returns>
        private bool FileShouldBeExcluded(string filepath) {
            string filename = string.Empty;

            if(filepath.Contains('.'))
                filename = filepath.Remove(filepath.LastIndexOf(".")).Substring(filepath.LastIndexOf('\\') + 1); // Tf Am I Doing Here?


            foreach(var blacklisted_file_or_folder in DefaultBlacklist)
                if(filepath.Contains(blacklisted_file_or_folder)) {
#if Log
                    DLog($"Ignoring: {filepath}");
#endif
                    return true;
                }

            if(BlacklistedFilesOrFolders != null)
                foreach(var blacklisted_file_or_folder in BlacklistedFilesOrFolders) {
                    if(filepath.Contains(blacklisted_file_or_folder)) {
#if Log
                        DLog($"User Ignoring: {filepath}");
#endif
                        return true;
                    }
                }
            return false;
        }


        /// <summary>
        /// Check Whether Or Not The File At filepath Should Have Pfs Compression Enabled.<br/>
        /// This Is Almost Certainly Incomplete. Need More Brain Juice.
        /// </summary>
        /// <returns> True If Pfs Compression Should Be Enabled. </returns>
        private bool SkipPfsCompressionForFile(string filepath) {
            string[] Blacklist = new string[] {
                "sce_sys",
                "sce_module",
                ".elf",
                ".bin",
                ".prx",
                ".dll"
            };

            foreach(var file in Blacklist)
                if(filepath.Contains(file))
                    return true;

            return false;
        }


        /// <summary>
        /// Check Whether Or Not The File At filepath Should Have The "chunks" Attribute.<br/>
        /// [This Is Almost Certainly Incomplete. Need More Brain Juice.]
        /// </summary>
        /// <returns> True If The Chunk Attribute Should Be Skipped. </returns>
        private bool SkipChunkAttributeForFile(string filepath) {
            string[] Blacklist = new string[] {
                "sce_sys",
                "sce_module",
                ".bin"
            };

            foreach(var file in Blacklist)
                if(filepath.Contains(file))
                    return true;

            return false;
        }
        #endregion
    }
}
