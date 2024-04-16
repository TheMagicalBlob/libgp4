using System;
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
        /// <param name="AssertOnErrorFound">
        /// Determines Whether Or Not Assertions Are Thrown When Errors Are Found In The .gp4 File,<br/>
        /// Or Only When The Integrity Is Checked Through VerifyGP4Integrity()
        /// </param>
        public GP4Reader(string GP4Path, bool AssertOnErrorFound = false) {
            using(var gp4_file = new StreamReader(GP4Path)) {
                gp4_file.ReadLine();                  // Skip First Line To Avoid A Version Conflict
                ParseGP4(XmlReader.Create(gp4_file)); // Read All Data Someone Might Want To Grab From The .gp4 For Whatevr Reason
                
                gp4_path = GP4Path;
                assert_on_error_found = AssertOnErrorFound;
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
        private static readonly string assertion_message = $"An Error Occured When Reading Attribute From The Following Node: $|$\nMessage: %|%";

        /// <summary> Catch DLog Errors, Disabling Whichever Output Threw The Error.
        ///</summary>
        private static readonly bool[] enable_output_channel = new bool[] { true, true };

        /// <summary>
        /// Determines Whether Or Not Assertions Are Thrown When Errors Are Found In The .gp4 File,<br/>
        /// Or Only When The Integrity Is Checked Through VerifyGP4Integrity().
        /// </summary>
        private static bool assert_on_error_found;

        /// <summary> Backup Of The GP4's File Path For Various Methods
        ///</summary>
        private readonly string gp4_path;

        /// <summary> The Content Id In The Named File. (Read Both For Redundancy To Hopefully Catch A Mismatched Id)
        ///</summary>
        private string sfo_content_id, playgo_content_id;


        /// <summary> Console Logging Method. </summary>
        private static void DLog(object o) {

            if(enable_output_channel[0])
            try { Debug.WriteLine("#libgp4.dll: " + o); }
            catch(Exception) { enable_output_channel[0] = false; }

            if(!Console.IsOutputRedirected && enable_output_channel[1]) // Avoid Duplicate Writes
            try { Console.WriteLine("#libgp4.dll: " + o); }
            catch(Exception) { enable_output_channel[1] = false; }
        }

        /// <summary> Error Logging Method. </summary>
        private static void ELog(string NodeName, string Error) {
            var Message = (assertion_message.Replace("$|$", NodeName)).Replace("%|%", Error);

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
        private static string VerifyGP4Path(string GP4Path) {
            if(!File.Exists(GP4Path)) // Absolute And Second Relative Path Checks || (In Case The User Excluded The First Backslash, idfk Why)

                // Bad Path
                if(!File.Exists($@"{Directory.GetCurrentDirectory()}\{GP4Path}"))
                    if (assert_on_error_found)
                        throw new FileNotFoundException($"Invalid .gp4 Path Given, Please Provide A Valid Absolute Or Relative Path To A .gp4 Project File.\n(Given Path: {GP4Path})");
                    else return string.Empty;

                // Relative Path Checks Out
                else return $@"{Directory.GetCurrentDirectory()}\{GP4Path}";

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
            if((GP4Path = VerifyGP4Path(GP4Path)) != string.Empty)
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
            if((GP4Path = VerifyGP4Path(GP4Path)) != string.Empty)
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
            if ((GP4Path = VerifyGP4Path(GP4Path)) != string.Empty)
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
                    int Indexing;

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
                            if(!IsPatchProject && volume_type != "pkg_ps4_app") {
                                var Error = $"Unexpacted Volume Type For PS4 Package: {volume_type}";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }

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
                            if((BaseAppPkgPath = gp4.GetAttribute("app_path")) == string.Empty && IsPatchProject) {
                                var Error = $"Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }

                            ContentID = gp4.GetAttribute("content_id");
                            Passcode = gp4.GetAttribute("passcode");
                            storage_type = gp4.GetAttribute("storage_type");
                            app_type = gp4.GetAttribute("app_type");

                            // Check .gp4 Integrity
                            if(Passcode.Length < 32) {
                                var Error = $"Passcode Length Was Less Than 32 Characters.";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }
                            break;
                        }


                        // Parse The Expected Chunk And Scenario Counts From The "chunk_info" Node
                        case "chunk_info": {
                            ScenarioCount = int.Parse(gp4.GetAttribute("scenario_count"));
                            ChunkCount = int.Parse(gp4.GetAttribute("chunk_count"));

                            // Check .gp4 Integrity
                            if(ScenarioCount == 0 || ChunkCount == 0) {
                                var Error = $"Scenario And/Or Chunk Counts Were 0 (Scnarios: {ScenarioCount}, Chunks: {ChunkCount})";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }
                            break;
                        }


                        // Parse The Contents Of "chunks" Node And Add All Chunks To The Chunks Str Array
                        ///
                        case "chunks": {
                            gp4.Read();
                            Indexing = 0;
                            var Chunks = new List<string>();

                            // Read All Chunks
                            while(gp4.Read()) { //! remove log output
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "chunk") {
                                    if(gp4.LocalName == "chunks")
                                        break;

                                    continue;
                                }

                                Chunks.Add(gp4.GetAttribute("label"));
                                Indexing++;
                            }

                            // Check .gp4 Integrity
                            if(Indexing != ChunkCount) {
                                var Error = $"ERORR: \"chunk_count\" Attribute Did Not Match Amount Of Chunk Nodes ({Indexing} != {ChunkCount})";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }


                            this.Chunks = Chunks.ToArray();
                            break;
                        }


                        // Parse The Contents Of "Files" Node
                        ///
                        case "scenarios": {
                            Indexing = 0;
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
                                Indexing++;
                            }

                            // Check .gp4 Integrity
                            if(Indexing != ScenarioCount) {
                                var Error = $"\"scenario_count\" Attribute Did Not Match Amount Of Scenario Nodes ({Indexing} != {ScenarioCount})";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }

                            this.Scenarios = Scenarios.ToArray();
                            break;
                        }


                        // Parse The Contents Of "Files" Node
                        ///
                        case "files": {
                            gp4.Read();
                            Indexing = 0;
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
                                    Indexing++;
                                }
                            }

                            // *
                            if(Indexing > 0) {
                                var Error = $"Invalid File{(Indexing > 1 ? "s " : " ")}In .gp4 Project: {InvalidFiles}";

                                ELog(gp4.LocalName, Error);
                                if(assert_on_error_found)
                                    throw new InvalidDataException(Error);
                            }

                            this.Files = Files.ToArray();
                            break;
                        }

                        // Parse The Contents Of "Files" Node
                        ///
                        case "rootdir": {
                            Indexing = 0;
                            var Subfolders = new List<string>();
                            var SubfolderNames = new List<string>();

                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "dir") {// Check For End Of "dir" Nodes
                                    if(gp4.LocalName == "rootdir")
                                        break;

                                    continue;
                                }
                                SubfolderNames.Add(gp4.GetAttribute("targ_name"));

                                if(Indexing > 0 && gp4.Depth > Indexing) // Append Subfolder Name To Parent
                                    Subfolders.Add($@"{Subfolders[Subfolders.Count - 1]}\{SubfolderNames.Last()}");
                                else
                                    Subfolders.Add(SubfolderNames.Last()); // Add As New Folder

                                SubfolderCount++;
                                Indexing = gp4.Depth;
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



        /// <summary> Check Various Parts Of The .gp4 To Try And Find Any Possible Errors In The Project File.
        ///</summary>
        /// <returns> False If Nothing's Wrong. Throws An InvalidDataException If The Class Was Initialized With Assertions Enabled. True Otherwise. </returns>
        public bool CheckGP4Integrity() { //! FINISH ME
            var Errors = string.Empty;


            // Check "psproject" Node Attributes
            if(format != "gp4" || version != 1000)
                Errors += $"Invalid Attribute Values In \"psproject\" Node.\nFormat: {format} != gp4 || Version: {version} != 1000 \n\n";


            // Check "volume_id" Node Attributes
            if(volume_id != "PS4VOLUME")
                Errors += $"Invalid volume_id Attribute in .gp4, Should Be PS4VOLUME. (Read: {volume_id})\n\n";


            // Check For Invalid Volume Type Data
            if(volume_type != "pkg_ps4_app" && volume_type != "pkg_ps4_patch")
                Errors += $"Invalid Volume Type:\n ({volume_type})\n\n";

            
            // Check For Conflicting Volume Type Crap
            if(BaseAppPkgPath == string.Empty && IsPatchProject)
                Errors += "Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)\n\n";



            //===================================\\
            //| Check "package" Node Attributes |\\
            //===================================\\
            {
                if(!LazyContentIdCheck())
                    Errors += $"Content Id Mismatch In Project.\n .gp4{ContentID}\n .dat{playgo_content_id}\n .sfo: {sfo_content_id}\n\n";
                
                if((!IsPatchProject && storage_type != "digital50") || (IsPatchProject && storage_type != "digital25"))
                    Errors +=
                      $"Unexpected Storage Type For {(IsPatchProject ? "Patch" : "Full Game")} Package\n" +
                      $"(Expected: {(IsPatchProject ? "digital25" : "digital50")}\nRead: {storage_type})\n\n";

                if(app_type != "full")
                    Errors += $"Invalid Application Type In .gp4 Project\n(Expected: full\nRead: {app_type})\n\n";

                if(Passcode.Length != 32)
                    Errors += $"Incorrect Passcode Length, Must Be 32 Characters (Actual Length: {Passcode.Length})\n\n";
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


                // Check Scenarios
                int i = 1;
                foreach(var Sc in Scenarios) {
                    if(Sc.Id > Scenarios.Length - 1)
                        Errors += $"Scenario Id Was Out Of Bounds In Scenario #{i} ({DefaultScenarioId} > {Scenarios.Length - 1})\n\n";

                    if(Sc.Type != "mp" && Sc.Type != "sp")
                        Errors += $"Unexpected Scenario Type For Scenario #{i}\n(Expected: sp || mp\nRead: {Sc.Type})\n\n";

                    if(Sc.InitialChunkCount > Chunks.Length)
                        Errors += $"Initial Chunk Count For Scenario #{i} Was Larger Than The Actual Chunk Count {Sc.InitialChunkCount} > {Chunks.Length}\n\n";

                    if(Sc.Label == "")
                        Errors += $"Empty Scenario Label In Scenario #{i}\n\n";

                    var RangeChk = int.Parse(Sc.ChunkRange.Substring(Sc.ChunkRange.LastIndexOf('-')+1));
                    if(RangeChk >= ChunkCount)
                        Errors += $"Invalid Maximum Value For Chunk Range In Scenario #{i}\n ({RangeChk} >= {ChunkCount})\n\n";

                    RangeChk = int.Parse(Sc.ChunkRange.Remove(Sc.ChunkRange.LastIndexOf('-')));
                    if(RangeChk >= ChunkCount)
                        Errors += $"Invalid Minimum Value For Chunk Range In Scenario #{i}\n ({RangeChk} >= {ChunkCount})\n\n";

                    i++;
                }
            }
            //|===============================================================================================================================|\\



            // Check File List To Ensure No Files That Should Be Excluded Have Been Added To The .gp4
            foreach(var file in Files)
                if(project_file_blacklist.Contains(file))
                    Errors += $"Invalid File Included In .gp4 Project: {file}\n\n";




            //===========================\\
            //| No Errors Were Detected |\\
            //===========================\\
            if(Errors == string.Empty) {
                return false;
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
            
            if(assert_on_error_found)
                throw new InvalidDataException(Message);

            return true;
        }
        #endregion






        ////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     Static User Functions     --\\\
        ////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region Static User Functions

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


        /// <summary>
        /// Does What It Says On The Tin. (Lazily Copied And Stripped GP4Creator Code)<br/>
        /// Extra Line
        /// </summary>
        /// <returns> True If The Content Ids In The .gp4 Project File, playgo-chunks.dat, and param.sfo Match </returns>
        private bool LazyContentIdCheck() {
            var buff = new byte[36];
            string[] arr;
            string p1 = string.Empty, p2;
            StringBuilder Builder;
            int ind, byteIndex = 0;

            foreach(var file in Files)
                if(file.Contains("param.sfo"))
                    p1 = file;

            if(p1 == "") {
                DLog($"Param.sfo File Not Found In .gp4 File Listing");
                sfo_content_id = "MissingFromGp4File";
                goto JMP;
            }

            if(File.Exists(p1))
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

            else
                sfo_content_id = "FileNotPresent";


            JMP:
            if (File.Exists(p2 = $"{p1.Remove(p1.LastIndexOf('\\') + 1)}playgo-chunk.dat"))
                using(var playgo = File.OpenRead(p2)) {
                    playgo.Position = 0x40;
                    playgo.Read(buff, 0, 36);
                    playgo_content_id = Encoding.UTF8.GetString(buff);
                }
            else
                playgo_content_id = "FileNotPresent";


            return (ContentID == playgo_content_id && playgo_content_id == sfo_content_id);
        }

        /// <summary> Check Various Parts Of The .gp4 To Try And Find Any Possible Errors In The Project File.
        ///</summary>
        /// <returns> False If Nothing's Wrong. </returns>
        public static bool CheckGP4Integrity(string GP4Path) {
            //! Replace With Stripped Parse To Avoid Reading Unnecessary Data
            return new GP4Reader(GP4Path).CheckGP4Integrity();
        }
        #endregion
    }



    /// <summary> A Small Class For Creating new .gp4 Files From Raw PS4 Gamedata, With A Few Options Related To .pkg Creation.
    ///</summary>
    public partial class GP4Creator {

        /// <summary>
        /// Initialize A New Instance Of The GP4Creator Class With Which To Edit<br/>
        /// Parses The param.sfo &amp; playgo-chunks.dat As Well As The Project Files/Folders Without Building The .gp4.
        /// </summary>
        /// 
        /// <param name="GamedataFolder"> The Folder Containing The Gamedata To Create A .gp4 Project File For. </param>
        public GP4Creator(string GamedataFolder) {
            Passcode = "00000000000000000000000000000000";
            gamedata_folder = GamedataFolder;
            Keystone = true;

            gp4_declaration = (gp4 = new XmlDocument()).CreateXmlDeclaration("1.1", "utf-8", "yes");
        }


        /// <summary> Output Log Messages To A Custom Output Method (GP4Creator.LoggingMethod(string)), And/Or To The Console If Applicable </summary>
        private void WLog(object o) {
            Console.WriteLine(o as string);
            if (!Console.IsOutputRedirected)
                Debug.WriteLine(o as string);
            
            LoggingMethod(o as string);
        }

        /// <summary> Console Logging Method. </summary>
        private void DLog(object o) {
            LoggingMethod(o as string);

            if(enable_output_channel[0])
                try { Debug.WriteLine("#libgp4.dll: " + o); }
                catch(Exception) { enable_output_channel[0] = false; }

            if(!Console.IsOutputRedirected && enable_output_channel[1]) // Avoid Duplicate Writes
                try { Console.WriteLine("#libgp4.dll: " + o); }
                catch(Exception) { enable_output_channel[1] = false; }
        }

        /// <summary> Error Logging Method. </summary>
        private static void ELog(string NodeName, string Error) {
            var Message = (assertion_message.Replace("$|$", NodeName)).Replace("%|%", Error);

            try { Debug.WriteLine("libgp4.dll: " + Message); }
            catch(Exception) { enable_output_channel[0] = false; }

            if(!Console.IsOutputRedirected) // Avoid Duplicate Writes
                try { Console.WriteLine("libgp4.dll: " + Message); }
                catch(Exception) { enable_output_channel[1] = false; }
        }


        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        //--     User Options For GP4 Creation     --\\
        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        #region User Options For GP4 Creation

        /// <summary>
        /// Include Keystone In The .gp4's File Listing. <br/>
        /// (False By Default)
        /// </summary>
        public bool Keystone;

        /// <summary> Limit GP4 Log Verbosity. </summary>
        //public bool VerboseLogging; UNIMPLEMENTED
        
        /// <summary>
        /// The 32-bit Key Used To Encrypt The .pkg. Required For Extraction With orbis-pub-chk. <br/>
        /// (No Effect On Dumping, You Can Leave This Alone)
        /// </summary>
        public string Passcode;

        /// <summary>
        /// Necessary Variable For The Creation Of Patch A .pkg.<br/><br/>
        /// Path Of The Base Game Pkg You're Going To Install The Created Patch Package To. (Patch Packages Must Be Married To Their Intended Base)
        /// </summary>
        public string BaseGamePackage;

        /// <summary>
        /// An Array Containing The Names Of Any Files Or Folders That Are To Be Excluded From The .gp4 Project.
        /// </summary>
        public string[] BlacklistedFilesOrFolders;

        /// <summary>
        /// Optional Method To Use For Logging. [Function(string s)]
        /// </summary>
        public Action<object> LoggingMethod;
        #endregion



        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     GP4 Attributes / Values     --\\\
        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region GP4 Attributes / Values

        private static readonly string assertion_message = $"An Error Occured When Reading Attribute From The Following Node: $|$\nMessage: %|%";

        /// <summary> Catch DLog Errors, Disabling Whichever Output Threw The Error
        ///</summary>
        private static readonly bool[] enable_output_channel = new bool[] { true, true };


        /// <summary> Files That Aren't Meant To Be Added To A .pkg
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

        /// <summary>
        /// List Of All Files In The Project Folder (Excluding Blacklisted Files/Directories)
        /// </summary>
        private string[] file_paths;

        /// <summary> Root Gamedata Directory To Be Parsed (Should Contain At Least An Executable And sce_sys Folder)</summary>
        private string gamedata_folder;

        /// <summary> Id Of The Default Game Scenario </summary>
        private int default_scenario_id;

        private int chunk_count, scenario_count;
        private int[] scenario_types, scenario_chunk_range, initial_chunk_count;
        private string app_ver, version, playgo_content_id, content_Id, title_id, category;
        private string[] chunk_labels, scenario_labels;

        private readonly string[] required_sfo_variables = new string[] { "APP_VER", "CATEGORY", "CONTENT_ID", "TITLE_ID", "VERSION" };
        
        private static readonly string[] DEBUG_misc_sfo_variables = new string[] {
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
        #endregion



        /////////////////\\\\\\\\\\\\\\\\\
        ///--     User Functions     --\\\
        /////////////////\\\\\\\\\\\\\\\\\
        #region User Functions


        public void CreateNewGP4(string GamedataFolder) {

        }


        /// <summary> Build A New .gp4 Project File For The Provided Gamedata With The Current Options/Settings, And Save It In The Specified OutputDirectory.
        ///</summary>
        /// 
        /// <param name="OutputDirectory"> Folder In Which To Place The Newly Build .gp4 Project File. </param>
        /// <param name="ErrorChecking"> Set Whether Or Not To Abort The Creation Process If An Error Is Found That Would Cause .pkg Creation To Fail, Or Simply Log It To The Standard Console Output And/Or LogOutput(string) Action. </param>
        public bool CreateGP4(string OutputDirectory, bool ErrorChecking = true) {
            if(!Directory.Exists(gamedata_folder)) {
                DLog($"Could Not Find The Game Data Directory \"{gamedata_folder}\"");
                return false;
            }


            string GP4OutputPath;


            // Timestamp For GP4, Same Format Sony Used Though Sony's Technically Only Tracks The Date,
            // With The Time Left As 00:00, But Imma Just Add The Time. It Doesn't Break Anything).
            var gp4_timestamp = $"{DateTime.Now.GetDateTimeFormats()[78]}";

            // Alternate One To Accurately Track .gp4 Build Times
            var internal_gp4_timestamp = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);


            WLog($"Starting .gp4 Creation. \nPasscode: {Passcode}\nSource .pkg Path: {BaseGamePackage}");


            if(playgo_content_id != content_Id) {
                DLog($"Content ID Mismatch Detected, Process Aborted\n[playgo-chunks.dat: {playgo_content_id} != param.sfo: {content_Id}]");

                if(ErrorChecking)
                return false;
            }


            // Catch Conflicting Project Type Information
            if(category == "gp" && !File.Exists(BaseGamePackage)) {
                if(BaseGamePackage == null) {
                    WLog("No Base Game Source .pkg Path Given For Patch .gp4, Using .pkg Name Default\n(.gp4 Will Expect Base Game .pkg To Be In The Same Directory As The .gp4)");
                    if(ErrorChecking)
                        return false;
                }
                else
                    WLog("Warning: Invalid Source .pkg Path Given. (File Does Not Currently Exist)");

            }


            // Create Elements
            CreateBaseElements(category, gp4_timestamp, content_Id, Passcode, BaseGamePackage, app_ver, version, chunk_count, scenario_count);
            CreateChunksElement(chunk_labels, chunk_count);
            CreateFilesElement(file_paths, gamedata_folder);
            CreateScenariosElement(scenario_labels);
            CreateRootDirectoryElement(gamedata_folder);

            
            gp4.Save(GP4OutputPath = $@"{OutputDirectory}\{title_id}-{((category == "gd") ? "app" : "patch")}.gp4");

            WLog($".gp4 Saved In {GP4OutputPath}");


            DLog($"GP4 Creation Successful, Time Taken: {WriteElementsToGP4(internal_gp4_timestamp).Subtract(internal_gp4_timestamp)}".TrimEnd('0'));
            return true;
        }
        #endregion


        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     User Functions (Static)     --\\\
        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region User Functions (Static)


        /// <summary> Build A New .gp4 Project File For A Patch Package With The Default Settings Out Of The Given ProjectFolder, And Save It To The Specified OutputDirectory </summary>
        /// 
        /// <param name="ProjectFolder"> The Folder Containing The Game Data. </param>
        /// <param name="OutputPath"> The Folder Or Full Path To Save The GP4 In/As.<br/>Providing A Folder Will Have The Program Name The .gp4 In The Default Naming Scheme For .gp4 Project Files.</param>
        /// <param name="FileOrFolderBlacklist"></param>
        /// <param name="SourcePkgPath"></param>
        /// 
        /// <returns> True If No Errors Were Found During The Creation Process. </returns>
        public static bool CreateNewGP4(string ProjectFolder, string OutputPath, string SourcePkgPath, string[] FileOrFolderBlacklist = null) {

            var gp4 = new GP4Creator(ProjectFolder) {
                BlacklistedFilesOrFolders = FileOrFolderBlacklist,
                BaseGamePackage = SourcePkgPath
            };
            gp4.ParseProjectFilesForGP4Variables(ProjectFolder);
            gp4.CreateGP4(OutputPath);


            return true;
        }
        /// <summary> Build A New .gp4 Project File For A Patch Package With The Default Settings Out Of The Given ProjectFolder, And Save It To The Specified OutputDirectory </summary>
        /// 
        /// <param name="ProjectFolder"> The Folder Containing The Game Data. </param>
        /// <param name="OutputPath"> The Folder Or Full Path To Save The GP4 In/As.<br/>Providing A Folder Will Have The Program Name The .gp4 In The Default Naming Scheme For .gp4 Project Files.</param>
        /// <param name="FileOrFolderBlacklist"></param>
        /// 
        /// <returns> True If No Errors Were Found During The Creation Process. </returns>
        public static bool CreateNewGP4(string ProjectFolder, string OutputPath, string[] FileOrFolderBlacklist = null) {

            var gp4 = new GP4Creator(ProjectFolder) {
                BlacklistedFilesOrFolders = FileOrFolderBlacklist,
                Keystone = false
            };
            gp4.ParseProjectFilesForGP4Variables(ProjectFolder);
            gp4.CreateGP4(OutputPath);


            return (gp4.ParseProjectFilesForGP4Variables(ProjectFolder) && gp4.CreateGP4(OutputPath));
        }

        #endregion




        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Main Application Functions     --\\\
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        #region Main Application Functions

        private void ParseBasicVariables() {

        }

        /// <summary>
        /// Parse Param.sfo &amp; playgo-chunks.dat And Load Required Variables,<br/>
        /// As Well As Save The Paths Of The Project Files &amp; Subdirectories In The Given Project Folder. <br/><br/>
        /// Sets The Following Values:
        /// <br/><br/>
        /// =============        <br/>
        /// <para>playgo-chunks.dat:</para>
        /// chunk_count          <br/>
        /// chunk_labels         <br/>
        /// scenario_count       <br/>
        /// scenario_types       <br/>
        /// scenario_labels      <br/>
        /// initial_chunk_count  <br/>
        /// scenario_chunk_range <br/>
        /// default_id           <br/>
        /// content_id      <br/><br/>
        /// 
        /// =============        <br/>
        /// <para>param.sfo:   </para>
        /// parameter_labels     <br/>
        /// app_ver              <br/>
        /// version              <br/>
        /// category             <br/>
        /// title_id             <br/>
        /// content_id (Read Again For Error Checking)<br/><br/>
        /// =============        <br/>
        /// </summary>
        private bool ParseProjectFilesForGP4Variables(string GamedataFolder) {

            // Parse playgo-chunks.dat For Various Scenario/Chunk Related Parameters, As Well As The Content Id To Check Against The .sfo
            using(var playgo = File.OpenRead($@"{GamedataFolder}\sce_sys\playgo-chunk.dat")) {

                byte[] buffer;

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

                // Read Chunk Count
                playgo.Position = 0x0A;
                chunk_count = (byte)playgo.ReadByte();
                chunk_labels = new string[chunk_count];

                // Read Scenario Count
                playgo.Position = 0x0E;
                scenario_count = (byte)playgo.ReadByte();
                scenario_types = new int[scenario_count];
                scenario_labels = new string[scenario_count];
                initial_chunk_count = new int[scenario_count];
                scenario_chunk_range = new int[scenario_count];

                // Read Default Scenario Id
                playgo.Position = 0x14;
                default_scenario_id = (byte)playgo.ReadByte();

                // Read Content ID
                buffer = new byte[36];
                playgo.Position = 0x40;
                playgo.Read(buffer, 0, 36);
                content_Id = Encoding.UTF8.GetString(buffer);

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
            }


            // Parse The Application's param.sfo For The Remaining Required Parameters
            using(var sfo = File.OpenRead($@"{GamedataFolder}\sce_sys\param.sfo")) {

                byte[] buffer;
                int[] ParamOffsets, DataTypes, ParamLengths;


                // Check PSF File Magic, + 4 Bytes To Skip Label Base Ptr
                sfo.Read(buffer = new byte[12], 0, 12);
                if(BitConverter.ToInt64(buffer, 0) != 1104986460160)
                    throw new InvalidDataException($"File Magic For .sfo Wasn't Valid ([Expected: 00-50-53-46-01-01-00-00] != [Read: {BitConverter.ToString(buffer)}])");


                // Read Base Pointer For .pkg Parameters
                sfo.Read(buffer = new byte[4], 0, 4);
                var ParamVariablesPointer = BitConverter.ToInt32(buffer, 0);

                // Read PSF Parameter Count
                sfo.Read(buffer, 0, 4);
                var ParameterCount = BitConverter.ToInt32(buffer, 0);


                // Initialize Arrays
                var SfoParams = new object[ParameterCount];
                var SfoParamLabels = new string[ParameterCount];
                DataTypes = new int[ParameterCount];
                ParamLengths = new int[ParameterCount];
                ParamOffsets = new int[ParameterCount];


                // Load Related Data For Each Parameter
                for(int i = 0; i < ParameterCount; i++) {

                    sfo.Position += 3; // Skip Label Offset

                    // Read And Check Data Type (4 = Int32, 2 = UTf8, 0 = Rsv4 )
                    if((DataTypes[i] = sfo.ReadByte()) == 2 || DataTypes[i] == 4) {
                        sfo.Read(buffer, 0, 4);
                        ParamLengths[i] = BitConverter.ToInt32(buffer, 0);

                        sfo.Read(buffer, 0, 4);
                        ParamOffsets[i] = BitConverter.ToInt32(buffer, 0);
                    }

                    sfo.Position += 4; // Skip Param Offset
                }

                // Load Parameter Labels
                for(int index = 0, @byte; index < ParameterCount; index++) {
                    var ByteList = new List<byte>();

                    // Read To End Of Label
                    for(; (@byte = sfo.ReadByte()) != 0; ByteList.Add((byte)@byte)) ;

                    SfoParamLabels[index] = Encoding.UTF8.GetString(ByteList.ToArray());
                }


                for(int i = 0; i < ParameterCount; ParamVariablesPointer += ParamOffsets[i++]) {
                    sfo.Position = ParamVariablesPointer;

                    sfo.Read(buffer = new byte[ParamLengths[i]], 0, ParamLengths[i]);

                    Debug.WriteLine($"\nLabel: {SfoParamLabels[i]}");


                    // String
                    if(DataTypes[i] == 2) {
                        if(ParamLengths[i] > 1 && buffer[ParamLengths[i] - 1] == 0)
                            SfoParams[i] = Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
                        else
                            SfoParams[i] = Encoding.UTF8.GetString(buffer);


                        if(((string)SfoParams[i])[0] == 0)
                            SfoParams[i] = "Empty String";

                        Debug.WriteLine($"Param: {SfoParams[i]}");
                    }

                    // Int32
                    else if(DataTypes[i] == 4) {
                        SfoParams[i] = BitConverter.ToInt32(buffer, 0);
                        Debug.WriteLine($"Param: {SfoParams[i]}");
                    }
                }

                foreach(string param in SfoParams)
                    switch(param) {
                        case "APP_TYPE":
                            break;
                        case "APP_VER":
                            app_ver = param;
                            break;
                        case "CATEGORY":
                            category = param;
                            break;
                        case "CONTENT_ID":
                            content_Id = param;
                            break;
                        case "VERSION":
                            version = param;
                            break;

                            /* Might Do Something WIth These Later
                            case "FORMAT":
                            case "PARENTAL_LEVEL":
                            case "PUBTOOLINFO":
                            case "PUBTOOLMINVER":
                            case "PUBTOOLVER":
                            case "SYSTEM_VER":
                            case "TARGET_APP_VER":
                            case "TITLE":
                            case "TITLE_00":
                            case "TITLE_ID":
                                break;
                            */
                    }
            }


            // Get Project Files And Subfolders
            var file_info = new DirectoryInfo(GamedataFolder).GetFiles(".", SearchOption.AllDirectories);
            file_paths = new string[file_info.Length];

            for(var index = 0; index < file_info.Length; index++)
                Debug.WriteLine("!>>-" + (file_paths[index] = file_info[index].FullName));

            return true;
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


            foreach(var blacklisted_file_or_folder in project_file_blacklist)
                if(filepath.Contains(blacklisted_file_or_folder)) {
#if DEBUG
                    WLog($"Ignoring: {filepath}");
#endif
                    return true;
                }

            if(BlacklistedFilesOrFolders != null)
                foreach(var blacklisted_file_or_folder in BlacklistedFilesOrFolders) {
                    if(filepath.Contains(blacklisted_file_or_folder)) {
#if DEBUG
                        WLog($"User Ignoring: {filepath}");
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
        private bool SkipCompression(string filepath) {
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
        private bool SkipChunkAttribute(string filepath) {
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
