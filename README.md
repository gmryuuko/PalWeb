# 幻兽帕鲁服务器管理

## 功能

- 查看服务器状态
- 查看玩家列表，踢出、封禁
- 发送游戏内广播
- 修改服务器设置
- 重启服务器
- 游戏内 gm 指令快捷复制
  - 登录为管理员 `/AdminPassword <password>`
  - 传送 `/TeleportTo <player>`、`/TeleportToMe <player>`

## 运行环境

- [dotnet 9.0](https://dotnet.microsoft.com/en-us/download)

## 构建

```shell
dotnet publish Web/Web.csproj -c Release -o ./publish
```

## 运行配置

- 需使用[官方 docker 部署](https://github.com/pocketpairjp/palworld-dedicated-server-docker)。先运行一次生成配置文件。
- 需在 `PalWorldSettings.ini` 中将 `RESTAPIEnabled` 设置为 `True`

拷贝一份 `appsettings.Secrets.Template.json`，命名为 `appsettings.Secrets.json`，然后填写你的配置。

- `Username`：固定为 `admin`
- `Password`：填写 `PalWorldSettings.ini` 中的 `AdminPassword` 值
- `Url`：游戏服务器 REST API 地址，例如 `localhost:8212` 
- `Root`：`compose.yaml` 所在目录

## 部署

把 `publish` 目录下的文件放到你喜欢的地方，然后运行 `dotnet Web.dll`。

修改配置、重启服务器功能需部署到游戏服务所在的机器上。
