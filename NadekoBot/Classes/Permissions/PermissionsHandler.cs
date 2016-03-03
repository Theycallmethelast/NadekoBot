﻿using Discord;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;

namespace NadekoBot.Classes.Permissions {
    public static class PermissionsHandler {
        public static ConcurrentDictionary<ulong, ServerPermissions> PermissionsDict =
            new ConcurrentDictionary<ulong, ServerPermissions>();

        public enum PermissionBanType {
            None, ServerBanCommand, ServerBanModule,
            ChannelBanCommand, ChannelBanModule, RoleBanCommand,
            RoleBanModule, UserBanCommand, UserBanModule
        }


        public static void Initialize() {
            Console.WriteLine("Reading from the permission files.");
            Directory.CreateDirectory("data/permissions");
            foreach (var file in Directory.EnumerateFiles("data/permissions/")) {
                try {
                    var strippedFileName = Path.GetFileNameWithoutExtension(file);
                    if (string.IsNullOrWhiteSpace(strippedFileName)) continue;
                    var id = ulong.Parse(strippedFileName);
                    var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ServerPermissions>(File.ReadAllText(file));
                    PermissionsDict.TryAdd(id, data);
                } catch { }
            }
            Console.WriteLine("Permission initialization complete.");
        }

        internal static Permissions GetRolePermissionsById(Server server, ulong id) {
            ServerPermissions serverPerms;
            if (!PermissionsDict.TryGetValue(server.Id, out serverPerms))
                return null;

            Permissions toReturn;
            serverPerms.RolePermissions.TryGetValue(id, out toReturn);
            return toReturn;
        }

        internal static Permissions GetUserPermissionsById(Server server, ulong id) {
            ServerPermissions serverPerms;
            if (!PermissionsDict.TryGetValue(server.Id, out serverPerms))
                return null;

            Permissions toReturn;
            serverPerms.UserPermissions.TryGetValue(id, out toReturn);
            return toReturn;
        }

        internal static Permissions GetChannelPermissionsById(Server server, ulong id) {
            ServerPermissions serverPerms;
            if (!PermissionsDict.TryGetValue(server.Id, out serverPerms))
                return null;

            Permissions toReturn;
            serverPerms.ChannelPermissions.TryGetValue(id, out toReturn);
            return toReturn;
        }

        internal static Permissions GetServerPermissions(Server server) {
            ServerPermissions serverPerms;
            return !PermissionsDict.TryGetValue(server.Id, out serverPerms) ? null : serverPerms.Permissions;
        }

        internal static PermissionBanType GetPermissionBanType(Command command, User user, Channel channel) {
            var server = user.Server;
            ServerPermissions serverPerms;
            if (!PermissionsDict.TryGetValue(server.Id, out serverPerms)) {
                serverPerms = new ServerPermissions(server.Id, server.Name);
                PermissionsDict.TryAdd(server.Id, serverPerms);
            }
            bool val;
            Permissions perm;
            //server
            if (serverPerms.Permissions.Modules.TryGetValue(command.Category, out val) && val == false)
                return PermissionBanType.ServerBanModule;
            if (serverPerms.Permissions.Commands.TryGetValue(command.Text, out val) && val == false)
                return PermissionBanType.ServerBanCommand;
            //channel
            if (serverPerms.ChannelPermissions.TryGetValue(channel.Id, out perm) &&
                perm.Modules.TryGetValue(command.Category, out val) && val == false)
                return PermissionBanType.ChannelBanModule;
            if (serverPerms.ChannelPermissions.TryGetValue(channel.Id, out perm) &&
                perm.Commands.TryGetValue(command.Text, out val) && val == false)
                return PermissionBanType.ChannelBanCommand;

            //ROLE PART - TWO CASES
            // FIRST CASE:
            // IF EVERY ROLE USER HAS IS BANNED FROM THE MODULE,
            // THAT MEANS USER CANNOT RUN THIS COMMAND
            // IF AT LEAST ONE ROLE EXIST THAT IS NOT BANNED,
            // USER CAN RUN THE COMMAND
            var foundNotBannedRole = false;
            foreach (var role in user.Roles) {
                //if every role is banned from using the module -> rolebanmodule
                if (serverPerms.RolePermissions.TryGetValue(role.Id, out perm) &&
                perm.Modules.TryGetValue(command.Category, out val) && val == false)
                    continue;
                foundNotBannedRole = true;
                break;
            }
            if (!foundNotBannedRole)
                return PermissionBanType.RoleBanModule;

            // SECOND CASE:
            // IF EVERY ROLE USER HAS IS BANNED FROM THE COMMAND,
            // THAT MEANS USER CANNOT RUN THAT COMMAND
            // IF AT LEAST ONE ROLE EXISTS THAT IS NOT BANNED,
            // USER CAN RUN THE COMMAND
            foundNotBannedRole = false;
            foreach (var role in user.Roles) {
                //if every role is banned from using the module -> rolebanmodule
                if (serverPerms.RolePermissions.TryGetValue(role.Id, out perm) &&
                perm.Commands.TryGetValue(command.Text, out val) && val == false)
                    continue;
                else {
                    foundNotBannedRole = true;
                    break;
                }
            }
            if (!foundNotBannedRole)
                return PermissionBanType.RoleBanCommand;

            //user
            if (serverPerms.UserPermissions.TryGetValue(user.Id, out perm) &&
                perm.Modules.TryGetValue(command.Category, out val) && val == false)
                return PermissionBanType.UserBanModule;
            if (serverPerms.UserPermissions.TryGetValue(user.Id, out perm) &&
                perm.Commands.TryGetValue(command.Text, out val) && val == false)
                return PermissionBanType.UserBanCommand;

            return PermissionBanType.None;
        }

        private static void WriteServerToJson(ServerPermissions serverPerms) {
            string pathToFile = $"data/permissions/{serverPerms}.json";
            File.WriteAllText(pathToFile,
                Newtonsoft.Json.JsonConvert.SerializeObject(serverPerms, Newtonsoft.Json.Formatting.Indented));
        }

        public static void WriteToJson() {
            Directory.CreateDirectory("data/permissions/");
            foreach (var kvp in PermissionsDict) {
                WriteServerToJson(kvp.Value);
            }
        }

        public static string GetServerPermissionsRoleName(Server server) {
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            return serverPerms.PermissionsControllerRole;
        }

        internal static void SetPermissionsRole(Server server, string roleName) {
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            serverPerms.PermissionsControllerRole = roleName;
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        internal static void SetVerbosity(Server server, bool val) {
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            serverPerms.Verbose = val;
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetServerModulePermission(Server server, string moduleName, bool value) {
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            var modules = serverPerms.Permissions.Modules;
            if (modules.ContainsKey(moduleName))
                modules[moduleName] = value;
            else
                modules.Add(moduleName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetServerCommandPermission(Server server, string commandName, bool value) {
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            var commands = serverPerms.Permissions.Commands;
            if (commands.ContainsKey(commandName))
                commands[commandName] = value;
            else
                commands.Add(commandName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetChannelModulePermission(Channel channel, string moduleName, bool value) {
            var server = channel.Server;

            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            if (!serverPerms.ChannelPermissions.ContainsKey(channel.Id))
                serverPerms.ChannelPermissions.Add(channel.Id, new Permissions(channel.Name));

            var modules = serverPerms.ChannelPermissions[channel.Id].Modules;

            if (modules.ContainsKey(moduleName))
                modules[moduleName] = value;
            else
                modules.Add(moduleName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetChannelCommandPermission(Channel channel, string commandName, bool value) {
            var server = channel.Server;
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            if (!serverPerms.ChannelPermissions.ContainsKey(channel.Id))
                serverPerms.ChannelPermissions.Add(channel.Id, new Permissions(channel.Name));

            var commands = serverPerms.ChannelPermissions[channel.Id].Commands;

            if (commands.ContainsKey(commandName))
                commands[commandName] = value;
            else
                commands.Add(commandName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetRoleModulePermission(Role role, string moduleName, bool value) {
            var server = role.Server;
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            if (!serverPerms.RolePermissions.ContainsKey(role.Id))
                serverPerms.RolePermissions.Add(role.Id, new Permissions(role.Name));

            var modules = serverPerms.RolePermissions[role.Id].Modules;

            if (modules.ContainsKey(moduleName))
                modules[moduleName] = value;
            else
                modules.Add(moduleName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetRoleCommandPermission(Role role, string commandName, bool value) {
            var server = role.Server;
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            if (!serverPerms.RolePermissions.ContainsKey(role.Id))
                serverPerms.RolePermissions.Add(role.Id, new Permissions(role.Name));

            var commands = serverPerms.RolePermissions[role.Id].Commands;

            if (commands.ContainsKey(commandName))
                commands[commandName] = value;
            else
                commands.Add(commandName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetUserModulePermission(User user, string moduleName, bool value) {
            var server = user.Server;
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));

            if (!serverPerms.UserPermissions.ContainsKey(user.Id))
                serverPerms.UserPermissions.Add(user.Id, new Permissions(user.Name));

            var modules = serverPerms.UserPermissions[user.Id].Modules;

            if (modules.ContainsKey(moduleName))
                modules[moduleName] = value;
            else
                modules.Add(moduleName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }

        public static void SetUserCommandPermission(User user, string commandName, bool value) {
            var server = user.Server;
            var serverPerms = PermissionsDict.GetOrAdd(server.Id,
                new ServerPermissions(server.Id, server.Name));
            if (!serverPerms.UserPermissions.ContainsKey(user.Id))
                serverPerms.UserPermissions.Add(user.Id, new Permissions(user.Name));

            var commands = serverPerms.UserPermissions[user.Id].Commands;

            if (commands.ContainsKey(commandName))
                commands[commandName] = value;
            else
                commands.Add(commandName, value);
            Task.Run(() => WriteServerToJson(serverPerms));
        }
    }
    /// <summary>
    /// Holds a permission list
    /// </summary>
    public class Permissions {
        /// <summary>
        /// Name of the parent object whose permissions these are
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Module name with allowed/disallowed
        /// </summary>
        public Dictionary<string, bool> Modules { get; set; }
        /// <summary>
        /// Command name with allowed/disallowed
        /// </summary>
        public Dictionary<string, bool> Commands { get; set; }

        public Permissions(string name) {
            Name = name;
            Modules = new Dictionary<string, bool>();
            Commands = new Dictionary<string, bool>();
        }

        public override string ToString() {
            var toReturn = "";
            var bannedModules = Modules.Where(kvp => kvp.Value == false);
            var bannedModulesArray = bannedModules as KeyValuePair<string, bool>[] ?? bannedModules.ToArray();
            if (bannedModulesArray.Any()) {
                toReturn += "`Banned Modules:`\n";
                toReturn = bannedModulesArray.Aggregate(toReturn, (current, m) => current + $"\t`[x]  {m.Key}`\n");
            }
            var bannedCommands = Commands.Where(kvp => kvp.Value == false);
            var bannedCommandsArr = bannedCommands as KeyValuePair<string, bool>[] ?? bannedCommands.ToArray();
            if (bannedCommandsArr.Any()) {
                toReturn += "`Banned Commands:`\n";
                toReturn = bannedCommandsArr.Aggregate(toReturn, (current, c) => current + $"\t`[x]  {c.Key}`\n");
            }
            return toReturn;
        }
    }

    public class ServerPermissions {
        /// <summary>
        /// The guy who can edit the permissions
        /// </summary>
        public string PermissionsControllerRole { get; set; }
        /// <summary>
        /// Does it print the error when a restriction occurs
        /// </summary>
        public bool Verbose { get; set; }
        /// <summary>
        /// The id of the thing (user/server/channel)
        /// </summary>
        public ulong Id { get; set; } //a string because of the role name.
        /// <summary>
        /// Permission object bound to the id of something/role name
        /// </summary>
        public Permissions Permissions { get; set; }

        public Dictionary<ulong, Permissions> UserPermissions { get; set; }
        public Dictionary<ulong, Permissions> ChannelPermissions { get; set; }
        public Dictionary<ulong, Permissions> RolePermissions { get; set; }

        public ServerPermissions(ulong id, string name) {
            Id = id;
            PermissionsControllerRole = "Nadeko";
            Verbose = true;

            Permissions = new Permissions(name);
            UserPermissions = new Dictionary<ulong, Permissions>();
            ChannelPermissions = new Dictionary<ulong, Permissions>();
            RolePermissions = new Dictionary<ulong, Permissions>();
        }
    }
}