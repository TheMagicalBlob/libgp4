using Microsoft.SqlServer.Server;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace libgp4 { // ver 0.1.1
    public class GP4Reader {

        public GP4Reader(string gp4_path) {
            gp4 = XmlReader.Create(gp4_path);
        }

        private XmlReader gp4;

        public void Read() {
            Output.Out(gp4.GetAttribute("psproject"));
        }
        public void Read(string Attribute) {
            Output.Out(gp4.GetAttribute(Attribute));
        }
    }


    public partial class GP4Creator {

        /// <summary> Initialize Class For Creating new .gp4 Files From Raw PS4 Gamedata 
        /// </summary>
        /// <param name="gamedata_folder"> The Folder Containing The Game's Executable And Game/System Data</param>
        public GP4Creator(string gamedata_folder) {
            gp4 = new XmlDocument();
            this.gamedata_folder = gamedata_folder;
            passcode = "00000000000000000000000000000000";
            gp4_declaration = gp4.CreateXmlDeclaration("1.1", "utf-8", "yes");
        }
        

        // GP4 Creation User Options
        public bool ignore_keystone = false;
        public string[] user_blacklist;
        public string
            passcode,
            pkg_source
        ;

        // GP4 Creation Variables
        private string gamedata_folder, gp4_output_directory;
        private int chunk_count, scenario_count, default_id, index = 0;
        private int[] scenario_types, scenario_chunk_range, initial_chunk_count;
        private string app_ver, version, content_id, title_id, category, timestamp;
        private string[] chunk_labels, parameter_labels, scenario_labels;
        private readonly string[] required_sfo_variables = new string[] { "APP_VER", "CATEGORY", "CONTENT_ID", "TITLE_ID", "VERSION" };


        private byte[] buffer;
        private TimeSpan internal_timestamp;



        #region Main Application Functions
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        ///--     Main Application Functions     --\\\
        ///////////////////////\\\\\\\\\\\\\\\\\\\\\\\
        

        /// <summary> Build A Base Game .gp4 With A Default Passcode, Outputting The .gp4 Right Outside The Gamedata Folder </summary>
        /// <returns> Success/Failure Status </returns>
        public string BuildGP4() {
            gp4_output_directory = gamedata_folder.Remove(gamedata_folder.LastIndexOf(@"\"));
            return BuildGP4(gp4_output_directory);
        }

        /// <summary> Build A Base Game .gp4 With A Default Passcode, Outputting The .gp4 To 
        /// </summary>
        /// <param name="gp4_output_directory"> Directory To Place The Finished .gp4 File </param>
        /// <returns> Success/Failure Status </returns>
        public string BuildGP4(string gp4_output_directory) {
            if(!Directory.Exists(gamedata_folder))
                return $"Could Not Find The Game Data Directory \"{gamedata_folder}\"";

            if(!Directory.Exists(gp4_output_directory)) {
                Output.Out($"Could Not Find The Selected .gp4 Output Directory\n({gp4_output_directory})");
                gp4_output_directory = gamedata_folder.Remove(gamedata_folder.LastIndexOf(@"\"));
                Output.Out($".gp4 Will Be Placed In {gp4_output_directory}");
            }
            if(category == "gp" && !File.Exists(pkg_source)) {
                if(pkg_source == null)
                    Output.Out("No Base Game Source .pkg Path Given For Patch .gp4, Using .pkg Name Default\n(.gp4 Will Expect Base Game .pkg To Be In The Same Directory As The .gp4)");

                Output.Out("Invalid Source .pkg Path Given, File Does Not Exist, Make Sure This Is Fixed Before .pkg Creation");
            }

            gp4_output_directory = gamedata_folder.Remove(gamedata_folder.LastIndexOf(@"\"));

            Output.Out("Starting .gp4 Creation");
            Output.Out($"Passcode: {passcode}");
            Output.Out($".gp4 Output Directory: {gp4_output_directory}");
            Output.Out($"Source .pkg Path: {pkg_source}");

            return BeginGP4BuildProcess(gamedata_folder, passcode, gp4_output_directory, pkg_source);
        }

        /// <summary> Internal Build Func Called By User Func </summary>
        private string BeginGP4BuildProcess(string gamedata_folder, string passcode, string gp4_output_directory, string pkg_source) {
            try {
                timestamp = $"{DateTime.Now.GetDateTimeFormats()[78]}"; // Sony One, For Consistency (though theirs technically only tracks the date, time is left as 00:00 but meh)
                internal_timestamp = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond); // Alternate One To Accurately Track Build Times

                // Get Necessary .gp4 Variables
                ParsePlaygoChunks(gamedata_folder);

                ParseSFO(gamedata_folder);

                string[] file_paths = GetProjectFilePaths(gamedata_folder);

                // Create Elements
                CreateBaseElements(category, timestamp, content_id, passcode, pkg_source, app_ver, version, chunk_count, scenario_count);

                CreateChunksElement(chunk_labels, chunk_count);
                CreateFilesElement(file_paths, gamedata_folder);
                CreateScenariosElement(scenario_labels);
                CreateRootDirectoryElement(gamedata_folder);

                return $"GP4 Creation Successful, Time Taken: {SaveElements(gp4_output_directory, internal_timestamp).Subtract(internal_timestamp)}".TrimEnd('0');
            }
            catch(Exception e) { return e.Message; }
        }



        /// <summary> Returns A String Array Containing The Paths For Every File In The Selected Gamedata Folder
        ///</summary>
        private string[] GetProjectFilePaths(string gamedata_folder) {
            DirectoryInfo directoryInfo = new DirectoryInfo(gamedata_folder);
            FileInfo[] file_info = directoryInfo.GetFiles(".", SearchOption.AllDirectories);

            string[] file_paths = new string[file_info.Length];
            for(index = 0; index < file_info.Length - 1; index++)
                file_paths[index] = file_info[index].FullName;

            return file_paths;
        }

        /// <summary> Check A Blacklist And User Blacklist And Exclude Any Files Who's Paths Contain A Blacklisted String
        ///</summary>
        /// <returns> True If The File in filepath Shouldn't Be Included In The .gp4 </returns>
        private bool FileShouldBeExcluded(string filepath) {
            string filename = string.Empty;
            if(filepath.Contains('.'))
                filename = filepath.Remove(filepath.LastIndexOf(".")).Substring(filepath.LastIndexOf('\\') + 1);

            string[] blacklist = new string[] {
                  // Drunk Canadian Guy
                    "right.sprx",
                    $"{(ignore_keystone ? @"sce_sys\keystone" : "@@")}",
                    "sce_discmap.plt",
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
                    Output.Out($"Ignoring: {filepath}");
#endif
                    return true;
                }

            if(user_blacklist != null)
                foreach(var blacklisted_file_or_folder in user_blacklist) {
                    if(filepath.Contains(blacklisted_file_or_folder)) {
#if DEBUG
                        Output.Out($"User Ignoring: {filepath}");
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
                ".txt",
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


        /// <summary> Parse playgo-chunks.dat And Param.sfo To Get Most Variables <br/><br/>
        /// chunk_count <br/>
        /// chunk_labels <br/>
        /// scenario_count <br/>
        /// scenario_types <br/>
        /// scenario_labels <br/>
        /// initial_chunk_count <br/>
        /// scenario_chunk_range <br/>
        /// default_id <br/>
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
                scenario_count = (byte)playgo_chunks_dat.ReadByte();
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
        /// app_ver <br/>
        /// version <br/>
        /// category <br/>
        /// title_id
        /// </summary>
        private void ParseSFO(string gamedata_folder) {
            using(var param_sfo = File.OpenRead($@"{gamedata_folder}\sce_sys\param.sfo")) {
                // Read Pointer For Array Of Parameter Names
                param_sfo.Position = 0x8;
                buffer = new byte[4];
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
        /// </summary>
        /// <param name="StringArray"> The Array To Write To, Already Initialized Before Calling This </param>
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
        #endregion
    }

    public static class Output {

        private static byte[] outputBuffer;
        private static int outputPosition = 0, outputReadPosition = 0;

        /// <summary> Output Method </summary>
        public static void Out(object o) {
            string s = o as string;
            var temp = outputBuffer;

            outputBuffer = new byte[outputPosition + 1 + Encoding.UTF8.GetBytes(s).Length];
            temp?.CopyTo(outputBuffer, 0);

            var currentOutput = Encoding.UTF8.GetBytes(s + (char)0x00);
            currentOutput.CopyTo(outputBuffer, outputPosition);
            outputPosition += currentOutput.Length;
        }

        /// <summary> Read A String From The Application Output And Advance (basic poc, will improve later)
        /// </summary>
        /// <returns> The Current Output String </returns>
        public static string OutputTest() {
            var newString = new StringBuilder();
            var stringLen = 0;


            if(outputReadPosition >= outputBuffer.Length) return "";
            for(int i = outputReadPosition; outputBuffer[i] != 0; i++) stringLen++;

            newString = newString.Append(Encoding.UTF8.GetString(outputBuffer, outputReadPosition, stringLen));
            outputReadPosition += stringLen + 1;

            return newString.ToString();
        }
    }

}
