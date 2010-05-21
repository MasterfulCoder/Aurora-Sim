/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nwc.XmlRpc;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Login
{
    public class LLLoginHandlers
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ILoginService m_LocalService;

        private IConfigSource m_Config;

        public LLLoginHandlers(ILoginService service, IConfigSource config)
        {
            m_Config = config;
            m_LocalService = service;
        }

        public XmlRpcResponse HandleXMLRPCLogin(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData != null)
            {
                if (requestData.ContainsKey("first") && requestData["first"] != null &&
                    requestData.ContainsKey("last") && requestData["last"] != null &&
                    requestData.ContainsKey("passwd") && requestData["passwd"] != null)
                {
                    string first = requestData["first"].ToString();
                    string last = requestData["last"].ToString();
                    string passwd = requestData["passwd"].ToString();
                    string startLocation = string.Empty;
                    UUID scopeID = UUID.Zero;
                    if (requestData["scope_id"] != null)
                        scopeID = new UUID(requestData["scope_id"].ToString());
                    if (requestData.ContainsKey("start"))
                        startLocation = requestData["start"].ToString();

                    string clientVersion = "Unknown";
                    if (requestData.Contains("version"))
                        clientVersion = requestData["version"].ToString();
                    // We should do something interesting with the client version...

                    //MAC BANNING START
                    string mac = (string)requestData["mac"];
                    Aurora.Framework.IGenericData GD = Aurora.DataManager.DataManager.GetDefaultGenericPlugin();
                    if (GD == null)
                    {
                        Aurora.Services.DataService.LocalDataService IDS = new Aurora.Services.DataService.LocalDataService();
                        IDS.Initialise(m_Config);
                        GD = Aurora.DataManager.DataManager.GetDefaultGenericPlugin();
                    }
                    //We tried... might as well skip it
                    if (GD != null)
                    {
                        List<string> found = GD.Query("macAddress", mac, "macban", "*");
                        if (found.Count != 0)
                        {
                            m_log.InfoFormat("Mac is in the list");
                            return new XmlRpcResponse();
                        }
                        //MAC BANNING END

                        //Viewer Ban Start
                        List<string> clientfound = GD.Query("Client", clientVersion, "BannedViewers", "*");
                        if (clientfound.Count != 0)
                        {
                            return new XmlRpcResponse();
                        }

                        //Viewer ban end
                    }

                    m_log.InfoFormat("[LOGIN]: XMLRPC Login Requested for {0} {1}, starting in {2}, using {3}", first, last, startLocation, clientVersion);

                    LoginResponse reply = null;
                    reply = m_LocalService.Login(first, last, passwd, startLocation, scopeID, clientVersion, remoteClient, requestData);

                    XmlRpcResponse response = new XmlRpcResponse();
                    response.Value = reply.ToHashtable();
                    return response;

                }
            }

            return FailedXMLRPCResponse();

        }

        public XmlRpcResponse HandleXMLRPCSetLoginLevel(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];

            if (requestData != null)
            {
                if (requestData.ContainsKey("first") && requestData["first"] != null &&
                    requestData.ContainsKey("last") && requestData["last"] != null &&
                    requestData.ContainsKey("level") && requestData["level"] != null &&
                    requestData.ContainsKey("passwd") && requestData["passwd"] != null)
                {
                    string first = requestData["first"].ToString();
                    string last = requestData["last"].ToString();
                    string passwd = requestData["passwd"].ToString();
                    int level = Int32.Parse(requestData["level"].ToString());

                    m_log.InfoFormat("[LOGIN]: XMLRPC Set Level to {2} Requested by {0} {1}", first, last, level);

                    Hashtable reply = m_LocalService.SetLevel(first, last, passwd, level, remoteClient);

                    XmlRpcResponse response = new XmlRpcResponse();
                    response.Value = reply;

                    return response;

                }
            }

            XmlRpcResponse failResponse = new XmlRpcResponse();
            Hashtable failHash = new Hashtable();
            failHash["success"] = "false";
            failResponse.Value = failHash;

            return failResponse;

        }

        public OSD HandleLLSDLogin(OSD request, IPEndPoint remoteClient)
        {
            if (request.Type == OSDType.Map)
            {
                OSDMap map = (OSDMap)request;

                if (map.ContainsKey("first") && map.ContainsKey("last") && map.ContainsKey("passwd"))
                {
                    string startLocation = string.Empty;

                    if (map.ContainsKey("start"))
                        startLocation = map["start"].AsString();

                    UUID scopeID = UUID.Zero;

                    if (map.ContainsKey("scope_id"))
                        scopeID = new UUID(map["scope_id"].AsString());

                    m_log.Info("[LOGIN]: LLSD Login Requested for: '" + map["first"].AsString() + "' '" + map["last"].AsString() + "' / " + startLocation);

                    LoginResponse reply = null;
                    reply = m_LocalService.Login(map["first"].AsString(), map["last"].AsString(), map["passwd"].AsString(), startLocation, scopeID, "", remoteClient, new Hashtable());
                    return reply.ToOSDMap();

                }
            }

            return FailedOSDResponse();
        }

        private XmlRpcResponse FailedXMLRPCResponse()
        {
            Hashtable hash = new Hashtable();
            hash["reason"] = "key";
            hash["message"] = "Incomplete login credentials. Check your username and password.";
            hash["login"] = "false";

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = hash;

            return response;
        }

        private OSD FailedOSDResponse()
        {
            OSDMap map = new OSDMap();

            map["reason"] = OSD.FromString("key");
            map["message"] = OSD.FromString("Invalid login credentials. Check your username and passwd.");
            map["login"] = OSD.FromString("false");

            return map;
        }
    }

}