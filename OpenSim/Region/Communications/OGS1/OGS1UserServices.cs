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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.Communications.OGS1
{
    public class OGS1UserServices : IUserService, IAvatarService, IMessagingService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private CommunicationsManager m_commsManager;

        public OGS1UserServices(CommunicationsManager commsManager)
        {
            m_commsManager = commsManager;
        }

        public UserProfileData ConvertXMLRPCDataToUserProfile(Hashtable data)
        {
            if (data.Contains("error_type"))
            {
                m_log.Warn("[GRID]: " +
                           "Error sent by user server when trying to get user profile: (" +
                           data["error_type"] +
                           "): " + data["error_desc"]);
                return null;
            }

            UserProfileData userData = new UserProfileData();
            userData.FirstName = (string) data["firstname"];
            userData.SurName = (string) data["lastname"];
            userData.ID = new UUID((string) data["uuid"]);
            userData.Created = Convert.ToInt32(data["profile_created"]);
            userData.UserInventoryURI = (string) data["server_inventory"];
            userData.UserAssetURI = (string) data["server_asset"];
            userData.FirstLifeAboutText = (string) data["profile_firstlife_about"];
            userData.FirstLifeImage = new UUID((string) data["profile_firstlife_image"]);
            userData.CanDoMask = Convert.ToUInt32((string) data["profile_can_do"]);
            userData.WantDoMask = Convert.ToUInt32(data["profile_want_do"]);
            userData.AboutText = (string)data["profile_about"];
            userData.Image = new UUID((string) data["profile_image"]);
            userData.LastLogin = Convert.ToInt32((string) data["profile_lastlogin"]);
            userData.HomeRegion = Convert.ToUInt64((string) data["home_region"]);
            if (data.Contains("home_region_id"))
                userData.HomeRegionID = new UUID((string)data["home_region_id"]);
            else
                userData.HomeRegionID = UUID.Zero;
            userData.HomeLocation =
                new Vector3((float) Convert.ToDecimal((string) data["home_coordinates_x"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_y"]),
                              (float) Convert.ToDecimal((string) data["home_coordinates_z"]));
            userData.HomeLookAt =
                new Vector3((float) Convert.ToDecimal((string) data["home_look_x"]),
                              (float) Convert.ToDecimal((string) data["home_look_y"]),
                              (float) Convert.ToDecimal((string) data["home_look_z"]));
            if (data.Contains("user_flags"))
                userData.UserFlags = Convert.ToInt32((string) data["user_flags"]);
            if (data.Contains("god_level"))
                userData.GodLevel = Convert.ToInt32((string) data["god_level"]);

            if (data.Contains("custom_type"))
                userData.CustomType = (string) data["custom_type"];
            else
                userData.CustomType = "";
            if (userData.CustomType == null)
                userData.CustomType = "";

            if (data.Contains("partner"))
                userData.Partner = new UUID((string) data["partner"]);
            else
                userData.Partner = UUID.Zero;

            return userData;
        }

        /// <summary>
        /// Get a user agent from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserAgentData GetAgentByUUID(UUID userId)
        {
           try
           {
               Hashtable param = new Hashtable();
               param["avatar_uuid"] = userId.ToString();
               IList parameters = new ArrayList();
               parameters.Add(param);
               XmlRpcRequest req = new XmlRpcRequest("get_agent_by_uuid", parameters);
               XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 6000);
               Hashtable respData = (Hashtable) resp.Value;
               if (respData.Contains("error_type"))
               {
                   //m_log.Warn("[GRID]: " +
                   //           "Error sent by user server when trying to get agent: (" +
                   //           (string) respData["error_type"] +
                   //           "): " + (string)respData["error_desc"]);
                   return null;
               }
               UUID sessionid = UUID.Zero;

               UserAgentData userAgent = new UserAgentData();
               userAgent.Handle = Convert.ToUInt64((string)respData["handle"]);
               UUID.TryParse((string)respData["sessionid"], out sessionid);
               userAgent.SessionID = sessionid;

               if ((string)respData["agent_online"] == "TRUE")
               {
                   userAgent.AgentOnline = true;
               }
               else
               {
                   userAgent.AgentOnline = false;
               }

               return userAgent;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch agent data by uuid from remote user server: {0}",
                    e);
            }

            return null;
        }

        public AvatarAppearance ConvertXMLRPCDataToAvatarAppearance(Hashtable data)
        {
            if (data != null)
            {
                if (data.Contains("error_type"))
                {
                    m_log.Warn("[GRID]: " +
                               "Error sent by user server when trying to get user appearance: (" +
                               data["error_type"] +
                               "): " + data["error_desc"]);
                    return null;
                }
                else
                {
                    return new AvatarAppearance(data);
                }
            }
            else
            {
                m_log.Error("[GRID]: The avatar appearance is null, something bad happenend");
                return null;
            }
        }

        public List<AvatarPickerAvatar> ConvertXMLRPCDataToAvatarPickerList(UUID queryID, Hashtable data)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            int pickercount = Convert.ToInt32((string) data["avcount"]);
            UUID respqueryID = new UUID((string) data["queryid"]);
            if (queryID == respqueryID)
            {
                for (int i = 0; i < pickercount; i++)
                {
                    AvatarPickerAvatar apicker = new AvatarPickerAvatar();
                    UUID avatarID = new UUID((string) data["avatarid" + i.ToString()]);
                    string firstname = (string) data["firstname" + i.ToString()];
                    string lastname = (string) data["lastname" + i.ToString()];
                    apicker.AvatarID = avatarID;
                    apicker.firstName = firstname;
                    apicker.lastName = lastname;
                    pickerlist.Add(apicker);
                }
            }
            else
            {
                m_log.Warn("[OGS1 USER SERVICES]: Got invalid queryID from userServer");
            }
            return pickerlist;
        }

        public List<FriendListItem> ConvertXMLRPCDataToFriendListItemList(Hashtable data)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();
            int buddycount = Convert.ToInt32((string)data["avcount"]);


            for (int i = 0; i < buddycount; i++)
            {
                FriendListItem buddylistitem = new FriendListItem();

                buddylistitem.FriendListOwner = new UUID((string)data["ownerID" + i.ToString()]);
                buddylistitem.Friend = new UUID((string)data["friendID" + i.ToString()]);
                buddylistitem.FriendListOwnerPerms = (uint)Convert.ToInt32((string)data["ownerPerms" + i.ToString()]);
                buddylistitem.FriendPerms = (uint)Convert.ToInt32((string)data["friendPerms" + i.ToString()]);

                buddylist.Add(buddylistitem);
            }


            return buddylist;
        }

        /// <summary>
        /// Logs off a user on the user server
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionID">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="position">final position</param>
        /// <param name="lookat">final lookat</param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, Vector3 position, Vector3 lookat)
        {
            Hashtable param = new Hashtable();
            param["avatar_uuid"] = userid.Guid.ToString();
            param["region_uuid"] = regionid.Guid.ToString();
            param["region_handle"] = regionhandle.ToString();
            param["region_pos_x"] = position.X.ToString();
            param["region_pos_y"] = position.Y.ToString();
            param["region_pos_z"] = position.Z.ToString();
            param["lookat_x"] = lookat.X.ToString();
            param["lookat_y"] = lookat.Y.ToString();
            param["lookat_z"] = lookat.Z.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);
            XmlRpcRequest req = new XmlRpcRequest("logout_of_simulator", parameters);

            try
            {
                req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
            }
            catch (WebException)
            {
                m_log.Warn("[LOGOFF]: Unable to notify grid server of user logoff");
            }
        }

        /// <summary>
        /// Logs off a user on the user server (deprecated as of 2008-08-27)
        /// </summary>
        /// <param name="UserID">UUID of the user</param>
        /// <param name="regionID">UUID of the Region</param>
        /// <param name="regionhandle">regionhandle</param>
        /// <param name="posx">final position x</param>
        /// <param name="posy">final position y</param>
        /// <param name="posz">final position z</param>
        public void LogOffUser(UUID userid, UUID regionid, ulong regionhandle, float posx, float posy, float posz)
        {
            LogOffUser(userid, regionid, regionhandle, new Vector3(posx, posy, posz), new Vector3());
        }

        public UserProfileData GetUserProfile(string firstName, string lastName)
        {
            return GetUserProfile(firstName + " " + lastName);
        }

        public List<AvatarPickerAvatar> GenerateAgentPickerRequestResponse(UUID queryID, string query)
        {
            List<AvatarPickerAvatar> pickerlist = new List<AvatarPickerAvatar>();
            Regex objAlphaNumericPattern = new Regex("[^a-zA-Z0-9 ]");
            try
            {
                Hashtable param = new Hashtable();
                param["queryid"] = (string) queryID.ToString();
                param["avquery"] = objAlphaNumericPattern.Replace(query, String.Empty);
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_avatar_picker_avatar", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable) resp.Value;
                pickerlist = ConvertXMLRPCDataToAvatarPickerList(queryID, respData);
            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to fetch Avatar Picker Response: " +
                           e.Message);
                // Return Empty picker list (no results)
            }
            return pickerlist;
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(string name)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_name"] = name;
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_name", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 30000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch profile data by name from remote user server: {0}",
                    e);
            }

            return null;
        }

        /// <summary>
        /// Get a user profile from the user server
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns>null if the request fails</returns>
        public UserProfileData GetUserProfile(UUID avatarID)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["avatar_uuid"] = avatarID.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_by_uuid", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 30000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToUserProfile(respData);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[OGS1 USER SERVICES]: Error when trying to fetch profile data by uuid from remote user server: {0}",
                    e);
            }

            return null;
        }


        public void ClearUserAgent(UUID avatarID)
        {
            // TODO: implement
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName)
        {
            return SetupMasterUser(firstName, lastName, String.Empty);
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(string firstName, string lastName, string password)
        {
            UserProfileData profile = GetUserProfile(firstName, lastName);
            return profile;
        }

        /// <summary>
        /// Retrieve the user information for the given master uuid.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns></returns>
        public UserProfileData SetupMasterUser(UUID uuid)
        {
            UserProfileData data = GetUserProfile(uuid);

            if (data == null)
            {
                throw new Exception(
                    "Could not retrieve profile for master user " + uuid + ".  User server did not respond to the request.");
            }

            return data;
        }

        public UUID AddUserProfile(string firstName, string lastName, string pass, uint regX, uint regY)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        
        public bool ResetUserPassword(string firstName, string lastName, string newPassword)
        {
            throw new Exception("The method or operation is not implemented.");
        }        

        public bool UpdateUserProfile(UserProfileData userProfile)
        {
            m_log.Debug("[OGS1 USER SERVICES]: Asking UserServer to update profile.");
            Hashtable param = new Hashtable();
            param["avatar_uuid"] = userProfile.ID.ToString();
            //param["AllowPublish"] = userProfile.ToString();
            param["FLImageID"] = userProfile.FirstLifeImage.ToString();
            param["ImageID"] = userProfile.Image.ToString();
            //param["MaturePublish"] = MaturePublish.ToString();
            param["AboutText"] = userProfile.AboutText;
            param["FLAboutText"] = userProfile.FirstLifeAboutText;
            //param["ProfileURL"] = userProfile.ProfileURL.ToString();

            param["home_region"] = userProfile.HomeRegion.ToString();
            param["home_region_id"] = userProfile.HomeRegionID.ToString();

            param["home_pos_x"] = userProfile.HomeLocationX.ToString();
            param["home_pos_y"] = userProfile.HomeLocationY.ToString();
            param["home_pos_z"] = userProfile.HomeLocationZ.ToString();
            param["home_look_x"] = userProfile.HomeLookAtX.ToString();
            param["home_look_y"] = userProfile.HomeLookAtY.ToString();
            param["home_look_z"] = userProfile.HomeLookAtZ.ToString();
            param["user_flags"] = userProfile.UserFlags.ToString();
            param["god_level"] = userProfile.GodLevel.ToString();
            param["custom_type"] = userProfile.CustomType.ToString();
            param["partner"] = userProfile.Partner.ToString();

            IList parameters = new ArrayList();
            parameters.Add(param);

            XmlRpcRequest req = new XmlRpcRequest("update_user_profile", parameters);
            XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
            Hashtable respData = (Hashtable)resp.Value;
            if (respData != null)
            {
                if (respData.Contains("returnString"))
                {
                    if (((string)respData["returnString"]).ToUpper() != "TRUE")
                    {
                        m_log.Warn("[GRID]: Unable to update user profile, User Server Reported an issue");
                        return false;
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update user profile, UserServer didn't understand me!");
                    return false;
                }
            }
            else
            {
                m_log.Warn("[GRID]: Unable to update user profile, UserServer didn't understand me!");
                return false;
            }
            
            return true;
        }

        #region IUserServices Friend Methods
        /// <summary>
        /// Adds a new friend to the database for XUser
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being added to</param>
        /// <param name="friend">The agent that being added to the friends list of the friends list owner</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void AddNewUserFriend(UUID friendlistowner, UUID friend, uint perms)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.Guid.ToString();
                param["friendID"] = friend.Guid.ToString();
                param["friendPerms"] = perms.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("add_new_user_friend", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to add new friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to add new friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to add new friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to AddNewUserFriend: " +
                           e.Message);

            }

        }

        /// <summary>
        /// Delete friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The Ex-friend agent</param>
        public void RemoveUserFriend(UUID friendlistowner, UUID friend)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.Guid.ToString();
                param["friendID"] = friend.Guid.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("remove_user_friend", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to remove friend, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to remove friend, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to remove friend, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to RemoveUserFriend: " +
                           e.Message);

            }
        }

        /// <summary>
        /// Update permissions for friend on friendlistowner's friendlist.
        /// </summary>
        /// <param name="friendlistowner">The agent that who's friends list is being updated</param>
        /// <param name="friend">The agent that is getting or loosing permissions</param>
        /// <param name="perms">A uint bit vector for set perms that the friend being added has; 0 = none, 1=This friend can see when they sign on, 2 = map, 4 edit objects </param>
        public void UpdateUserFriendPerms(UUID friendlistowner, UUID friend, uint perms)
        {
            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.Guid.ToString();
                param["friendID"] = friend.Guid.ToString();
                param["friendPerms"] = perms.ToString();
                IList parameters = new ArrayList();
                parameters.Add(param);

                XmlRpcRequest req = new XmlRpcRequest("update_user_friend_perms", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 3000);
                Hashtable respData = (Hashtable)resp.Value;
                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to update_user_friend_perms, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to update_user_friend_perms, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update_user_friend_perms, UserServer didn't understand me!");

                }
            }
            catch (WebException e)
            {
                m_log.Warn("[GRID]: Error when trying to update_user_friend_perms: " +
                           e.Message);
            }
        }
        /// <summary>
        /// Returns a list of FriendsListItems that describe the friends and permissions in the friend relationship for UUID friendslistowner
        /// </summary>
        /// <param name="friendlistowner">The agent that we're retreiving the friends Data.</param>
        public List<FriendListItem> GetUserFriendList(UUID friendlistowner)
        {
            List<FriendListItem> buddylist = new List<FriendListItem>();

            try
            {
                Hashtable param = new Hashtable();
                param["ownerID"] = friendlistowner.Guid.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_user_friend_list", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;

                if (respData != null && respData.Contains("avcount"))
                {
                    buddylist = ConvertXMLRPCDataToFriendListItemList(respData);
                }

            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to fetch Avatar's friends list: " +
                           e.Message);
                // Return Empty list (no friends)
            }
            return buddylist;
        }

        public Dictionary<UUID, FriendRegionInfo> GetFriendRegionInfos (List<UUID> uuids)
        {
            Dictionary<UUID, FriendRegionInfo> result = new Dictionary<UUID, FriendRegionInfo>();

            // ask MessageServer about the current on-/offline status and regions the friends are in
            ArrayList parameters = new ArrayList();
            Hashtable map = new Hashtable();

            ArrayList list = new ArrayList();
            foreach (UUID uuid in uuids)
            {
                list.Add(uuid.ToString());
                list.Add(uuid.ToString());
            }
            map["uuids"] = list;

            map["recv_key"] = m_commsManager.NetworkServersInfo.UserRecvKey;
            map["send_key"] = m_commsManager.NetworkServersInfo.UserSendKey;

            parameters.Add(map);

            try {
                XmlRpcRequest req = new XmlRpcRequest("get_presence_info_bulk", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.MessagingURL, 8000);
                Hashtable respData = resp != null ? (Hashtable) resp.Value : null;

                if (respData == null || respData.ContainsKey("faultMessage"))
                {
                    m_log.WarnFormat("[OGS1 USER SERVICES]: Contacting MessagingServer about user-regions resulted in error: {0}",
                                     respData == null ? "<unknown error>" : respData["faultMessage"]);
                }
                else if (!respData.ContainsKey("count"))
                {
                    m_log.WarnFormat("[OGS1 USER SERVICES]: Wrong format in response for MessagingServer request get_presence_info_bulk: missing 'count' field");
                }
                else
                {
                    int count = (int)respData["count"];
                    m_log.DebugFormat("[OGS1 USER SERVICES]: Request returned {0} results.", count);
                    for (int i = 0; i < count; ++i)
                    {
                        if (respData.ContainsKey("uuid_" + i) && respData.ContainsKey("isOnline_" + i) && respData.ContainsKey("regionHandle_" + i))
                        {
                            UUID uuid;
                            if (UUID.TryParse((string)respData["uuid_" + i], out uuid))
                            {
                                FriendRegionInfo info = new FriendRegionInfo();
                                info.isOnline = (bool)respData["isOnline_" + i];
                                if (info.isOnline)
                                {
                                    // TODO remove this after the next protocol update (say, r7800?)
                                    info.regionHandle = Convert.ToUInt64(respData["regionHandle_" + i]);

                                    // accept missing id
                                    if (respData.ContainsKey("regionID_" + i))
                                        UUID.TryParse((string)respData["regionID_" + i], out info.regionID);
                                }

                                result.Add(uuid, info);
                            }
                        }
                        else
                        {
                            m_log.WarnFormat("[OGS1 USER SERVICES]: Response to get_presence_info_bulk contained an error in entry {0}", i);
                        }
                    }
                }
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 USER SERVICES]: Network problems when trying to fetch friend infos: {0}", e.Message);
            }
            
            m_log.DebugFormat("[OGS1 USER SERVICES]: Returning {0} entries", result.Count);
            return result;
        }

        #endregion

        /// Appearance
        public AvatarAppearance GetUserAppearance(UUID user)
        {
            AvatarAppearance appearance = null;

            try
            {
                Hashtable param = new Hashtable();
                param["owner"] = user.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("get_avatar_appearance", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;

                return ConvertXMLRPCDataToAvatarAppearance(respData);
            }
            catch (WebException e)
            {
                m_log.ErrorFormat("[OGS1 USER SERVICES]: Network problems when trying to fetch appearance for avatar {0}, {1}", user, e.Message);
            }

            return appearance;
        }

        public void UpdateUserAppearance(UUID user, AvatarAppearance appearance)
        {
            try
            {
                Hashtable param = appearance.ToHashTable();
                param["owner"] = user.ToString();

                IList parameters = new ArrayList();
                parameters.Add(param);
                XmlRpcRequest req = new XmlRpcRequest("update_avatar_appearance", parameters);
                XmlRpcResponse resp = req.Send(m_commsManager.NetworkServersInfo.UserURL, 8000);
                Hashtable respData = (Hashtable) resp.Value;

                if (respData != null)
                {
                    if (respData.Contains("returnString"))
                    {
                        if ((string)respData["returnString"] == "TRUE")
                        {

                        }
                        else
                        {
                            m_log.Warn("[GRID]: Unable to update_user_appearance, User Server Reported an issue");
                        }
                    }
                    else
                    {
                        m_log.Warn("[GRID]: Unable to update_user_appearance, UserServer didn't understand me!");
                    }
                }
                else
                {
                    m_log.Warn("[GRID]: Unable to update_user_appearance, UserServer didn't understand me!");
                }
            }
            catch (WebException e)
            {
                m_log.Warn("[OGS1 USER SERVICES]: Error when trying to update Avatar's appearance: " +
                           e.Message);
                // Return Empty list (no friends)
            }
        }
    }
}
