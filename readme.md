# 角色 POI 与小地图扩展（Mod）

## 概述

本 Mod 用于扩展游戏的小地图与角色兴趣点（POI），提供可配置的图标映射、动态 POI 管理、最小地图渲染增强与自定义输入支持。

## 主要功能

- 动态 POI 管理：通过 `Managers/CharacterPOIManager.cs` 跟踪角色与对象并在小地图上显示 POI。
- 小地图增强：`Extenders/MiniMapExtender.cs` 与 `Managers/CustomMinimapManager.cs` 提供额外图层、渲染钩子与图形控制。
- 可配置图标：使用 `Resources/config/iconConfig.json` 定义 POI 图标 ID 与资源路径，可在部署时替换或扩展图标集合。
- 运行时设置：`Managers/ModSettingManager.cs` 与 `Api/ModSettingAPI.cs` 暴露配置项与运行时 API，支持在代码或控制台中读取/修改设置。
- 扩展点：`Extenders/CharacterPOIExtender.cs` 可用于注入、修改或拦截 POI 行为。

## 要求

- 目标框架：`.NET Standard 2.1`
- 推荐 IDE：Visual Studio 或兼容的 .NET 开发环境

## 配置

- `Resources/config/iconConfig.json`：编辑图标 ID、路径与映射规则以更改 POI 的显示。请确保 JSON 格式有效且资源路径正确。