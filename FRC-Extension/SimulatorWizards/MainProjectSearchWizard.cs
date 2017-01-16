﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;
using VSLangProj;

namespace RobotDotNet.FRC_Extension.SimulatorWizards
{
    /// <summary>
    /// This wizard is used when creating the Simulator project in order to search for the main robot project and fill out the replacements properly
    /// </summary>
    public class MainProjectSearchWizard : IWizard
    {
        /// <summary>
        /// Called at the start of the wizard being called, which happens while the project is getting created.
        /// </summary>
        /// <param name="automationObject"></param>
        /// <param name="replacementsDictionary"></param>
        /// <param name="runKind"></param>
        /// <param name="customParams"></param>
        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = automationObject as DTE;
            //Do nothing if we cannot access our automation object
            if (dte == null)
            {
                return;
            }

            //Force a try, even though we dont really need to. VS does not like exceptions.
            try
            {
                //Loop through all projects found
                foreach (Project project in dte.Solution.Projects)
                {
                    if (project.Globals == null)
                    {
                        //If globals are null, continue, as the project is probably disabled.
                        continue;
                    }
                    //Find the project with the RobotProject global, and also make sure it references WPILib.
                    if (project.Globals.VariableExists["RobotProject"])
                    {
                        var vsproject = project.Object as VSProject;

                        if (vsproject != null)
                        {
                            if ((from Reference reference in vsproject.References where reference.SourceProject == null select reference.Name).Any(name => name.Contains("WPILib")))
                            {
                                //If everything checks out, Search for the robot namespace and class, and store
                                //a reference to the project so we can add it to the new project later.
                                FindRobotNameAndNamespace(replacementsDictionary, project);
                                m_robotProject = project;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ThreadHelper.JoinableTaskFactory.Run(() => OutputWriter.Instance.WriteLineAsync(ex.StackTrace));
            }
        }

        private Project m_robotProject;

        //Find and add our robot namespace and class to the replacement dictionary.
        private void FindRobotNameAndNamespace(Dictionary<string, string> replacementsDictionary, Project robotProject)
        {
            string ns = null;
            string cls = null;
            foreach (ProjectItem projectItem in robotProject.ProjectItems)
            {
                //Search all items in project for our Program.cs
                if (projectItem.Name == "Program.cs")
                {
                    //Found the program file. Load it, and search for our namespace and class
                    string fileName = projectItem.FileNames[1];
                    try
                    {
                        string[] lines = File.ReadAllLines(fileName);
                        foreach (var line in lines)
                        {
                            if (ns != null && cls != null)
                            {
                                break;
                            }
                            if (line.StartsWith("namespace"))
                            {
                                //Its our namespace
                                string[] split = line.Split(' ');
                                if (split.Length > 1)
                                {
                                    ns = split[1];
                                }
                                continue;
                            }
                            if (line.Contains("RobotBase.Main"))
                            {
                                //Its the way to find our main class
                                int typeofIndex = line.IndexOf("typeof");
                                string sub = line.Substring(typeofIndex);
                                int startParam = sub.IndexOf('(');
                                int endParam = sub.IndexOf(')', startParam + 1);
                                cls = sub.Substring(startParam + 1, endParam - startParam - 1);
                                continue;
                            }
                        }

                    }
                    catch (Exception)
                    {
                    }
                    if (ns != null && cls != null)
                    {
                        break;
                    }
                }
                else if (projectItem.Name == "Program.vb")
                {
                    //TODO Do this
                }
            }

            //If we found both, add both to the dictionary.
            if (ns != null && cls != null)
            {
                replacementsDictionary.Add("$robotnamespace$", ns);
                replacementsDictionary.Add("$robotclass$", cls);
            }
        }

        /// <summary>
        /// Called after the project is finished generating. Use this to automatically add the main robot project
        /// to the simulator as a reference.
        /// </summary>
        /// <param name="project"></param>
        public void ProjectFinishedGenerating(Project project)
        {
            var vsproject = project.Object as VSProject;


            if (vsproject != null && m_robotProject != null)
            {
                vsproject.References.AddProject(m_robotProject);
            }

            DTE dte = Package.GetGlobalService(typeof(DTE)) as DTE;
            if (dte != null)
            {
                dte.Solution.Properties.Item("StartupProject").Value = project.Name;
            }
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {

        }

        public bool ShouldAddProjectItem(string filePath)
        {
            return true;
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {

        }

        public void RunFinished()
        {

        }
    }
}
