# Windows Rogue Software Cleaner

Windows 流氓软件/异常组件扫描与清理工具。先扫描识别，再让用户确认，最后执行可恢复清理。

## 功能

### 扫描识别（7 个扫描源）
| 扫描源 | 说明 |
|---|---|
| 当前运行进程 | 枚举所有正在运行的进程，提取路径和厂商信息 |
| 注册表启动项 | 读取 HKCU Run 键值 |
| 启动文件夹 | 读取当前用户 Startup 目录 |
| 系统服务 | 枚举所有 Windows 服务，含启动类型和状态 |
| 计划任务 | 通过 schtasks 枚举所有计划任务 |
| 已安装程序 | 读取 HKLM 下的 Uninstall 注册表项 |
| 目录扫描 | 检查 AppData、Program Files、Temp 等关键目录 |

### 风险分级
- **高风险**：Temp 目录中的程序、含恶意关键词、无签名且行为异常
- **中风险**：自启动项、AppData 中的程序、无厂商信息、可疑关键词
- **低风险**：正常软件的可选组件、知名厂商的程序

### 可恢复清理
- 停止进程 → 可恢复
- 禁用注册表启动项（备份后移除）→ **可还原**
- 移动启动文件夹文件到隔离区 → **可还原**
- 禁用服务 → **可还原**
- 禁用计划任务 → **可还原**
- 隔离文件到隔离区 → **可还原**

### 清理后验证
逐项检查目标进程是否停止、服务是否禁用、任务是否禁用、文件是否移除。

### 隔离区
所有被清理的项目都记录在隔离区中，支持一键还原。

### 报告
导出三种 JSON 报告：
- `scan_report_*.json` — 完整扫描结果
- `clean_plan_*.json` — 用户选择的清理计划
- `clean_result_*.json` — 执行结果（含成功/失败详情）

## 系统要求

- Windows 10 / 11
- .NET 8.0 Desktop Runtime
- 管理员权限（用于扫描服务和计划任务，清理时需要）

## 快速开始

### 方式一：直接运行（需 .NET 8 SDK）

```powershell
git clone https://github.com/123com92/DeleteApp.git
cd DeleteApp
dotnet run --project src/DeleteApp
```

### 方式二：编译后运行

```powershell
git clone https://github.com/123com92/DeleteApp.git
cd DeleteApp
dotnet publish src/DeleteApp -c Release -o publish
.\publish\DeleteApp.exe
```

### 方式三：使用打包脚本

```powershell
.\publish.ps1
# 打包输出到 publish/ 目录，直接运行 publish/DeleteApp.exe
```

## 使用流程

1. 启动程序（需要管理员权限，会自动请求 UAC 提权）
2. 点击 **刷新扫描** → 等待扫描完成
3. 在各 Tab 页查看结果：
   - **全部** — 汇总视图
   - **进程** — 停止选中进程
   - **启动项** — 禁用选中启动项
   - **服务** — 禁用选中服务
   - **计划任务** — 禁用选中任务
   - **安装程序** — 查看已安装程序
   - **快捷方式** — 桌面快捷方式
4. 勾选要清理的项目，点击 **执行清理**
5. 确认弹窗会列出具体操作摘要
6. 如需反悔 → 去 **隔离区** Tab → 勾选 → 点击 **还原选中**
7. 查看 **报告** Tab 浏览生成的所有 JSON 报告

## 项目结构

```
DeleteApp/
├── AGENTS.md              # 项目开发规范
├── README.md              # 使用说明
├── publish.ps1            # 一键打包发布脚本
├── .gitignore
└── src/
    └── DeleteApp/
        ├── DeleteApp.csproj
        ├── App.xaml / App.xaml.cs
        ├── MainWindow.xaml / MainWindow.xaml.cs
        ├── app.manifest
        ├── Core/
        │   ├── Cleaner/       # 清理执行器
        │   ├── Quarantine/    # 隔离区管理
        │   ├── Report/        # 报告导出
        │   ├── RiskEngine/    # 风险分级引擎
        │   ├── Scanner/       # 扫描接口与组合
        │   └── Verifier/      # 清理后验证
        ├── Data/
        │   └── Models/        # 数据模型
        ├── UI/
        │   ├── Commands/      # MVVM 命令
        │   └── ViewModels/    # 视图模型
        ├── Utils/
        │   ├── Hash/          # SHA256 哈希
        │   ├── Logger/        # 本地日志
        │   └── PathSafe/      # 路径安全校验
        └── Windows/
            ├── DesktopShortcutReader/
            ├── DirectoryReader/
            ├── InstalledProgramsReader/
            ├── ProcessReader/
            ├── ServiceReader/
            ├── StartupReader/
            └── TaskSchedulerReader/
```

## 数据存储

| 数据 | 路径 |
|---|---|
| 日志 | `%LocalAppData%\DeleteApp\logs\app.log` |
| 报告 | `%LocalAppData%\DeleteApp\reports\` |
| 隔离区清单 | `%LocalAppData%\DeleteApp\quarantine\manifest.json` |
| 隔离文件 | `%LocalAppData%\DeleteApp\quarantine\` |

## 技术栈

- **语言**：C# 12
- **框架**：.NET 8 + WPF
- **系统接口**：Windows API (System.Management, ServiceController, Registry)
- **日志**：本地文件日志
- **报告**：JSON

## 安全原则

- 所有清理操作执行前必须用户确认
- 默认不得全选高风险项目
- 所有删除操作优先进入隔离区
- 支持一键还原
- 不静默删除文件
- 不绕过 Windows 权限控制
- 不上传用户隐私数据

## 执照

MIT License
