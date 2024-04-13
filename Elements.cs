using libgp4;
using System;
using System.IO;
using System.Xml;

namespace libgp4 {
    public partial class GP4Creator {
    #pragma warning disable CS1587

        // GP4 Element Variables
        private readonly XmlDocument gp4;
        private readonly XmlDeclaration gp4_declaration;

        /// <summary> XML Element Required For Valid .gp4 Project Creation </summary>
        private static XmlElement
            file,
            psproject,
            chunk,
            scenario,
            dir,
            subdir,
            chunks,
            volume,
            rootdir,
            chunk_info,
            files,
            package,
            scenarios,
            volume_id,
            volume_type,
            volume_ts
        ;


        /////////////////////\\\\\\\\\\\\\\\\\\\
        ///--     GP4 ELEMENT CREATION     --\\\
        /////////////////////\\\\\\\\\\\\\\\\\\\
        #region GP4 ELEMENT CREATION

        /// <summary>
        ///   Create Base .gp4 Elements (Up To Chunk/Scenario Data)
        /// </summary>
        private void CreateBaseElements(string category, string timestamp, string content_id, string passcode, string pkg_source, string app_ver, string version, int chunk_count, int scenario_count) {
            psproject = gp4.CreateElement("psproject");
            psproject.SetAttribute("fmt", "gp4");
            psproject.SetAttribute("version", "1000");

            volume = gp4.CreateElement("volume");

            volume_type = gp4.CreateElement("volume_type");
            volume_type.InnerText = $"pkg_{((category == "gd")? "ps4_app" : "ps4_patch")}";

            volume_id = gp4.CreateElement("volume_id");
            volume_id.InnerText = "PS4VOLUME";

            volume_ts = gp4.CreateElement("volume_ts");
            volume_ts.InnerText = timestamp;

            package = gp4.CreateElement("package");
            package.SetAttribute("content_id", content_id);
            package.SetAttribute("passcode", passcode);
            package.SetAttribute("storage_type", ((category == "gp")? "digital25" : "digital50"));
            package.SetAttribute("app_type", "full");

            if(category == "gp")
                package.SetAttribute("app_path", $"{((pkg_source == "")? $"{content_id}-A{app_ver.Replace(".", "")}-V{version.Replace(".", "")}.pkg" : pkg_source)}");

            chunk_info = gp4.CreateElement("chunk_info");
            chunk_info.SetAttribute("chunk_count", $"{chunk_count}");
            chunk_info.SetAttribute("scenario_count", $"{scenario_count}");
        }


        /// <summary>
        ///   Create "files" Element, Containing File Destination And Source Paths, Along With Whether To Enable PFS Compression
        /// </summary>
        private void CreateFilesElement(string[] file_paths, string gamedata_folder) {
            files = gp4.CreateElement("files");

            for(var index = 0; index < file_paths.Length; index++) {
                if(FileShouldBeExcluded(file_paths[index]))
                    continue;

                file = gp4.CreateElement("file");
                file.SetAttribute("targ_path", (file_paths[index].Replace(gamedata_folder + "\\", string.Empty)).Replace('\\', '/'));
                file.SetAttribute("orig_path", file_paths[index]);

                if(!SkipCompression(file_paths[index]))
                    file.SetAttribute("pfs_compression", "enable");
                
                if(!SkipChunkAttribute(file_paths[index]) && chunk_count - 1 != 0)
                    file.SetAttribute("chunks", $"0-{chunk_count - 1}");
                
                files.AppendChild(file);
            }
        }

        /// <summary>
        ///    Create "rootdir" Element Containing The Game's File Structure through A Listing Of Each Directory And Subdirectory
        /// </summary>
        private void CreateRootDirectoryElement(string gamedata_folder) {
            rootdir = gp4.CreateElement("rootdir");

            void AppendSubfolder(string dir, XmlElement node) {
                foreach(string folder in Directory.GetDirectories(dir)) {
                    subdir = gp4.CreateElement("dir");
                    subdir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));

                    if(folder.Substring(folder.LastIndexOf('\\') + 1) != "about")
                        node.AppendChild(subdir);

                    if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, subdir);
                }
            }

            foreach(string folder in Directory.GetDirectories(gamedata_folder)) {
                dir = gp4.CreateElement("dir");
                dir.SetAttribute("targ_name", folder.Substring(folder.LastIndexOf('\\') + 1));

                rootdir.AppendChild(dir);
                if(Directory.GetDirectories(folder).Length > 0) AppendSubfolder(folder, dir);
            }

        }

        /// <summary>
        ///   Create "chunks" Element
        /// </summary>
        private void CreateChunksElement(string[] chunk_labels, int chunk_count) {
            chunks = gp4.CreateElement("chunks");

            for(int chunk_id = 0; chunk_id < chunk_count; chunk_id++) {
                chunk = gp4.CreateElement("chunk");
                chunk.SetAttribute("id", $"{chunk_id}");

                if(chunk_labels[chunk_id] == "") //  I Hope This Fix Works For Every Game...
                    chunk.SetAttribute("label", $"Chunk #{chunk_id}");
                else
                    chunk.SetAttribute("label", $"{chunk_labels[chunk_id]}");
                chunks.AppendChild(chunk);
            }
        }

        /// <summary>
        ///   Create "scenarios" Element
        /// </summary>
        private void CreateScenariosElement(string[] scenario_labels) {
            scenarios = gp4.CreateElement("scenarios");
            scenarios.SetAttribute("default_id", $"{default_scenario_id}");

            for(var index = 0; index < scenario_count; index++) {
                scenario = gp4.CreateElement("scenario");

                scenario.SetAttribute("id", $"{index}");
                scenario.SetAttribute("type", $"{(scenario_types[index] == 1 ? "sp" : "mp")}");
                scenario.SetAttribute("initial_chunk_count", $"{initial_chunk_count[index]}");
                scenario.SetAttribute("label", $"{scenario_labels[index]}");
                
                if (scenario_chunk_range[index] - 1 != 0)
                    scenario.InnerText = $"0-{scenario_chunk_range[index] - 1}";

                else scenario.InnerText = "0";
                
                scenarios.AppendChild(scenario);
            }

        }

        /// <summary>
        ///   Build .gp4 Structure And Save To File
        ///</summary>
        /// <returns> Time Taken For Build Process </returns>
        public TimeSpan WriteElementsToGP4(TimeSpan internal_timestamp) {
            gp4.AppendChild(gp4_declaration);
            gp4.AppendChild(psproject);
            
            psproject.AppendChild(volume);
            psproject.AppendChild(files);
            psproject.AppendChild(rootdir);
            
            volume.AppendChild(volume_type);
            volume.AppendChild(volume_id);
            volume.AppendChild(volume_ts);
            volume.AppendChild(package);
            volume.AppendChild(chunk_info);
            
            chunk_info.AppendChild(chunks);
            chunk_info.AppendChild(scenarios);

            var NewTime = new TimeSpan(DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second, DateTime.Now.Millisecond);
            var stamp = gp4.CreateComment($"gengp4.exe Alternative. Time Taken For Build Process: {NewTime.Subtract(internal_timestamp)}");
            gp4.AppendChild(stamp);

            return NewTime;
        }

        #endregion
    }
}
