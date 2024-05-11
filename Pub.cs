using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;
using System.Collections.Generic;

namespace libgp4 {
#pragma warning disable CS1587

    public partial class GP4Creator {

        ///////////////\\\\\\\\\\\\\\\
        //--     User Options     --\\
        ///////////////\\\\\\\\\\\\\\\
        #region User Options

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

        /// <summary>
        /// Target Application Version.
        /// </summary>
        public string TargetAppVer { get; private set; }

        /// <summary>
        /// Creation Date Of The param.sfo File.
        /// </summary>
        public string CreationDate { get; private set; }

        /// <summary>
        /// The PS4/Orbis SDK Version Of The Application.
        /// </summary>
        public string SdkVersion { get; private set; }
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
#if Log
            WLog($"Starting .gp4 Creation.", false);
            WLog($"PKG Passcode: {Passcode}\nSource .pkg Path: {SourcePkgPath}\n", true);
#endif

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
#if Log
                WLog($"Parsing playgo-chunk.dat File\nPath: {gamedata_folder}\\sce_sys\\playgo-chunk.dat", true);
#endif

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

#if Log
                DLog('\n');
#endif
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
#if Log
                WLog($"Parsing param.sfo File\nPath: {gamedata_folder}\\sce_sys\\param.sfo", true);
#endif

                var buffer = new byte[12];
                int[] ParamOffsets, DataTypes, ParamLengths;


                // Check PSF File Magic, + 4 Bytes To Skip Label Base Ptr
                sfo.Read(buffer, 0, 12);
                if(BitConverter.ToInt64(buffer, 0) != 1104986460160)
                    throw new InvalidDataException($"File Magic For .sfo Wasn't Valid ([Expected: 00-50-53-46-01-01-00-00] != [Read: {BitConverter.ToString(buffer)}])");


                // Read Base Pointer For .sfo Parameters
                sfo.Read(buffer = new byte[4], 0, 4);
                var ParamVariablesPointer = BitConverter.ToInt32(buffer, 0);
#if Log
                DLog($"Base Pointer For Parameters: {ParamVariablesPointer:X}");
#endif

                // Read PSF Parameter Count
                sfo.Read(buffer, 0, 4);
                var ParameterCount = BitConverter.ToInt32(buffer, 0);
#if Log
                WLog($"{ParameterCount} Parameters In .sfo", true);
#endif


                // Initialize Arrays
                var SfoParams = new object[ParameterCount];
                var SfoParamLabels = new string[ParameterCount];
                DataTypes = new int[ParameterCount];
                ParamLengths = new int[ParameterCount];
                ParamOffsets = new int[ParameterCount];

                // Load Related Data For Each Parameter
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
                for(int i = 0; i < ParameterCount; ++i) {

                    sfo.Position = ParamVariablesPointer + ParamOffsets[i];

                    sfo.Read(buffer = new byte[ParamLengths[i]], 0, ParamLengths[i]);

#if Log
                    DLog($"Label: {SfoParamLabels[i]}");
#endif

                    // Datatype = string
                    if(DataTypes[i] == 2) {
                        if(ParamLengths[i] > 1 && buffer[ParamLengths[i] - 1] == 0)
                            SfoParams[i] = Encoding.UTF8.GetString(buffer, 0, buffer.Length - 1);
                        else
                            SfoParams[i] = Encoding.UTF8.GetString(buffer);

#if Log
                        DLog($"Param: {SfoParams[i]}");
#endif
                    }

                    // Datatype = Int32
                    else if(DataTypes[i] == 4) {
                        SfoParams[i] = BitConverter.ToInt32(buffer, 0);
#if Log
                        DLog($"Param: {SfoParams[i]}");
#endif
                    }

#if Log
                    DLog('\n');
#endif
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
            var file_info = new DirectoryInfo(gamedata_folder).GetFiles(".", SearchOption.AllDirectories); // The Period Is Needed To Search Every Single File/Folder Recursively, I Don't Even Know Why It Works That Way.
            file_paths = new string[file_info.Length];

            for(var index = 0; index < file_info.Length; ++index) {
                file_paths[index] = file_info[index].FullName;
            }
            file_info = null;


            if(Directory.Exists(GP4OutputPath)) {
                GP4OutputPath = $@"{GP4OutputPath}\{title_id}-{((category == "gd") ? "app" : "patch")}.gp4";
            }


            // Check The Parsed Data For Any Potential Errors Before Building The .gp4 With It
            if(VerifyIntegrity) {
                VerifyGP4(gamedata_folder, playgo_content_id, content_id, category, app_ver);
            }




            // Create Base .gp4 Elements (Up To Chunk/Scenario Data)
            gp4 = new XmlDocument();
            var base_elements = CreateBaseElements(category, gp4_timestamp, content_id, Passcode, SourcePkgPath, app_ver, version, chunk_count, scenario_count, gp4);

            // Create The Actual .go4 Structure
            BuildGp4Elements(
                gp4,
                gp4.CreateXmlDeclaration("1.1", "utf-8", "yes"),
                psproject: base_elements[0],
                volume: base_elements[1],
                volume_type: base_elements[2],
                volume_id: base_elements[3],
                volume_ts: base_elements[4],
                package: base_elements[5],
                chunk_info: base_elements[6],
                files: CreateFilesElement(extra_files, file_paths, chunk_count, gamedata_folder, gp4),
                chunks: CreateChunksElement(chunk_labels, chunk_count, gp4),
                scenarios: CreateScenariosElement(default_scenario_id, scenario_count, initial_chunk_count, scenario_types, scenario_labels, scenario_chunk_range, gp4),
                rootdir: CreateRootDirectoryElement(gamedata_folder, gp4)
            );


            // Write The .go4 File To The Provided Folder / As The Provided Filename
            gp4.Save(GP4OutputPath);

#if Log
            WLog($"GP4 Creation Successful, File Saved As {GP4OutputPath}", false);
#endif
        }
        #endregion
    }
}