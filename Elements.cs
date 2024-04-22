using libgp4;
using System;
using System.IO;
using System.Xml;

namespace libgp4 {
    public partial class GP4Creator {
    #pragma warning disable CS1587


        /////////////////////\\\\\\\\\\\\\\\\\\\
        ///--     GP4 ELEMENT CREATION     --\\\
        /////////////////////\\\\\\\\\\\\\\\\\\\
        #region GP4 ELEMENT CREATION

        /// <summary>
        ///  Create Base .gp4 Elements (Up To Chunk/Scenario Data).
        ///   <br/><br/>
        /// - psproject <br/>
        /// - volume <br/>
        /// - volume_type <br/>
        /// - volume_id <br/>
        /// - volume_ts <br/>
        /// - package <br/>
        /// - chunk_info <br/>
        /// </summary>
        private XmlNode[] CreateBaseElements(string category, string timestamp, string content_id, string passcode, string base_package, string app_ver, string version, int chunk_count, int scenario_count) {
            var psproject = gp4.CreateElement("psproject");
            psproject.SetAttribute("fmt", "gp4");
            psproject.SetAttribute("version", "1000");

            var volume = gp4.CreateElement("volume");

            var volume_type = gp4.CreateElement("volume_type");
            volume_type.InnerText = $"pkg_{((category == "gd")? "ps4_app" : "ps4_patch")}";

            var volume_id = gp4.CreateElement("volume_id");
            volume_id.InnerText = "PS4VOLUME";

            var volume_ts = gp4.CreateElement("volume_ts");
            volume_ts.InnerText = timestamp;

            var package = gp4.CreateElement("package");
            package.SetAttribute("content_id", content_id);
            package.SetAttribute("passcode", passcode);
            package.SetAttribute("storage_type", ((category == "gp")? "digital25" : "digital50"));
            package.SetAttribute("app_type", "full");

            if(category == "gp")
                package.SetAttribute("app_path", base_package ?? $"{content_id}-A{app_ver}-V{version}.pkg");

            var chunk_info = gp4.CreateElement("chunk_info");
            chunk_info.SetAttribute("chunk_count", $"{chunk_count}");
            chunk_info.SetAttribute("scenario_count", $"{scenario_count}");

            return new XmlNode[] { psproject, volume, volume_type, volume_id, volume_ts, package, chunk_info };
        }


        /// <summary>
        ///   Create "files" Element, Containing File Destination And Source Paths, Along With Whether To Enable PFS Compression
        /// </summary>
        private XmlNode CreateFilesElement(string[][] extra_files, string[] file_paths, int chunk_count, string gamedata_folder) {
            var files = gp4.CreateElement("files");

            for(var index = 0; index < file_paths.Length; index++) {
                if(FileShouldBeExcluded(file_paths[index]))
                    continue;

                var file = gp4.CreateElement("file");
                file.SetAttribute("targ_path", (file_paths[index].Replace(gamedata_folder + "\\", string.Empty)).Replace('\\', '/'));
                file.SetAttribute("orig_path", file_paths[index]);

                if(!SkipPfsCompressionForFile(file_paths[index]))
                    file.SetAttribute("pfs_compression", "enable");
                
                if(!SkipChunkAttributeForFile(file_paths[index]) && chunk_count - 1 != 0)
                    file.SetAttribute("chunks", $"0-{chunk_count - 1}");
                
                files.AppendChild(file);
            }

            if (extra_files != null)
                for(var index = 0; index < extra_files.Length; index++) {
                    if(FileShouldBeExcluded(extra_files[index][1]))
                        continue;

                    var file = gp4.CreateElement("file");
                    file.SetAttribute("targ_path", (extra_files[index][0].Replace(gamedata_folder + "\\", string.Empty)).Replace('\\', '/'));
                    file.SetAttribute("orig_path", extra_files[index][1]);

                    if(!SkipPfsCompressionForFile(extra_files[index][1]))
                        file.SetAttribute("pfs_compression", "enable");

                    if(!SkipChunkAttributeForFile(extra_files[index][1]) && chunk_count - 1 != 0)
                        file.SetAttribute("chunks", $"0-{chunk_count - 1}");

                    files.AppendChild(file);
                }

            return files;
        }

        /// <summary>
        ///    Create "rootdir" Element Containing The Game's File Structure through A Listing Of Each Directory And Subdirectory
        /// </summary>
        private XmlNode CreateRootDirectoryElement(string gamedata_folder) {
            var rootdir = gp4.CreateElement("rootdir");

            void AppendSubfolder(string dir, XmlElement node) {
                foreach(string folder in Directory.GetDirectories(dir)) {
                    var subdir = gp4.CreateElement("dir");
                    subdir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));

                    if(folder.Substring(folder.LastIndexOf('\\') + 1) != "about")
                        node.AppendChild(subdir);

                    if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, subdir);
                }
            }

            foreach(string folder in Directory.GetDirectories(gamedata_folder)) {
                var dir = gp4.CreateElement("dir");
                dir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));

                rootdir.AppendChild(dir);
                if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, dir);
            }

            return rootdir;
        }

        /// <summary>
        ///   Create "chunks" Element
        /// </summary>
        private XmlNode CreateChunksElement(string[] chunk_labels, int chunk_count) {
            var chunks = gp4.CreateElement("chunks");

            for(int chunk_id = 0; chunk_id < chunk_count; chunk_id++) {
                var chunk = gp4.CreateElement("chunk");
                chunk.SetAttribute("id", $"{chunk_id}");

                if(chunk_labels[chunk_id] == "") //  I Hope This Fix Works For Every Game...
                    chunk.SetAttribute("label", $"Chunk #{chunk_id}");
                else
                    chunk.SetAttribute("label", $"{chunk_labels[chunk_id]}");
                chunks.AppendChild(chunk);
            }
            return chunks;
        }

        /// <summary>
        ///   Create "scenarios" Element
        /// </summary>
        private XmlNode CreateScenariosElement(int default_scenario_id, int scenario_count, int[] initial_chunk_count, int[] scenario_types, string[] scenario_labels, int[] scenario_chunk_range) {
            var scenarios = gp4.CreateElement("scenarios");
            scenarios.SetAttribute("default_id", $"{default_scenario_id}");

            for(var index = 0; index < scenario_count; index++) {
                var scenario = gp4.CreateElement("scenario");

                scenario.SetAttribute("id", $"{index}");
                scenario.SetAttribute("type", $"{(scenario_types[index] == 1 ? "sp" : "mp")}");
                scenario.SetAttribute("initial_chunk_count", $"{initial_chunk_count[index]}");
                scenario.SetAttribute("label", $"{scenario_labels[index]}");
                
                if (scenario_chunk_range[index] - 1 != 0)
                    scenario.InnerText = $"0-{scenario_chunk_range[index] - 1}";

                else scenario.InnerText = "0";
                
                scenarios.AppendChild(scenario);
            }

            return scenarios;
        }

        /// <summary>
        ///   Build .gp4 Structure And Save To File
        ///</summary>
        /// <returns> Time Taken For Build Process </returns>
        public void BuildGp4Elements(XmlDeclaration gp4_declaration, XmlNode psproject, XmlNode volume, XmlNode volume_type, XmlNode volume_id, XmlNode scenarios, XmlNode volume_ts, XmlNode package, XmlNode chunk_info, XmlNode files, XmlNode rootdir, XmlNode chunks) {
          

            gp4.AppendChild(gp4_declaration);
            gp4.AppendChild(psproject);

            psproject.AppendChild(volume);

            volume.AppendChild(volume_type);
            volume.AppendChild(volume_id);
            volume.AppendChild(volume_ts);
            volume.AppendChild(package);
            volume.AppendChild(chunk_info);

            chunk_info.AppendChild(chunks);
            chunk_info.AppendChild(scenarios);

            psproject.AppendChild(files);
            psproject.AppendChild(rootdir); 
            
            gp4.AppendChild(gp4.CreateComment($"gengp4.exe Alternative. (add a link to the library repository)"));
        }

        #endregion
    }
}
