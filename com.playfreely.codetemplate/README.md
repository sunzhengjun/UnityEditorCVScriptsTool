# CV Scripts Tool (代码模板/复制工具)

一个用于在 Unity 编辑器中集中管理与快速复制代码模板的工具。支持搜索、排序、拖拽调整顺序、示例库导入等。

## 安装（UPM via Git）
> Unity 2022.3 及以上（你当前为 2022.3.40f1）。

1. 把本项目上传到你的 GitHub 仓库，比如：`https://github.com/<yourname>/UnityCodeTemplateTool`
2. 打 tag：`v1.0.0`
3. Unity 中打开 Package Manager → **Add package from git URL**，粘贴：

```
https://github.com/<yourname>/UnityCodeTemplateTool.git?path=com.playfreely.codetemplate#v1.0.0
```

> 若你把包放在仓库根目录，则可省略 `?path=`。

## 使用
- 菜单：**Tools/代码CV战士**
- 首次打开时会在 `Assets/Code/Editor/Windows/CodeTemplateLibrary.asset` 自动创建或定位模板库；
  你也可以在 Package Manager 右侧 **Samples** 区导入 *Starter Library* 获取示例。

## 目录结构
```
com.playfreely.codetemplate/
├─ package.json
├─ Editor/
│  ├─ CodeTemplateEditorWindow.cs
│  ├─ CodeTemplateLibrary.cs
│  └─ PlayFreely.CodeTemplate.Editor.asmdef
└─ Samples~/
   └─ Starter Library/
      └─ CodeTemplateLibrary.asset
```

## 许可
MIT
