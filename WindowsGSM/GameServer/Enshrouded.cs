﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;

namespace WindowsGSM.GameServer
{
    class Enshrouded
    {
        // 存储服务器配置数据的对象
        private readonly ServerConfig _serverData;

        // 错误消息和通知消息
        public string Error;
        public string Notice;

        // 服务器全名、启动路径、是否允许嵌入控制台、端口号增量、查询方法等服务器信息
        public const string FullName = "雾锁王国 专用服务器";
        public string StartPath = @"enshrouded_server.exe";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 2;
        public dynamic QueryMethod = new Query.A2S();

        // 默认配置项：端口号、查询端口、默认地图、最大玩家数、额外参数及应用 ID
        public string Port = "15636";
        public string QueryPort = "15637";
        public string Defaultmap = "Dedicated";
        public string Maxplayers = "16";
        public string Additional = $""; // 额外的服务器启动参数
        public string AppId = "2278520";

        // 构造函数，需要传入服务器配置数据对象
        public Enshrouded(Functions.ServerConfig serverData)
        {
            _serverData = serverData;
        }

        // - 在安装后为游戏服务器创建一个默认的 cfg
        public async void CreateServerCFG()
        {
            var serverConfig = new
            {
                name = $"{_serverData.ServerName}",
                password = "",
                saveDirectory = "./savegame",
                logDirectory = "./logs",
                ip = $"{_serverData.ServerIP}",
                gamePort = _serverData.ServerPort,
                queryPort = _serverData.ServerQueryPort,
                slotCount = _serverData.ServerMaxPlayer
            };

            // Convert the object to JSON format
            string jsonContent = JsonConvert.SerializeObject(serverConfig, Formatting.Indented);

            // Specify the file path
            string filePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "enshrouded_server.json");

            // Write the JSON content to the file
            File.WriteAllText(filePath, jsonContent);
        }

        // 启动服务器进程
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} 未找到 ({shipExePath})";
                return null;
            }
            string param = $" {_serverData.ServerParam} ";
            param += $"-ip=\"{_serverData.ServerIP}\" ";
            param += $"-gamePort={_serverData.ServerPort} ";
            param += $"-queryPort={_serverData.ServerQueryPort} ";
            param += $"-slotCount={_serverData.ServerMaxPlayer} ";
            param += $"-name=\"\"\"{_serverData.ServerName}\"\"\"";

            UpdateConfig();
            // 创建进程，并设置启动参数
            Process p;
            if (!AllowsEmbedConsole)
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = shipExePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Minimized,
                        UseShellExecute = false
                    },
                    EnableRaisingEvents = true
                };
                p.Start();
            }
            else
            {
                p = new Process
                {
                    StartInfo =
                    {
                        WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                        FileName = shipExePath,
                        Arguments = param,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                var serverConsole = new Functions.ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            return p;
        }
        public void UpdateConfig()
        {
            var serverConfig = new
            {
                name = $"{_serverData.ServerName}",
                ip = $"{_serverData.ServerIP}",
                gamePort = _serverData.ServerPort,
                queryPort = _serverData.ServerQueryPort,
                slotCount = _serverData.ServerMaxPlayer
            };

            // Convert the object to JSON format
            string jsonContent = JsonConvert.SerializeObject(serverConfig, Formatting.Indented);

            // Specify the file path
            string filePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "enshrouded_server.json");

            // Write the JSON content to the file
            File.WriteAllText(filePath, jsonContent);
        }

        // - Stop server function
        public async Task Stop(Process p)
        {

            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
            });
            await Task.Delay(20000);

        }

        // 安装游戏服务端
        public async Task<Process> Install()
        {
            var steamCMD = new Installer.SteamCMD();
            // 使用 SteamCMD 安装服务端
            Process p = await steamCMD.Install(_serverData.ServerID, string.Empty, AppId);
            Error = steamCMD.Error;

            return p;
        }

        // 升级游戏服务端
        public async Task<Process> Update(bool validate = false, string custom = null)
        {
            // 使用 SteamCMD 更新服务端
            var (p, error) = await Installer.SteamCMD.UpdateEx(_serverData.ServerID, AppId, validate, custom: custom);
            Error = error;
            if (error != null && p != null)
            {
                UpdateConfig();
            }
            return p;
        }

        // 在服务器文件夹中检查游戏服务端是否已正确安装
        public bool IsInstallValid()
        {
            return File.Exists(Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, "enshrouded_server.exe"));
        }

        public bool IsImportValid(string path)
        {
            string exePath = Path.Combine(path, StartPath);
            Error = $"无效路径！找不到 {Path.GetFileName(exePath)}";
            return File.Exists(exePath);
        }

        // 获取本地游戏服务端的版本号
        public string GetLocalBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return steamCMD.GetLocalBuild(_serverData.ServerID, AppId);
        }

        // 获取官方游戏服务端的版本号
        public async Task<string> GetRemoteBuild()
        {
            var steamCMD = new Installer.SteamCMD();
            return await steamCMD.GetRemoteBuild(AppId);
        }
    }
}
