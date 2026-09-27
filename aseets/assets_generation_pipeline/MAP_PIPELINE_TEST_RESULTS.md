# 地图流水线测试结果

## 测试环境与输入

- Python 3.11.9；Pillow、NumPy、OpenCV 4.11.0。
- Godot 4.7.2 stable mono，headless 实际运行，不只是文本格式检查。
- 输入：`source/maps` 中已有的 31 张 PNG，未改动源图。
- 参数：语义图最长边 1024、场景最长边 128 格、每格 32 世界像素、建筑默认可摧毁。

## 已执行验证

| 检查 | 结果 |
| --- | --- |
| Python unittest | 5/5 通过 |
| 31 张实际输入批量生成 | 31 成功、0 失败 |
| 原生 map.tscn 场景加载和实例化 | 31/31 通过 |
| 栅格数量与尺寸一致性 | 通过 |
| 六类瓦片 custom data | 通过 |
| river/mountain/两种 buildings 碰撞多边形；default/snow 无碰撞 | 通过 |
| 世界坐标与格坐标换算 | 通过 |
| 可摧毁建筑承受 40 + 60 伤害后变为 default，移除碰撞定义 | 通过 |
| 不可摧毁建筑承受 1000 伤害仍保留 | 通过 |
| 越界访问返回 -1 | 通过 |
| 白纸不识别为积雪 | 通过 |
| 六类人工 ID 图缩放、分区投票及场景序列化 | 通过 |
| 非法 ID/RGB 标注输入拒绝 | 通过 |
| 93 张生成 PNG 完整解码及 mask ID/尺寸 | 通过 |
| 巴黎、波兹南、华沙场景独立 headless 启动、运行 5 帧 | 通过 |

Godot 测试输出：`SEMANTIC_MAP_TEST scenes=31 failures=0`。

## 分类质量检查：不等于准确率验证

已查看 `generated_maps/overview.png` 汇总预览，存在明显的启发式分类问题：

- `USSR_Vladivostok` 实际场景格全部是 default，当前草稿不具备该城市地理结构；需要检查源图裁切、对比度并提供标注。
- 多张图把图框、道路、文字或等高线当作建筑。慕尼黑、法兰克福等图可见大面积建筑候选，不能直接作为真实建筑布局。
- 彩色图能提取部分水域形状，但黑白图或淡色河流可能漏检；蓝色印刷线也可能误检。
- 山地、积雪、建筑摧毁属性无法从任意历史扫描图可靠推断。本次自动草稿不产生山地或雪地，建筑采用显式可摧毁策略。这些类别已通过人工 ID 图与 Godot 六类合成输入验证。
- 没有地理真值标签，未测 IoU/准确率；未验证路线可达性、AI 寻路、出生点、完整战役可玩性或导出安装包。

结论：**图片→六类数据→可运行 Godot 场景的流水线通过；现有 29 张图的自动分类仅为需要人工复核的草稿，不是最终可玩地图。**

## 复现

在项目根目录运行：

```text
python -m unittest discover -s test -p test_map_pipeline.py -v
python aseets/assets_generation_pipeline/build_maps.py --overwrite
Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --script test/test_generated_maps.gd
```

流水线每次写出 `generated_maps/build_report.json` 和各城的 `map.json`。汇总预览 `overview.png` 是本次审阅附件，不是每次构建自动维护的文件；重新构建后应重新生成或忽略它。
