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
namespace libgp4 { // ver 1.16.70


    ///////////\\\\\\\\\\\\
    //  GP4READER CLASS  \\
    ///////////\\\\\\\\\\\\

    /// <summary> Small Class For Grabbing Data From .gp4 Projects.<br/><br/>
    /// Create A New Instance To Parse And Return All Relevant Data From The .gp4 File.<br/>
    /// Alternatively, Individual Variables Can Be Read Alone Through Static Methods.  <br/>
    ///</summary>
    public class GP4Reader {

        /// <summary>
        /// Create A New Instance Of The GP4Reader Class To Parse And Return All Relevant Data From.<br/><br/>
        /// Skips Passed The First Line To Avoid A Version Conflict (The XmlReader Class Doesn't Like 1.1),<br/>
        /// Then Parses The Given Project File For All Relevant .gp4 Data. Also Checks For Possible Errors.
        /// </summary>
        /// <param name="gp4_path"> The Absolute Path To The .gp4 Project File </param>
        public GP4Reader(string gp4_path) {
            using(var gp4_file = new StreamReader(gp4_path)) {
                gp4_file.ReadLine();                  // Skip First Line To Avoid A Version Conflict
                ParseGP4(XmlReader.Create(gp4_file)); // Read All Data Someone Might Want To Grab From The .gp4 For Whatevr Reason
                this.gp4_path = gp4_path;
            }
        }

        /// <summary>
        /// Small Struct For Scenario Node Attributes.
        /// <br/><br/>
        /// Members:
        /// <br/> [string] Type
        /// <br/> [string] Label
        /// <br/> [int] Id
        /// <br/> [int] InitialChunkCount
        /// <br/> [string] ChunkRange
        ///</summary>
        public struct Scenario { //! TRY TO ADD MORE DESCRIPTIVE SUMMARIES
            public Scenario(XmlReader gp4Stream) {
                Type = gp4Stream.GetAttribute("type");
                Label = gp4Stream.GetAttribute("label");
                Id = int.Parse(gp4Stream.GetAttribute("id"));
                InitialChunkCount = int.Parse(gp4Stream.GetAttribute("initial_chunk_count"));
                ChunkRange = gp4Stream.ReadInnerXml();
            }

            /// <summary>
            /// The Type Of The Selected Game Scenario. (E.G. sp / mp)
            /// </summary>
            public string Type;

            /// <summary>
            /// The Label/Name Of The Selected Game Scenario.
            /// </summary>
            public string Label;

            /// <summary>
            /// Id Of The Selected Game Scenario.
            /// </summary>
            public int Id;

            /// <summary>
            /// The Initial Chunk Count Of The Selected Game Scenario.
            /// </summary>
            public int InitialChunkCount;

            ///  <summary>
            /// The Chunk Range For The Selected Game Scenario.
            /// 
            /// <br/><br/>
            ///  NOTE: No Idea If My Own Tool Creates This Attribute Properly,
            ///  <br/>But If It Doesn't, It Won't Matter Unless You're Trying To Burn The Created .pkg To A Disc, Anyway
            ///  </summary>
            public string ChunkRange;
        }

        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Internal Variables / Methods     --\\\
        ////////////////////////\\\\\\\\\\\\\\\\\\\\\\\\

        /// <summary> Files That Aren't Meant To Be Added To A .pkg
        ///</summary>
        private readonly string[] ProjectFileBlacklist = new string[] {
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

        /// <summary> Catch DLog Errors And Disable It If It Fails
        ///</summary>
        private static readonly bool[] EnableConsoleLogging = new bool[] { true, true };

        /// <summary> Backup Of The GP4's File Path For Various Methods
        ///</summary>
        private readonly string gp4_path;

        /// <summary> Console Logging
        ///</summary>
        private static void DLog(object o) {
#if DEBUG
            if(EnableConsoleLogging[0])
            try { Debug.WriteLine("libgp4.dll: " + o); }
            catch(Exception) { EnableConsoleLogging[0] = false; }

            if(EnableConsoleLogging[1])
            try { Console.WriteLine("libgp4.dll: " + o); }
            catch(Exception) { EnableConsoleLogging[1] = false; }
#endif
        }


        /// <summary> 
        ///</summary>
        /// <param name="GP4Path"> An Absolute Or Relative Path To The .gp4 File</param>
        /// <returns></returns>
        private static string VerifyGP4Path(string GP4Path) {
            // Absolute And Second Relative Path Checks || (In Case The User Excluded The First Backslash, idfk Why)
            if(!File.Exists(GP4Path))
                // Bad Path
                if(!File.Exists($@"{Directory.GetCurrentDirectory()}\{GP4Path}")) {
                    DLog($"An Invalid .gp4 Project Path Was Given. ({GP4Path} Not Found)");
                    return string.Empty;
                }
                // Relative Path Checks Out
                else {
                    GP4Path = $@"{Directory.GetCurrentDirectory()}\{GP4Path}";
                    DLog($"Using Relative .gp4 Path: {GP4Path}");
                }

            return GP4Path;
        }

        /// <summary> Open A .gp4 at The Specified Path and Read The The Value Of "AttributeName" At The Given Parent Node </summary>
        /// 
        /// <param name="GP4Path"> The Absolute Or Relative Path To The .gp4 File</param>
        /// <param name="NodeName"> Attribute's Parent Node </param>
        /// <param name="AttributeName"> The Attribute To Read And Return </param>
        /// <returns> The Value Of The Specified Attribute If Successfully Found; string.Empty Otherwise </returns>
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

        /// <summary> Open A .gp4 at The Specified GP4Path and Read All Nodes With AttributeName
        ///</summary>
        /// <param name="GP4Path"> An Absolute Or Relative Path To The .gp4 File</param>
        /// <param name="NodeName"> Attribute's Parent Node To Cgeck Every Instance Of </param>
        /// <param name="AttributeName"> The Attribute To Read And Add To The List </param>
        /// <returns> A String Array Containing The Value Of Each Instance Of AttributeName, Or An Empty String Array Otherwise </returns>
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

        /// <summary> Open A .gp4 at The Specified GP4Path and Read The Inner Xml Contents Of The Specified Node
        ///</summary>
        /// <param name="GP4Path"> An Absolute Or Relative Path To The .gp4 File</param>
        /// <param name="NodeName"> Attribute's Parent Node To Cgeck Every Instance Of </param>
        /// <returns> The Inner Xml Data Of the Given Node If It's Successfully Found; string.Empty Otherwise </returns>
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


        /////////////////////////////////////|





        #region FULL PARSE
        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     GP4 Attributes / Values     --\\\ (User Accessed)
        //////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region GP4 Attributes / Values

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
        /// (Label &amp; Title ID)
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


        /// <summary> Array Of Scenario Data For The .gp4 Project
        ///</summary>
        public Scenario[] Scenarios { get; private set; }
        public int ScenarioCount    { get; private set; }

        /// <summary> The Default Scenario ID Of The .gp4 Project </summary>
        public int DefaultScenarioID { get; private set; }
        #endregion



        /// <summary>
        /// Parse Each .gp4 Node For Relevant PS4 .gp4 Project Data.
        /// <br/><br/>
        /// Throws An InvalidDataException If Invalid Attribute Values Are Found.
        /// <br/><br/>
        /// (For Example: If Invalid Files Have Been Added To The .gp4's File Listing, Or There Are Conflicting Variables For<br/>
        /// The Package Type Because Of gengp4.exe Being A Pile Of Arse.)
        /// </summary>
        /// <exception cref="InvalidDataException"/>
        private void ParseGP4(XmlReader gp4) {
            do {
                var NT = gp4.MoveToContent();
                if(NT == XmlNodeType.Element) {
                    int ind;

                    switch(gp4.LocalName) { //! REMOVE SWITCH CASE
                        default: continue;

                        // Get The Current Package Type
                        case "volume_type":
                            string PackageType;

                            IsPatchProject = (PackageType = gp4.ReadInnerXml()) == "pkg_ps4_patch";

                            // Check .gp4 Integrity
                            if(!IsPatchProject && PackageType != "pkg_ps4_app")
                                throw new InvalidDataException($"Unexpacted Volume Type For PS4 Package: {PackageType}");

                            break;

                        // Parse The Contents Of "package" Node
                        case "package":
                            if((BaseAppPkgPath = gp4.GetAttribute("app_path")) == string.Empty && IsPatchProject) // Check .gp4 Integrity
                                throw new InvalidDataException($"Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)");

                            ContentID = gp4.GetAttribute("content_id");
                            Passcode = gp4.GetAttribute("passcode");
                            break;

                        // Parse The Expected Chunk And Scenario Counts From The "chunk_info" Node
                        case "chunk_info":
                            ScenarioCount = int.Parse(gp4.GetAttribute("scenario_count"));
                            ChunkCount = int.Parse(gp4.GetAttribute("chunk_count"));
                            break;


                        // Parse The Contents Of "chunks" Node And Add All Chunks To The Chunks Str Array
                        ///
                        case "chunks": {
                            gp4.Read();
                            var Chunks = new List<string>();
                            ind = 0;

                            // Read All Chunks
                            while(gp4.Read()) { //! remove log output
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "chunk") {
                                    if(gp4.LocalName == "chunks")
                                        break;

                                    continue;
                                }

                                Chunks.Add(gp4.GetAttribute("label"));
                                ind++;
                            }

                            // Check .gp4 Integrity
                            if(ind != ChunkCount)
                                throw new InvalidDataException($"ERORR: \"chunk_count\" Attribute Did Not Match Amount Of Chunk Nodes ({ind} != {ChunkCount})");

                            this.Chunks = Chunks.ToArray();
                            break;
                        }

                        // Parse The Contents Of "Files" Node
                        ///
                        case "scenarios": {
                            var Scenarios = new List<Scenario>();
                            ind = 0;

                            DefaultScenarioID = int.Parse(gp4.GetAttribute("default_id"));

                            // Read All Scenarios
                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "scenario") { // Check For End Of "scenario" Nodes
                                    if(gp4.LocalName == "scenarios")
                                        break;

                                    continue;
                                }

                                Scenarios.Add(new Scenario(gp4));
                                ind++;
                            }

                            // Check .gp4 Integrity
                            if(ind != ScenarioCount)
                                throw new InvalidDataException($"\"scenario_count\" Attribute Did Not Match Amount Of Scenario Nodes ({ind} != {ScenarioCount})");

                            this.Scenarios = Scenarios.ToArray();
                            break;
                        }

                        // Parse The Contents Of "Files" Node
                        ///
                        case "files": {
                            gp4.Read();
                            var Files = new List<string>();
                            ind = 0;
                            var InvalidFiles = string.Empty;

                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "file") {// Check For End Of "file" Nodes
                                    if(gp4.LocalName == "files")
                                        break;

                                    continue;
                                }

                                Files.Add(gp4.GetAttribute("targ_path"));
                                FileCount++;

                                // * Check .gp4 Integrity
                                if(ProjectFileBlacklist.Contains(Files.Last())) {
                                    InvalidFiles += $"{Files.Last()}\n";
                                    ind++;
                                }
                            }

                            // *
                            if(ind > 0)
                                throw new InvalidDataException($"Invalid File{(ind > 1 ? "s " : " ")}In .gp4 Project: {InvalidFiles}");

                            this.Files = Files.ToArray();
                            break;
                        }

                        // Parse The Contents Of "Files" Node
                        ///
                        case "rootdir": {
                            var SubfolderNames = new List<string>();
                            var Subfolders = new List<string>();
                            ind = 0;

                            while(gp4.Read()) {
                                if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "dir") {// Check For End Of "dir" Nodes
                                    if(gp4.LocalName == "rootdir")
                                        break;

                                    continue;
                                }
                                SubfolderNames.Add(gp4.GetAttribute("targ_name"));

                                if(ind > 0 && gp4.Depth > ind) // Append Subfolder Name To Parent
                                    Subfolders.Add($@"{Subfolders[Subfolders.Count - 1]}\{SubfolderNames.Last()}");
                                else
                                    Subfolders.Add(SubfolderNames.Last()); // Add As New Folder

                                SubfolderCount++;
                                ind = gp4.Depth;
                            }

                            if(false)
                                throw new InvalidDataException($"Unimplemented Error Message");

                            this.SubfolderNames = SubfolderNames.ToArray();
                            this.Subfolders = Subfolders.ToArray();
                            break;
                        }
                    }
                }
                else if(NT == XmlNodeType.EndElement && gp4.LocalName == "psproject")
                    return;
            }
            while(gp4.Read());
        }


        /// <summary> Check Various Parts Of The .gp4 To Try And Find Any Possible Errors In The Project File.
        ///</summary>
        /// <returns> False If Nothing's Wrong. Throws An InvalidDataException Otherwise. </returns>
        public bool CheckGP4Integrity() { //! FINISH ME
            var Errors = string.Empty;

            // Check Passcode Validity
            if(Passcode.Length != 32)
                Errors += $"Incorrect Passcode Length, Must Be 32 Characters (Actual Length: {Passcode.Length})\n\n";


            // Check For Conflicting Volume Type Data
            if(BaseAppPkgPath == string.Empty && IsPatchProject)
                Errors += "Conflicting Volume Type Data For PS4 Package.\n(Base .pkg Path Not Found In .gp4 Project, But The Volume Type Was Patch Package)\n\n";


            // Check Chunk Data Integrity
            if(Chunks.Length != ChunkCount)
                Errors += "Number Of Chunks Listed In .gp4 Project Doesn't Match ChunkCount Attribute\n\n";


            // Check Scenario Data Integrity
            if(Scenarios.Length != ScenarioCount)
                Errors += "Number Of Scenarios Listed In .gp4 Project Doesn't Match ScenarioCount Attribute\n\n";


            // Check File List To Ensure No Files That Should Be Excluded Have Been Added To The .gp4
            foreach(var file in Files)
                if(ProjectFileBlacklist.Contains(file))
                    Errors += $"Invalid File Included In .gp4 Project: {file}\n\n";



            // Throw An Exception If Any Errors Were Detected
            if(Errors != string.Empty) {
                string Message;
                var ErrorCount = (Errors.Length - Errors.Replace("\n\n", "").Length) / 2;

                if(ErrorCount == 1)
                    Message = $"The Following Error Was Found In The .gp4 Project File At {gp4_path}:\n{Errors}";
                
                else
                    Message = $"The Following {ErrorCount} Errors Were Found In The .gp4 Project File At {gp4_path}:\n{Errors}";

                throw new InvalidDataException(Message);
            }

            return false; // No Errors Were Found
        }
        #endregion


        ////////////////////\\\\\\\\\\\\\\\\\\\\\
        ///--     Static User Functions     --\\\
        ////////////////////\\\\\\\\\\\\\\\\\\\\\
        #region Static User Functions

        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> The Passcode The .pkg Will Be Encrypted With (Pointless On fpkg's, Does Not Prevent Dumping, Only orbis-pub-chk extraction)
        ///</returns>
        public static string GetPkgPasscode(string GP4Path) => GetAttribute(GP4Path, "package", "passcode");

        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> The Path Of The Base Game Package The .gp4 Project File's Patch Is To Be Married With
        ///</returns>
        public static string GetBasePkgPath(string GP4Path) => GetAttribute(GP4Path, "package", "app_path");

        /// <summary>s</summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Project Files Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFileListing(string GP4Path) => GetAttributes(GP4Path, "file", "targ_path");

        /// <summary> Read And Return the Names Of All Subfolders Located Wiithin The Project Folder </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Just The Names Of All Subfolders Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFolderNames(string GP4Path) => GetAttributes(GP4Path, "dir", "targ_name");

        /// <summary> Read And Return All Subfolders Located Wiithin The Project Folder </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> A String Array Containing Full Paths To All Subfolders Listed In The .gp4 Project
        ///</returns>
        public static string[] GetFolderListing(string GP4Path) { // Rework Copied Code
            var SubfolderNames = new List<string>();
            var Subfolders = new List<string>();
            var ind = 0;

            using(StreamReader GP4File = new StreamReader(GP4Path)) {
                GP4File.ReadLine(); // Skip Version Confilct
                
                using(var gp4 = XmlReader.Create(GP4File)) {

                    while(gp4.Read()) {

                        // Check For End Of "dir" Nodes
                        if(gp4.MoveToContent() != XmlNodeType.Element || gp4.LocalName != "dir") {
                            if(gp4.LocalName == "rootdir" && gp4.NodeType == XmlNodeType.EndElement)
                                break;

                            continue;
                        }

                        SubfolderNames.Add(gp4.GetAttribute("targ_name"));

                        if(ind > 0 && gp4.Depth > ind) // Append Subfolder Name To Parent
                            Subfolders.Add($@"{Subfolders[Subfolders.Count - 1]}\{SubfolderNames.Last()}");
                        
                        else
                            Subfolders.Add(SubfolderNames.Last()); // Add As New Folder

                        ind = gp4.Depth;
                    }
                }

                return Subfolders.ToArray();
            }
        }

        /// <summary>
        /// Check Whether Or Not The .gp4 Project File Is For A Patch .pkg
        /// </summary>
        /// <param name="GP4Path"> Absolute Path To The .gp4 File Being Checked </param>
        /// <returns> True If The Volume Type Is pkg_ps4_patch. </returns>
        public static bool IsPatchPackage(string GP4Path) { return GetInnerXMLData(GP4Path, "volume_type") == "pkg_ps4_patch"; }

        #endregion
    }




    ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
    //  GP4Creator: .gp4 Project Creation Class \\
    ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\

    /// <summary> A Small Class For Building With A Few Options Related To .pkg Creation, And A Build Function
    ///</summary>
    public partial class GP4Creator {

        /// <summary> Initialize Class For Creating new .gp4 Files From Raw PS4 Gamedata </summary>
        /// <param name="GamedataFolder"> The Folder Containing The Game's Executable And Game/System Data </param>
        public GP4Creator(string GamedataFolder) {
            gp4 = new XmlDocument();
            this.gamedata_folder = GamedataFolder;
            Passcode = "00000000000000000000000000000000";
            gp4_declaration = gp4.CreateXmlDeclaration("1.1", "utf-8", "yes");
        }



        /////////////////\\\\\\\\\\\\\\\\
        //  GP4 Creation User Options  \\
        /////////////////\\\\\\\\\\\\\\\\
        /// <summary> Exclude Keystone From .gp4<br/>(False By Default) </summary>
        public bool IgnoreKeystone;
        /// <summary> Limit GP4 Log Verbosity </summary>
        public bool LimitLogOutput;
        /// <summary> An Array Of Strings With Witch To Exclude Files From The .gp4 </summary>
        public string[] UserBlacklist;
        /// <summary>  The 32-bit Key Used To Encrypt The .pkg, Needed To Extract With PC Tools<br/>(No Effect On Dumping, You Can Leave This Alone)</summary>
        public string Passcode;
        /// <summary> Necessary Variably For Patch .pkg Creation. <br/>
        /// Path Of The Base Game Pkg You're Going To Install The </summary>
        public string SourcePkgPath;
        /// <summary> Text Box Control To Use As A Log For The Creation Process </summary>
        public RichTextBox LogTextBox;


        ///////////////\\\\\\\\\\\\\\\
        //  GP4 Creation Variables  \\
        ///////////////\\\\\\\\\\\\\\\
        private string gamedata_folder;
        private int chunk_count, scenario_count, default_id, index = 0; //! Why am I using a global fucking index?
        private int[] scenario_types, scenario_chunk_range, initial_chunk_count;
        private string app_ver, version, content_id, title_id, category;
        private string[] chunk_labels, parameter_labels, scenario_labels;
        private readonly string[] required_sfo_variables = new string[] { "APP_VER", "CATEGORY", "CONTENT_ID", "TITLE_ID", "VERSION" };

        private byte[] buffer;



        #region User Functions
        /////////////////\\\\\\\\\\\\\\\\\
        ///--     User Functions     --\\\
        /////////////////\\\\\\\\\\\\\\\\\

        /// <summary> Build A Base Game .gp4 With The Current Settings </summary>
        /// <returns> Success/Failure Status </returns>
        public string BuildGP4() {
            return GP4Sart(gamedata_folder, Passcode, SourcePkgPath);
        }

        /// <summary> Build A Base Game .gp4 With The Current Settings, And Save It To The Specified Directory </summary>
        /// <returns> Success/Failure Status </returns>
        public string BuildGP4(string gp4_output_directory) {
            var Result = GP4Sart(gamedata_folder, Passcode, SourcePkgPath);
            var newGP4Path = $@"{gp4_output_directory}\{title_id}-{(category == "gd" ? "app" : "patch")}.gp4";
            gp4.Save(newGP4Path);
            WLog($".gp4 Saved In {newGP4Path}");
            return Result;
        }

        /// <summary> Save The .gp4 To gp4_output_directory
        ///</summary>
        public void SaveGP4(string gp4_output_directory) {
            var newGP4Path = $@"{gp4_output_directory}\{title_id}-{(category == "gd" ? "app" : "patch")}.gp4";
            gp4.Save(newGP4Path);
            WLog($".gp4 Saved In {newGP4Path}");
        }

        /// <summary> Save The .gp4 To The Gamedata FOlder's Parent Folder
        ///</summary>
        public void SaveGP4() {
            var gp4_output_directory = gamedata_folder.Remove(gamedata_folder.LastIndexOf(@"\"));
            var newGP4Path = $@"{gp4_output_directory}\{title_id}-{(category == "gd" ? "app" : "patch")}.gp4";
            gp4.Save(newGP4Path);
            WLog($".gp4 Saved In {newGP4Path}");
        }
        #endregion




        #region Main Application Functions
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Main Application Functions     --\\\
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\

        /// <summary> Output Log Messages To A Specified RichTextBox Control, And/Or To The Console If Applicable </summary>
        private void WLog(object o) {
            string s = o as string;

            Console.WriteLine(s);

            LogTextBox?.AppendText($"{s}\n");
            LogTextBox?.ScrollToCaret();
        }

        /// <summary> Build A Base Game .gp4 With A Default Passcode, Outputting The .gp4 To 
        ///</summary>
        /// <returns> Success/Failure Status </returns>
        private string GP4Sart(string gamedata_folder, string Passcode, string SourcePkgPath) {

            if(!Directory.Exists(gamedata_folder))
                return $"Could Not Find The Game Data Directory \"{gamedata_folder}\"";


            // Timestamp For GP4, Same Format Sony Used Though Sony's Technically Only Tracks The Date,
            // With The Time Left As 00:00, But Imma Just Add The Time. It Doesn't Break Anything).
            var gp4_timestamp = $"{DateTime.Now.GetDateTimeFormats()[78]}";

            // Alternate One To Accurately Track .gp4 Build Times
            var internal_gp4_timestamp = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);


            WLog("Starting .gp4 Creation");
            WLog($"Passcode: {Passcode}");
            WLog($"Source .pkg Path: {SourcePkgPath}");

            // Get Necessary .gp4 Variables
            ParsePlaygoChunks(gamedata_folder);
            var ID1 = content_id;
            ParseSFO(gamedata_folder);
            if(ID1 != content_id)
                return $"Content ID Mismatch Detected, Process Aborted\n.dat: {ID1} | .sfo: {content_id}";

            if(category == "gp" && !File.Exists(SourcePkgPath)) {
                if(SourcePkgPath == null)
                    WLog("No Base Game Source .pkg Path Given For Patch .gp4, Using .pkg Name Default\n(.gp4 Will Expect Base Game .pkg To Be In The Same Directory As The .gp4)");
                else
                    WLog("Invalid Source .pkg Path Given, File Does Not Exist, Make Sure This Is Fixed Before .pkg Creation");
            }


            string[] file_paths
                = // Look Ma, No Indendtation Issues
            GetProjectFilePaths(gamedata_folder);


            // Create Elements
            CreateBaseElements(category, gp4_timestamp, content_id, Passcode, SourcePkgPath, app_ver, version, chunk_count, scenario_count);
            CreateChunksElement(chunk_labels, chunk_count);
            CreateFilesElement(file_paths, gamedata_folder);
            CreateScenariosElement(scenario_labels);
            CreateRootDirectoryElement(gamedata_folder);

            return $"GP4 Creation Successful, Time Taken: {WriteElementsToGP4(internal_gp4_timestamp).Subtract(internal_gp4_timestamp)}".TrimEnd('0');
        }

        /// <summary> Parse playgo-chunks.dat And Param.sfo To Get Most Variables <br/><br/>
        /// chunk_count          <br/>
        /// chunk_labels         <br/>
        /// scenario_count       <br/>
        /// scenario_types       <br/>
        /// scenario_labels      <br/>
        /// initial_chunk_count  <br/>
        /// scenario_chunk_range <br/>
        /// default_id           <br/>
        /// content_id
        /// </summary>
        private void ParsePlaygoChunks(string gamedata_folder) {
            using(var playgo_chunks_dat = File.OpenRead($@"{gamedata_folder}\sce_sys\playgo-chunk.dat")) {

                // Read Chunk Count
                playgo_chunks_dat.Position = 0x0A;
                chunk_count = (byte)playgo_chunks_dat.ReadByte();
                chunk_labels = new string[chunk_count];

                // Read Scenario Count
                playgo_chunks_dat.Position = 0x0E;
                scenario_count       = (byte)playgo_chunks_dat.ReadByte();
                scenario_types       = new int   [scenario_count];
                scenario_labels      = new string[scenario_count];
                initial_chunk_count  = new int   [scenario_count];
                scenario_chunk_range = new int   [scenario_count];

                // Read Default Scenario Id
                playgo_chunks_dat.Position = 0x14;
                default_id = (byte)playgo_chunks_dat.ReadByte();

                // Read Content ID Here Instead Of The .sfo Because Meh, User Has Bigger Issues If Those Aren't the Same
                buffer = new byte[36];
                playgo_chunks_dat.Position = 0x40;
                playgo_chunks_dat.Read(buffer, 0, 36);
                content_id = Encoding.UTF8.GetString(buffer);

                // Read Chunk Label Start Address From Pointer
                buffer = new byte[4];
                playgo_chunks_dat.Position = 0xD0;
                playgo_chunks_dat.Read(buffer, 0, 4);
                var chunk_label_pointer = BitConverter.ToInt32(buffer, 0);

                // Read Length Of Chunk Label Byte Array
                playgo_chunks_dat.Position = 0xD4;
                playgo_chunks_dat.Read(buffer, 0, 4);
                var chunk_label_array_length = BitConverter.ToInt32(buffer, 0);


                // Load Scenario(s)
                playgo_chunks_dat.Position = 0xE0;
                playgo_chunks_dat.Read(buffer, 0, 4);
                var scenarioPointer = BitConverter.ToInt32(buffer, 0);
                for(index = 0; index < scenario_count; index++) {
                    // Read Scenario Type
                    playgo_chunks_dat.Position = scenarioPointer;
                    scenario_types[index] = (byte)playgo_chunks_dat.ReadByte();

                    // Read Scenario initial_chunk_count
                    playgo_chunks_dat.Position = (scenarioPointer + 0x14);
                    playgo_chunks_dat.Read(buffer, 2, 2);
                    initial_chunk_count[index] = BitConverter.ToInt16(buffer, 2);
                    playgo_chunks_dat.Read(buffer, 2, 2);
                    scenario_chunk_range[index] = BitConverter.ToInt16(buffer, 2);
                    scenarioPointer += 0x20;
                }

                // Load Scenario Label Array Byte Length
                buffer = new byte[2];
                playgo_chunks_dat.Position = 0xF4;
                playgo_chunks_dat.Read(buffer, 0, 2);
                var scenario_label_array_length = BitConverter.ToInt16(buffer, 0);

                // Load Scenario Label Pointer
                playgo_chunks_dat.Position = 0xF0;
                buffer = new byte[4];
                playgo_chunks_dat.Read(buffer, 0, 4);
                var scenario_label_array_pointer = BitConverter.ToInt32(buffer, 0);

                // Load Scenario Labels
                playgo_chunks_dat.Position = scenario_label_array_pointer;
                buffer = new byte[scenario_label_array_length];
                playgo_chunks_dat.Read(buffer, 0, buffer.Length);
                ConvertbufferToStringArray(scenario_labels);


                // Load Chunk Labels
                buffer = new byte[chunk_label_array_length];
                playgo_chunks_dat.Position = chunk_label_pointer;
                playgo_chunks_dat.Read(buffer, 0, buffer.Length);
                ConvertbufferToStringArray(chunk_labels);
            }
        }

        /// <summary> Parse param.sfo For Various Parameters <br/>
        /// parameter_labels <br/>
        /// app_ver          <br/>
        /// version          <br/>
        /// category         <br/>
        /// title_id
        /// </summary>
        private void ParseSFO(string gamedata_folder) {
            using(var param_sfo = File.OpenRead($@"{gamedata_folder}\sce_sys\param.sfo")) {

                // Read Pointer For Array Of Parameter Names
                buffer = new byte[4];
                param_sfo.Position = 0x8;
                param_sfo.Read(buffer, 0, 4);
                var sfo_param_name_array_pointer = BitConverter.ToInt32(buffer, 0);

                // Read Base Pointer For .pkg Parameters
                param_sfo.Position = 0x0C;
                param_sfo.Read(buffer, 0, 4);
                var sfo_parameters_pointer = BitConverter.ToInt32(buffer, 0);

                // Read Parameter Name Array Length And Initialize Offset Array
                param_sfo.Position = 0x10;
                param_sfo.Read(buffer, 0, 4);
                var sfo_param_name_array_length = BitConverter.ToInt32(buffer, 0);
                int[] sfo_params_offsets = new int[sfo_param_name_array_length];

                // Load Parameter Names
                buffer = new byte[sfo_parameters_pointer - sfo_param_name_array_pointer];
                parameter_labels = new string[sfo_param_name_array_length];
                param_sfo.Position = sfo_param_name_array_pointer;
                param_sfo.Read(buffer, 0, buffer.Length);
                ConvertbufferToStringArray(parameter_labels);

                // Load Parameter Offsets
                param_sfo.Position = 0x20;
                buffer = new byte[4];
                for(index = 0; index < sfo_param_name_array_length; param_sfo.Position += (0x10 - buffer.Length)) {
                    param_sfo.Read(buffer, 0, 4);
                    sfo_params_offsets[index] = sfo_parameters_pointer + BitConverter.ToInt32(buffer, 0);
                    index++;
                }


                // Load The Rest Of The Required .pkg Variables From param.sfo
                for(index = 0; index < sfo_param_name_array_length; index++)
                    if(required_sfo_variables.Contains(parameter_labels[index])) { // Ignore Variables Not Needed For .gp4 Project Creation

                        param_sfo.Position = sfo_params_offsets[index];
                        buffer = new byte[4];

                        switch(parameter_labels[index]) { // I'm Too Tired to think of a more elegant solution right now. If it works, it works

                            case "APP_VER":
                                buffer = new byte[5];
                                param_sfo.Read(buffer, 0, 5);
                                app_ver = Encoding.UTF8.GetString(buffer);
                                break;
                            case "CATEGORY": // gd / gp
                                param_sfo.Read(buffer, 0, 2);
                                category = Encoding.UTF8.GetString(buffer, 0, 2);
                                break;
                            case "CONTENT_ID":
                                buffer = new byte[36];
                                param_sfo.Read(buffer, 0, 36);
                                content_id = Encoding.UTF8.GetString(buffer);
                                break;
                            case "TITLE_ID":
                                buffer = new byte[9];
                                param_sfo.Read(buffer, 0, 9);
                                title_id = Encoding.UTF8.GetString(buffer);
                                break;
                            case "VERSION": // Remaster
                                buffer = new byte[5];
                                param_sfo.Read(buffer, 0, 5);
                                version = Encoding.UTF8.GetString(buffer);
                                break;
                        }
                    }
            }
        }


        /// <summary> Parses A Byte Array And Converts Data To A String Array, With Strings Seperated By Null Bytes
        ///</summary>
        /// <param name="StringArray"> The Initialized Array To Write To</param>
        private void ConvertbufferToStringArray(string[] StringArray) {
            int byteIndex = 0;
            StringBuilder Builder;

            for(index = 0; index < StringArray.Length; index++) {
                Builder = new StringBuilder();

                while(buffer[byteIndex] != 0)
                    Builder.Append(Encoding.UTF8.GetString(new byte[] { buffer[byteIndex++] })); // Just Take A Byte, You Fussy Prick

                byteIndex++;
                StringArray[index] = Builder.ToString();
            }
        }

        /// <summary> Returns A String Array Containing The Paths For Every File In The Selected Gamedata Folder
        ///</summary>
        private string[] GetProjectFilePaths(string gamedata_folder) {
            DirectoryInfo directoryInfo = new DirectoryInfo(gamedata_folder);
            FileInfo[] file_info = directoryInfo.GetFiles(".", SearchOption.AllDirectories);

            string[] file_paths = new string[file_info.Length];
            for(index = 0; index < file_info.Length; index++)
                file_paths[index] = file_info[index].FullName;

            return file_paths;
        }

        /// <summary> Check A Blacklist And User Blacklist And Exclude Any Files Who's Paths Contain A Blacklisted String
        ///</summary>
        /// <returns> True If The File in filepath Shouldn't Be Included In The .gp4 </returns>
        private bool FileShouldBeExcluded(string filepath) {
            string filename = string.Empty;
            if(filepath.Contains('.'))
                filename = filepath.Remove(filepath.LastIndexOf(".")).Substring(filepath.LastIndexOf('\\') + 1); // Tf Am I Doing Here?

            string[] blacklist = new string[] {
                  // Drunk Canadian Guy
                    "right.sprx",
                    $"{(IgnoreKeystone ? @"sce_sys\keystone" : "@@")}",
                    "sce_discmap.plt",
                    "sce_discmap_patch.plt",
                    @"sce_sys\playgo-chunk",
                    @"sce_sys\psreserved.dat",
                    $@"sce_sys\{filename}.dds",
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

            foreach(var blacklisted_file_or_folder in blacklist)
                if(filepath.Contains(blacklisted_file_or_folder)) {
#if DEBUG
                    WLog($"Ignoring: {filepath}");
#endif
                    return true;
                }

            if(UserBlacklist != null)
                foreach(var blacklisted_file_or_folder in UserBlacklist) {
                    if(filepath.Contains(blacklisted_file_or_folder)) {
#if DEBUG
                        WLog($"User Ignoring: {filepath}");
#endif
                        return true;
                    }
                }
            return false;
        }

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
