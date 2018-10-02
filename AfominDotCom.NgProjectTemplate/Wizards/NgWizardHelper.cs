﻿using EnvDTE;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace AfominDotCom.NgProjectTemplate.Wizards
{
    public static class NgWizardHelper
    {
        // private const string NgVersionSuccessFragment = "@angular/cli";
        private const string NgVersionSuccessFragment1 = "angular";
        private const string NgVersionSuccessFragment2 = "cli";
        internal const string GitignoreFileName = ".gitignore";
        internal const string GitignoreTempFileName = ".gitignore.temp";
        internal const string PackageJsonFileName = "package.json";
        internal const string PackageJsonOldFileName = "package.json.old";
        internal const string AngularCliJsonFileName = ".angular-cli.json";
        internal const string AngularJsonFileName = "angular.json";
        internal const string TsconfigJsonFileName = "tsconfig.json";
        internal const string TsconfigJsonOldFileName = "tsconfig.json.old";
        internal const string StartupCsFileName = "Startup.cs";
        internal const string NgFoundLogFileName = "ErrorNgNotFound.txt";

        private static string RunCmd(string arguments, string workingDirectory, bool createNoWindow, bool redirectStandardOutput)
        {
            string output = null;
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = createNoWindow,
                RedirectStandardOutput = redirectStandardOutput,
            };
            using (var process = System.Diagnostics.Process.Start(startInfo))
            {
                if (redirectStandardOutput)
                {
                    output = process.StandardOutput.ReadToEnd();
                }
                process.WaitForExit();
            }
            return output;
        }

        private static string RunNgVersion(string workingDirectory)
        {
            var cmdArguments = "/c ng --version";
            return RunCmd(cmdArguments, workingDirectory, true, true);
        }

        internal static string RunNgNew(string projectDirectory, string projectName, bool addRouting, bool isNgFound)
        {
            var path = projectDirectory.EndsWith("\\") ? projectDirectory : string.Concat(projectDirectory, "\\");
            var directoryInfo = Directory.GetParent(path);
            var parentDirectory = directoryInfo.Parent.FullName;
            var directory = directoryInfo.Name;

            var routingOption = addRouting ? " --routing" : "";

            // CMD writes errors to the StandardError stream. NG writes errors to StandardOutput. 
            // To read both the streams is possible but needs extra effots to avoid a thread deadlock.
            // If NG was not found, we display the Command Window to the user to watch the errors.
            var cmdArguments = $"/c ng new {projectName} --directory {directory}{routingOption} --skip-git --skip-install"
                + (isNgFound ? "" : " & timeout /t 10");

            return RunCmd(cmdArguments, parentDirectory, isNgFound, isNgFound);
        }

        /// <summary>
        ///  Test if @angular/cli is installed globally.
        /// </summary>
        /// <param name="preferredDirectory">Prefererred directory</param>
        /// <returns></returns>
        internal static bool IsNgFound(string preferredDirectory)
        {
            // Be optimistic. Missing target is better than false alarm. We will check the result of "ng new" anyway.
            var isNgFound = true;
            var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var workingDirectory = Directory.Exists(preferredDirectory)
                ? preferredDirectory
                : (Directory.Exists(desktopDirectory) ? desktopDirectory : null);
            string ngVersionOutput = String.Empty;
            var start = DateTime.Now;
            if (!String.IsNullOrEmpty(workingDirectory))
            {
                ngVersionOutput = RunNgVersion(workingDirectory);
                // Old versions of CLI ~1.1 (actually chalk / supports-color) on Windows 7 fail when the output stream is redirected. ngVersionOutput is null/empty in that case.
                isNgFound = !String.IsNullOrEmpty(ngVersionOutput);
                if (isNgFound)
                {
                    var lowerCaseOutput = ngVersionOutput.ToLower();
                    isNgFound = lowerCaseOutput.Contains(NgVersionSuccessFragment1)
                        && lowerCaseOutput.Contains(NgVersionSuccessFragment2);
                }
            }
            return isNgFound;
        }

        //public static ProjectItem FindProjectItem(Project project, string fileName)
        //{
        //    ProjectItem projectItem = null;
        //    if (project != null)
        //    {
        //        foreach (var i in project.ProjectItems)
        //        {
        //            if (i is ProjectItem item)
        //            {
        //                var itemName = item.Name;
        //                if (itemName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        //                {
        //                    projectItem = item;
        //                    break;
        //                }
        //            }
        //        }
        //    }
        //    return projectItem;
        //}

        internal static bool FindFileInRootDir(Project project, string fileName)
        {
            return FindFileInRootDir(project, fileName, out string filePath);
        }

        internal static bool FindFileInRootDir(Project project, string fileName, out string filePath)
        {
            filePath = null;
            if (project != null)
            {
                var projectDirectory = Path.GetDirectoryName(project.FullName);
                if (Directory.Exists(projectDirectory))
                {
                    var path = Path.Combine(projectDirectory, fileName);
                    if (File.Exists(path))
                    {
                        filePath = path;
                        return true;
                    }
                }
            }
            return false;
        }

        internal static Version GetVersion()
        {
            return typeof(AfominDotCom.NgProjectTemplate.Wizards.NgWizardHelper).Assembly.GetName().Version;
        }

        internal static bool FindWindow(Project project, string filePath)
        {
            return FindWindow(project, filePath, out EnvDTE.Window window);
        }

        internal static bool FindWindow(Project project, string filePath, out EnvDTE.Window window)
        {
            window = null;
            var windows = project.DTE.Windows;
            foreach (var w in windows)
            {
                if (w is EnvDTE.Window wnd)
                {
                    var projectItem = wnd.ProjectItem;
                    if ((projectItem != null) && (projectItem.FileCount != 0) /* && window.Type == vsWindowType.vsWindowTypeDocument */)
                    {
                        var fileName = projectItem.FileNames[1]; // 1-based array
                        if (fileName == filePath)
                        {
                            window = wnd;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        //internal static bool FindAndCloseWindow(Project project, string filePath, vsSaveChanges saveChanges)
        //{
        //    if (FindWindow(project, filePath, out Window window))
        //    {
        //        window.Close(saveChanges);
        //        return true;
        //    }
        //    return false;
        //}

        internal static void RewriteFile(string filePath, string fileContents)
        {
            // Don't do: File.WriteAllText(filePath, result.ToString(), System.Text.Encoding.UTF8); // That writes a BOM. BOM causes Webpack to fail.
            var bytes = Encoding.UTF8.GetBytes(fileContents);
            using (var memoryStream = new MemoryStream(bytes))
            {
                using (var fileStream = new FileStream(filePath, FileMode.Truncate, FileAccess.Write))
                {
                    memoryStream.CopyTo(fileStream);
                }
            }
        }

        internal static bool IsAspNetCore2(Project project)
        {
            var filePath = project?.FullName;
            if (File.Exists(filePath))
            {
                var lines = File.ReadAllLines(filePath);
                var isCoreVersion2x = lines
                    .Where(i => i.Contains("<TargetFramework>") && i.Contains("netcoreapp2."))
                    .Any()
                    ;
                //var isAspNetCore20 = lines
                //    .Where(i => i.Contains("<PackageReference") && i.Contains("\"Microsoft.AspNetCore.") && i.Contains("\"2."))
                //    .Any()
                //    ;
                // ASP.NET Core 2.0 used Microsoft.AspNetCore.All version 2.x. ASP.NET Core 2.1 uses Microsoft.AspNetCore.App without a version specified.
                var isAspNetCore = lines
                    .Where(i => i.Contains("<PackageReference") && i.Contains("\"Microsoft.AspNetCore."))
                    .Any()
                    ;
                return isCoreVersion2x && isAspNetCore;
            }
            return false;
        }

        internal static bool IsNpmAngularFound(string packageJsonFilePath)
        {
            if (File.Exists(packageJsonFilePath))
            {
                var lines = File.ReadAllLines(packageJsonFilePath);
                var isNpmAngularFound = lines
                  .Where(i => i.Contains("@angular/core"))
                  // Although the JSON standard demands double quotes, let's be paranoid.
                  .Select(i => i.Replace("'", "\""))
                  .Where(i => i.Contains("\"@angular/core\""))
                  .Any()
                  ;
                return isNpmAngularFound;
            }
            return false;
        }

        internal static bool IsFileOpened(Project project, string fileName)
        {
            if (FindFileInRootDir(project, fileName, out string filePath))
            {
                return FindWindow(project, filePath);
            }
            return false;
        }

    }
}
