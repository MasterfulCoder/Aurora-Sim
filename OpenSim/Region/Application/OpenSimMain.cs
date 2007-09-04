/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.IO;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Configuration;
using OpenSim.Region.Physics.Manager;

using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment;
using System.Text;
using System.Collections.Generic;
using OpenSim.Framework.Utilities;

namespace OpenSim
{
    public delegate void ConsoleCommand(string[] comParams);

    public class OpenSimMain : RegionApplicationBase, conscmd_callback
    {
        public string m_physicsEngine;
        public string m_scriptEngine;
        public bool m_sandbox;
        public bool user_accounts;
        public bool m_gridLocalAsset;


        protected ModuleLoader m_moduleLoader;

        protected string m_storageDLL = "OpenSim.DataStore.NullStorage.dll";

        protected string m_startupCommandsFile = "";

        protected List<UDPServer> m_udpServers = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();
        protected List<Scene> m_localScenes = new List<Scene>();

        private bool m_silent;
        private string m_logFilename = ("region-console.log");
        private bool m_permissions = false;

        private bool m_DefaultModules = true;
        private string m_exceptModules = "";
        private bool m_DefaultSharedModules = true;
        private string m_exceptSharedModules = "";

        private bool standaloneAuthenticate = false;
        private string standaloneWelcomeMessage = null;
        private string standaloneInventoryPlugin = "";
        private string standaloneUserPlugin = "";

        private Scene m_consoleRegion = null;

        public ConsoleCommand CreateAccount = null;

        public OpenSimMain(IConfigSource configSource)
            : base()
        {
            IConfigSource startupSource = configSource;
            string iniFile = startupSource.Configs["Startup"].GetString("inifile", "OpenSim.ini");

            //check for .INI file (either default or name passed in command line)
            string iniFilePath = Path.Combine(Util.configDir(), iniFile);
            if (File.Exists(iniFilePath))
            {
                startupSource = new IniConfigSource(iniFilePath);

                //enable following line, if we want the original config source(normally commandline args) merged with ini file settings.
                //in this case we have it so that if both sources have the same named setting, the command line value will overwrite the ini file value. 
                //(as if someone has bothered to enter a command line arg, we should take notice of it)
                startupSource.Merge(configSource);
            }

            ReadConfigSettings(startupSource);
        }

        protected void ReadConfigSettings(IConfigSource configSource)
        {
            m_networkServersInfo = new NetworkServersInfo();
            m_sandbox = !configSource.Configs["Startup"].GetBoolean("gridmode", false);
            m_physicsEngine = configSource.Configs["Startup"].GetString("physics", "basicphysics");
            m_silent = configSource.Configs["Startup"].GetBoolean("noverbose", false);
            m_permissions = configSource.Configs["Startup"].GetBoolean("serverside_object_permissions", false);

            m_storageDLL = configSource.Configs["Startup"].GetString("storage_plugin", "OpenSim.DataStore.NullStorage.dll");

            m_startupCommandsFile = configSource.Configs["Startup"].GetString("startup_console_commands_file", "");

            m_scriptEngine = configSource.Configs["Startup"].GetString("script_engine", "DotNetEngine");

            m_DefaultModules = configSource.Configs["Startup"].GetBoolean("default_modules", true);
            m_DefaultSharedModules = configSource.Configs["Startup"].GetBoolean("default_shared_modules", true);
            m_exceptModules = configSource.Configs["Startup"].GetString("except_modules", "");
            m_exceptSharedModules = configSource.Configs["Startup"].GetString("except_shared_modules", "");

            standaloneAuthenticate = configSource.Configs["StandAlone"].GetBoolean("accounts_authenticate", false);
            standaloneWelcomeMessage = configSource.Configs["StandAlone"].GetString("welcome_message", "Welcome to OpenSim");
            standaloneInventoryPlugin = configSource.Configs["StandAlone"].GetString("inventory_plugin", "OpenSim.Framework.Data.SQLite.dll");
            standaloneUserPlugin = configSource.Configs["StandAlone"].GetString("userDatabase_plugin", "OpenSim.Framework.Data.DB4o.dll");

            m_networkServersInfo.loadFromConfiguration(configSource);
        }


        /// <summary>
        /// Performs initialisation of the scene, such as loading configuration from disk.
        /// </summary>
        public override void StartUp()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            m_log = new LogBase(Path.Combine(Util.logDir(), m_logFilename), "Region", this, m_silent);
            MainLog.Instance = m_log;

            base.StartUp();

            if (m_sandbox)
            {
                CommunicationsLocal.LocalSettings settings = new CommunicationsLocal.LocalSettings(standaloneWelcomeMessage, standaloneAuthenticate, standaloneInventoryPlugin, standaloneUserPlugin);
                CommunicationsLocal localComms = new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache, settings);
                m_commsManager = localComms;
                if (standaloneAuthenticate)
                {
                    this.CreateAccount = localComms.doCreate;
                }
            }
            else
            {
                m_commsManager = new CommunicationsOGS1(m_networkServersInfo, m_httpServer, m_assetCache);
                m_httpServer.AddStreamHandler(new SimStatusHandler());
            }

            string regionConfigPath = Path.Combine(Util.configDir(), "Regions");

            if (!Directory.Exists(regionConfigPath))
            {
                Directory.CreateDirectory(regionConfigPath);
            }

            string[] configFiles = Directory.GetFiles(regionConfigPath, "*.xml");

            if (configFiles.Length == 0)
            {
                CreateDefaultRegionInfoXml(Path.Combine(regionConfigPath, "default.xml"));
                configFiles = Directory.GetFiles(regionConfigPath, "*.xml");
            }

            m_moduleLoader = new ModuleLoader();
            MainLog.Instance.Verbose("Loading Shared Modules");
            m_moduleLoader.LoadDefaultSharedModules(m_exceptSharedModules);

            // Load all script engines found
            OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineLoader ScriptEngineLoader = new OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineLoader(m_log);

            for (int i = 0; i < configFiles.Length; i++)
            {
                //Console.WriteLine("Loading region config file");
                RegionInfo regionInfo = new RegionInfo("REGION CONFIG #" + (i + 1), configFiles[i]);


                UDPServer udpServer;
                Scene scene = SetupScene(regionInfo, out udpServer);

                m_moduleLoader.InitialiseSharedModules(scene);
                MainLog.Instance.Verbose("Loading Region's Modules");
                m_moduleLoader.CreateDefaultModules(scene, m_exceptModules);
                scene.SetModuleInterfaces();

                // Check if we have a script engine to load
                if (m_scriptEngine != null && m_scriptEngine != "")
                {
                    OpenSim.Region.Environment.Scenes.Scripting.ScriptEngineInterface ScriptEngine = ScriptEngineLoader.LoadScriptEngine(m_scriptEngine);
                    scene.AddScriptEngine(ScriptEngine, m_log);
                }

                //Server side object editing permissions checking
                if (m_permissions)
                    scene.PermissionsMngr.EnablePermissions();
                else
                    scene.PermissionsMngr.DisablePermissions();

                m_localScenes.Add(scene);

                m_udpServers.Add(udpServer);
                m_regionData.Add(regionInfo);
            }

            m_moduleLoader.PostInitialise();
            m_moduleLoader.ClearCache();

            // Start UDP servers
            for (int i = 0; i < m_udpServers.Count; i++)
            {
                this.m_udpServers[i].ServerListener();
            }

            //Run Startup Commands
            if (m_startupCommandsFile != "")
            {
                RunCommandScript(m_startupCommandsFile);
            }
            else
            {
                MainLog.Instance.Verbose("No startup command script specified. Moving on...");
            }
        }

        private static void CreateDefaultRegionInfoXml(string fileName)
        {
            new RegionInfo("DEFAULT REGION CONFIG", fileName);
        }

        protected override StorageManager CreateStorageManager(RegionInfo regionInfo)
        {
            return new StorageManager(m_storageDLL, regionInfo.DataStore, regionInfo.RegionName);
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager, AgentCircuitManager circuitManager)
        {
            return new Scene(regionInfo, circuitManager, m_commsManager, m_assetCache, storageManager, m_httpServer, m_moduleLoader);
        }

        protected override void Initialize()
        {
            m_httpServerPort = m_networkServersInfo.HttpListenerPort;

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo(m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
            m_assetCache = new AssetCache(assetServer);
            // m_assetCache = new AssetCache("OpenSim.Region.GridInterfaces.Local.dll", m_networkServersInfo.AssetURL, m_networkServersInfo.AssetSendKey);
        }

        protected override LogBase CreateLog()
        {
            if (!Directory.Exists(Util.logDir()))
            {
                Directory.CreateDirectory(Util.logDir());
            }

            return new LogBase((Path.Combine(Util.logDir(), m_logFilename)), "Region", this, m_silent);
        }

        # region Setup methods

        protected override PhysicsScene GetPhysicsScene()
        {
            return GetPhysicsScene(m_physicsEngine);
        }

        private class SimStatusHandler : IStreamHandler
        {
            public byte[] Handle(string path, Stream request)
            {
                return Encoding.UTF8.GetBytes("OK");
            }

            public string ContentType
            {
                get { return "text/plain"; }
            }

            public string HttpMethod
            {
                get { return "GET"; }
            }

            public string Path
            {
                get { return "/simstatus/"; }
            }
        }

        protected void ConnectToRemoteGridServer()
        {

        }

        #endregion

        /// <summary>
        /// Performs any last-minute sanity checking and shuts down the region server
        /// </summary>
        public virtual void Shutdown()
        {
            m_log.Verbose("Closing all threads");
            m_log.Verbose("Killing listener thread");
            m_log.Verbose("Killing clients");
            // IMPLEMENT THIS
            m_log.Verbose("Closing console and terminating");
            for (int i = 0; i < m_localScenes.Count; i++)
            {
                m_localScenes[i].Close();
            }
            m_log.Close();
            Environment.Exit(0);
        }

        #region Console Commands
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        private void RunCommandScript(string fileName)
        {
            MainLog.Instance.Verbose("Running command script (" + fileName + ")");
            if (File.Exists(fileName))
            {
                StreamReader readFile = File.OpenText(fileName);
                string currentCommand = "";
                while ((currentCommand = readFile.ReadLine()) != null)
                {
                    if (currentCommand != "")
                    {
                        MainLog.Instance.Verbose("Running '" + currentCommand + "'");
                        MainLog.Instance.MainLogRunCommand(currentCommand);
                    }
                }
            }
            else
            {
                MainLog.Instance.Error("Command script missing. Can not run commands");
            }
        }

        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public void RunCmd(string command, string[] cmdparams)
        {
            if ((m_consoleRegion == null) || (command == "exit-region") || (command == "change-region"))
            {
                switch (command)
                {
                    case "help":
                        m_log.Error("alert - send alert to a designated user or all users.");
                        m_log.Error("  alert [First] [Last] [Message] - send an alert to a user. Case sensitive.");
                        m_log.Error("  alert general [Message] - send an alert to all users.");
                        m_log.Error("backup - trigger a simulator backup");
                        m_log.Error("load-xml [filename] - load prims from XML");
                        m_log.Error("save-xml [filename] - save prims to XML");
                        m_log.Error("script - manually trigger scripts? or script commands?");
                        m_log.Error("show uptime - show simulator startup and uptime.");
                        m_log.Error("show users - show info about connected users.");
                        m_log.Error("shutdown - disconnect all clients and shutdown.");
                        m_log.Error("terrain help - show help for terrain commands.");
                        m_log.Error("quit - equivalent to shutdown.");
                        break;

                    case "show":
                        if (cmdparams.Length > 0)
                        {
                            Show(cmdparams[0]);
                        }
                        break;

                    case "save-xml":
                        if (cmdparams.Length > 0)
                        {
                            m_localScenes[0].SavePrimsToXml(cmdparams[0]);
                        }
                        else
                        {
                            m_localScenes[0].SavePrimsToXml("test.xml");
                        }
                        break;

                    case "load-xml":
                        if (cmdparams.Length > 0)
                        {
                            m_localScenes[0].LoadPrimsFromXml(cmdparams[0]);
                        }
                        else
                        {
                            m_localScenes[0].LoadPrimsFromXml("test.xml");
                        }
                        break;

                    case "terrain":
                        string result = "";
                        foreach (Scene scene in m_localScenes)
                        {
                            if (!scene.Terrain.RunTerrainCmd(cmdparams, ref result, scene.RegionInfo.RegionName))
                            {
                                m_log.Error(result);
                            }
                        }
                        break;
                    case "terrain-sim":
                        string result2 = "";
                        foreach (Scene scene in m_localScenes)
                        {
                            if (scene.RegionInfo.RegionName.ToLower() == cmdparams[0].ToLower())
                            {
                                string[] tmpCmdparams = new string[cmdparams.Length - 1];
                                cmdparams.CopyTo(tmpCmdparams, 1);

                                if (!scene.Terrain.RunTerrainCmd(tmpCmdparams, ref result2, scene.RegionInfo.RegionName))
                                {
                                    m_log.Error(result2);
                                }
                            }
                        }
                        break;
                    case "script":
                        foreach (Scene scene in m_localScenes)
                        {
                            scene.SendCommandToScripts(cmdparams);
                        }
                        break;

                    case "command-script":
                        if (cmdparams.Length > 0)
                        {
                            RunCommandScript(cmdparams[0]);
                        }
                        break;

                    case "permissions":
                        // Treats each user as a super-admin when disabled
                        foreach (Scene scene in m_localScenes)
                        {
                            if (Convert.ToBoolean(cmdparams[0]))
                                scene.PermissionsMngr.EnablePermissions();
                            else
                                scene.PermissionsMngr.DisablePermissions();
                        }
                        break;

                    case "backup":
                        foreach (Scene scene in m_localScenes)
                        {
                            scene.Backup();
                        }
                        break;

                    case "alert":
                        foreach (Scene scene in m_localScenes)
                        {
                            scene.HandleAlertCommand(cmdparams);
                        }
                        break;

                    case "create":
                        if (CreateAccount != null)
                        {
                            CreateAccount(cmdparams);
                        }
                        break;

                    case "quit":
                    case "shutdown":
                        Shutdown();
                        break;

                    case "change-region":
                        if (cmdparams.Length > 0)
                        {
                            string name = this.CombineParams(cmdparams, 0);
                            Console.WriteLine("Searching for Region: '" + name + "'");
                            foreach (Scene scene in m_localScenes)
                            {
                                if (scene.RegionInfo.RegionName.ToLower() == name.ToLower())
                                {
                                    m_consoleRegion = scene;
                                    MainLog.Instance.Verbose("Setting current region: " + m_consoleRegion.RegionInfo.RegionName);
                                }
                            }
                        }
                        else
                        {
                            if (m_consoleRegion != null)
                            {
                                MainLog.Instance.Verbose("Current Region: " + m_consoleRegion.RegionInfo.RegionName + ". To change region please use 'change-region <regioname>'");
                            }
                            else
                            {
                                MainLog.Instance.Verbose("Currently at Root level. To change region please use 'change-region <regioname>'");
                            }
                        }
                        break;

                    case "exit-region":
                        if (m_consoleRegion != null)
                        {
                            m_consoleRegion = null;
                            MainLog.Instance.Verbose("Exiting region, Now at Root level");
                        }
                        else
                        {
                            MainLog.Instance.Verbose("No region is set. Already at Root level");
                        }
                        break;

                    default:
                        m_log.Error("Unknown command");
                        break;
                }
            }
            else
            {
                //let the scene/region handle the command directly.
                m_consoleRegion.ProcessConsoleCmd(command, cmdparams);
            }
        }

        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="ShowWhat">What information to display (valid arguments are "uptime", "users")</param>
        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    m_log.Error("OpenSim has been running since " + m_startuptime.ToString());
                    m_log.Error("That is " + (DateTime.Now - m_startuptime).ToString());
                    break;
                case "users":
                    m_log.Error(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname", "Agent ID", "Session ID", "Circuit", "IP", "World"));
                    for (int i = 0; i < m_localScenes.Count; i++)
                    {
                        Scene scene = m_localScenes[i];
                        foreach (EntityBase entity in scene.Entities.Values)
                        {
                            if (entity is ScenePresence)
                            {
                                ScenePresence scenePrescence = entity as ScenePresence;
                                if (!scenePrescence.childAgent)
                                {
                                    m_log.Error(
                                        String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}",
                                                      scenePrescence.Firstname,
                                                      scenePrescence.Lastname,
                                                      scenePrescence.UUID,
                                                      scenePrescence.ControllingClient.AgentId,
                                                      "Unknown",
                                                      "Unknown",
                                                      scene.RegionInfo.RegionName));
                                }
                            }
                        }
                    }
                    break;
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = "";
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] +" ";
            }
            result = result.TrimEnd(' ');
            return result;
        }
        #endregion
    }


}
