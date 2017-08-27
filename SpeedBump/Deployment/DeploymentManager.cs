﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SpeedBump.Versioning;
using System.IO;
using System.Text.RegularExpressions;
using log4net;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Windows;
using System.Threading;

namespace SpeedBump.Deployment
{
    public class DeploymentManager
    {
        //TODO remove source and item from functions
        public DeploymentManager(ProjectControlSource source, ProjectControlSourceItem item)
        {
            this.source = source;
            this.item = item;
        }

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private VersionManager ver = new VersionManager();
        private ProjectControlSource source;
        private ProjectControlSourceItem item;

        public Versioning.Version Bump(string choice)
        {

            Versioning.Version newVersion = new Versioning.Version();
            string projectPath = source.BaseDir + item.BaseDir + @"\";
            Versioning.Version itemVersion = ver.GetVersion(projectPath + item.StageDir);

            //List<string> childpaths = ver.GetChildren(source.BaseDir + item.BaseDir);
            string[] childpaths = Directory.GetDirectories(projectPath);

            switch (choice)
            {
                case "Trivial":
                    itemVersion.bumpTrivial();
                    break;
                case "Minor":
                    itemVersion.bumpMinor();
                    break;
                case "Major":
                    itemVersion.bumpMajor();
                    break;
                default:
                    throw new Exception(choice + "is not a valid option");
            }


            string pattern = "\"[^\"]+\"";
            int count = 0;
            foreach (string child in childpaths)
            {
                string filepath = child + "\\properties\\AssemblyInfo.cs";
                if (!File.Exists(filepath))
                {
                    log.Warn(filepath +"does not exist. Skipping");
                    continue;
                }
                string[] temp = File.ReadAllLines(filepath);
                foreach (string line in temp)
                {
                    if (line.Contains("[assembly: AssemblyVersion(") && !line.Contains("//"))
                    {
                        temp[count] = Regex.Replace(line, pattern, '"' + itemVersion.getVersion() + '"');
                        count = 0;
                        break;
                    }
                    count++;
                }
                File.WriteAllLines(filepath, temp);

                newVersion = ver.GetVersion(projectPath + item.StageDir);





            }

            return newVersion;
        }
        private string run(string command, string arguments)
        {
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo();
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.FileName = command;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            bool b = process.Start();

            StringBuilder buffer = new StringBuilder();
            buffer.Append(command + " " + arguments + "\n\n");
            while (!process.HasExited)
            {
                buffer.Append(process.StandardOutput.ReadToEnd());
            }

            var exitCode = process.ExitCode;
            process.Close();

            return buffer.ToString();
        }
        public string Build()
        {
            log.Debug("[User Action] Build");
            string pattern = "[1-9]+?[0-9]?[ ][W][a][r]";
            string command = source.CompileDir + @"\msbuild.exe";
            string arguments = "\"" + source.BaseDir + item.BaseDir +"\\" + item.Project + ".sln\" /p:Configuration=Debug";
            log.Debug("command="+command);
            log.Debug("arguments=" + arguments);
            string result = run(command, arguments);
           /* if(result.Contains("Build FAILED") || result.Contains("MSBUILD : error"))
            {
                throw new Exception("Build Failed");
            }
            Regex warningCheck = new Regex(pattern);
            if (warningCheck.IsMatch(result))
            {
                throw new Exception("Warning: Check Log");
            }*/
            log.Debug(result);
            return result;
        }
        public void Clean()
        {
            log.Debug("[User Action] Clean");
            string projectPath = source.BaseDir + item.BaseDir + @"\";
            string[] childpaths = Directory.GetDirectories(projectPath);
            if (Directory.Exists(projectPath + @"\.vs\"))
            {
                DeleteDirectory(projectPath + @"\.vs\");
            }
            if (Directory.Exists(projectPath + @"\TestResults\"))
            {
                DeleteDirectory(projectPath + @"\TestResults\");
            }
            try
            {
                foreach (string child in childpaths)
                {
                    if (Directory.Exists(child + "\\bin"))
                    {
                        DeleteDirectory(child + "\\bin\\");
                    }
                    if (Directory.Exists(child + "\\obj"))
                    {
                        DeleteDirectory(child + @"\obj\");
                    }
                }
            }
            catch (Exception) { throw; }
        }
        public void Prepare()
        {
            string projectPath = source.BaseDir + item.BaseDir;
            string[] files = Directory.GetFiles(projectPath);
            foreach(string file in files)
            {
                if (file.EndsWith(".sln"))
                {
                    File.WriteAllLines(file, File.ReadLines(file).Where(l => !l.Contains("Any CPU")).ToList());
                }
            }
        }
        private void copyDirectory()
        {
            MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir + @"\" + item.StageDir);
            Versioning.Version itemVersion = ver.getchildVersion(assembly);
            string SourcePath = source.BaseDir + item.BaseDir + "\\" + item.StageDir;

            Directory.CreateDirectory(SourcePath + @"\bin\x64\copy\" + itemVersion.getVersion());
            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(SourcePath + @"\bin\x64\Debug", "*.*",
                SearchOption.AllDirectories))
                File.Copy(newPath, newPath.Replace(SourcePath + @"\bin\x64\Debug", SourcePath + @"\bin\x64\copy\" + itemVersion.getVersion()), true);
        }
        private void Zip()
        {
            MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir + @"\" + item.StageDir);
            Versioning.Version itemVersion = ver.getchildVersion(assembly);
            string path = source.BaseDir + item.BaseDir + "\\" + item.StageDir + @"\bin\x64\copy\";
            ZipFile.CreateFromDirectory(path, source.BaseDir + item.BaseDir + "\\" + item.StageDir + @"\bin\x64\" + itemVersion.getVersion() + ".zip"); 
        }
        private void upload(string address)
        {
            MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir + @"\" + item.StageDir);
            Versioning.Version itemVersion = ver.getchildVersion(assembly);
            string remoteStagingDir = item.RemoteStagingDir; // get this from the algo control properties; you may have to create a new entry
            string zipFilename = itemVersion.getVersion() + ".zip";
            var password = source.FTPHosts.Where(f => f.IPAddress == address)
                .Select(p => p.Password)
                .First();
            var username = source.FTPHosts.Where(f => f.IPAddress == address)
                .Select(u => u.Username)
                .First();
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Credentials = new NetworkCredential(username, password);
                    client.UploadFile(@"ftp://" + address + "/" + remoteStagingDir + "/" + zipFilename, "STOR", source.BaseDir + item.BaseDir + "\\" + item.StageDir + @"\bin\x64\" + zipFilename);
                }
            }catch(Exception) { throw; }
        }
        private void remove()
        {
            MyFile assembly = ver.OpenAssemblyInfo(source.BaseDir + item.BaseDir + @"\" + item.StageDir);
            Versioning.Version itemVersion = ver.getchildVersion(assembly);
            Directory.Delete(source.BaseDir + item.BaseDir + "\\" + item.StageDir + "\\" + @"bin\x64\copy", true);
            File.Delete(source.BaseDir + item.BaseDir + "\\" + item.StageDir + "\\" + @"bin\x64\" + itemVersion.getVersion() + ".zip");       
        }
        public void Deploy(string address)
        {
            copyDirectory();
            Zip();
            upload(address);
            remove();
        }
        private void DeleteDirectory(string path)
        {
            string[] dirs = Directory.GetDirectories(path);
            if(dirs.Count() != 0)
            {
                foreach(string dir in dirs)
                {
                    DeleteDirectory(dir);
                }
            }
            string[] files = Directory.GetFiles(path);
            if(files.Count() != 0)
            {
                foreach(string file in files)
                {
                    File.Delete(file);
                }
            }
            Directory.Delete(path);
        }
        
    } }
