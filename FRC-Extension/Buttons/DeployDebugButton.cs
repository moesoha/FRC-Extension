﻿using System;
using System.Collections.Generic;
using System.Linq;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Renci.SshNet.Common;
using VSLangProj;

namespace RobotDotNet.FRC_Extension.Buttons
{
    public class DeployDebugButton : ButtonBase
    {
        protected readonly bool m_debugButton;
        protected static bool s_deploying = false;
        protected static readonly List<OleMenuCommand> s_deployCommands = new List<OleMenuCommand>();

        private Project m_robotProject = null;

        public DeployDebugButton(Frc_ExtensionPackage package, int pkgCmdIdOfButton, bool debug) : base(package, true, GuidList.guidFRC_ExtensionCmdSet, pkgCmdIdOfButton)
        {
            m_debugButton = debug;
            s_deployCommands.Add(m_oleMenuItem);
        }

        private void DisableAllButtons()
        {
            foreach (var oleMenuCommand in s_deployCommands)
            {
                oleMenuCommand.Enabled = false;
            }
            s_deploying = true;
        }

        private void EnableAllButtons()
        {
            foreach (var oleMenuCommand in s_deployCommands)
            {
                oleMenuCommand.Enabled = true;
            }
            s_deploying = false;
        }

        public override async void ButtonCallback(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand == null)
            {
                return;
            }
            if (!s_deploying)
            {
                try
                {
                    m_output.ProgressBarLabel = "Deploying Robot Code";
                    OutputWriter.Instance.Clear();
                    SettingsPageGrid page;
                    string teamNumber = m_package.GetTeamNumber(out page);

                    if (teamNumber == null) return;

                    //Disable the deploy buttons
                    DisableAllButtons();
                    DeployManager m = new DeployManager(m_package.PublicGetService(typeof (DTE)) as DTE);
                    bool success = await m.DeployCode(teamNumber, page, m_debugButton, m_robotProject);
                    EnableAllButtons();
                    if (success)
                    {
                        m_output.ProgressBarLabel = "Robot Code Deploy Successful";
                    }
                    else
                    {
                        m_output.ProgressBarLabel = "Robot Code Deploy Failed";
                    }
                }
                catch (SshConnectionException)
                {
                    m_output.WriteLine("Connection to RoboRIO lost. Deploy aborted.");
                    EnableAllButtons();
                    m_output.ProgressBarLabel = "Robot Code Deploy Failed";
                }
                catch (Exception ex)
                {
                    m_output.WriteLine(ex.ToString());
                    EnableAllButtons();
                    m_output.ProgressBarLabel = "Robot Code Deploy Failed";
                }

            }
        }

        public override void QueryCallback(object sender, EventArgs e)
        {
            var menuCommand = sender as OleMenuCommand;
            if (menuCommand != null)
            {
                var dte = m_package.PublicGetService(typeof(DTE)) as DTE;

                bool visable = false;
                m_robotProject = null;

                SettingsPageGrid grid = (SettingsPageGrid) m_package.PublicGetDialogPage(typeof (SettingsPageGrid));

                if (grid.DebugMode)
                {
                    var sb = (SolutionBuild2) dte.Solution.SolutionBuild;

                    if (sb.StartupProjects != null)
                    {
                        if (sb.StartupProjects != null)
                        {
                            string project = ((Array) sb.StartupProjects).Cast<string>().First();
                            Project startupProject = dte.Solution.Item(project);
                            var vsproject = startupProject.Object as VSLangProj.VSProject;
                            if (vsproject != null)
                            {
                                //If we are an assembly, and its named WPILib, enable the deploy
                                if (
                                    (from Reference reference in vsproject.References
                                        where reference.SourceProject == null
                                        select reference.Name).Any(name => name.Contains("WPILib")))
                                {
                                    m_robotProject = startupProject;
                                    visable = true;
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (Project project in dte.Solution.Projects)
                    {
                        if (project.Globals.VariableExists["RobotProject"])
                        {
                            if (project.Globals["RobotProject"].ToString() != "yes")
                            {
                                continue;
                            }
                            var vsproject = project.Object as VSLangProj.VSProject;

                            if (vsproject != null)
                            {
                                //If we are an assembly, and its named WPILib, enable the deploy
                                if (
                                    (from Reference reference in vsproject.References
                                        where reference.SourceProject == null
                                        select reference.Name).Any(name => name.Contains("WPILib")))
                                {
                                    visable = true;
                                    m_robotProject = project;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (s_deploying)
                    visable = false;

                menuCommand.Enabled = visable;

                menuCommand.Visible = true;
            }
        }
    }
}
