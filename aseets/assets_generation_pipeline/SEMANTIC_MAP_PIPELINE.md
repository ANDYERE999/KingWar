# 六类语义地图 → Godot 4 场景流水线

## 范围与真实性

本流水线将图片转换为独立的 Godot 4.3+ `Node2D` / `TileMapLayer` 场景。当前项目的 Godot 4.7.2 已通过运行测试。不修改现有主菜单、BattlefieldMap、AI、出生点、据点或武器脚本；生成物是地图资源，不是已接入原有突破模式的完整关卡。

普通历史地图的外观无法可靠说明积雪、山地边界，更无法说明建筑是否应允许摧毁。因此提供两种输入方式：

- **自动草稿**：蓝色区域候选为河流，深色块候选为建筑，其余为 default。文字、道路、蓝色坐标网可能误判；白纸不会被当成积雪，等高线不会被随意当作山体。山地和积雪留给标注。
- **明确标签**：通过 `--masks` 输入人工确认或外部模型产生并复核的六类 ID 图。这是正式关卡推荐路径，六类均可表达，不依赖源图颜色。

不能把“成功转换/场景加载”理解为地理识别准确率。此前地图目录中的年代、比例尺及裁切说明也应独立核实，流水线不会验证历史资料真实性。

## 安装与快速运行

在项目根目录执行（Windows PowerShell、CMD 或 Bash 均可使用以下单行命令）：

```text
python -m pip install -r aseets/assets_generation_pipeline/requirements.txt
python aseets/assets_generation_pipeline/build_maps.py --overwrite
```

已有环境如果已安装兼容的 `cv2`，不必再同时安装另一种 OpenCV 包。

默认输入为脚本旁 `source/maps`，输出为 `generated_maps`。支持目录批处理或单个 PNG/JPEG/TIFF 文件；目录仅扫描第一层，README 和 manifest 不作为输入。相同输出名（含大小写不敏感冲突）会报错。

```text
python aseets/assets_generation_pipeline/build_maps.py --input aseets/assets_generation_pipeline/source/maps/France_Paris.png --output aseets/assets_generation_pipeline/preview_maps --size 1024 --grid-size 128 --cell-size 32
```

| 参数 | 默认值 | 作用 |
| --- | --- | --- |
| `--input` | `source/maps` | 图片文件或目录 |
| `--output` | `generated_maps` | 目标目录 |
| `--size` | 1024 | 中间语义图最长边，256..4096，保留纵横比 |
| `--grid-size` | 128 | Godot 地图最长边格数，8..256，保留纵横比 |
| `--cell-size` | 32 | 每格 Godot 世界像素数，8..128 |
| `--building-policy` | `can_destroy` | 自动检测出的所有建筑统一为 can_destroy 或 no_destroy |
| `--masks` | 未设置 | 使用明确 ID 图，替代自动分类；缺失任一图即记录该输入失败，不静默降级 |
| `--overwrite` | 关闭 | 允许重新生成；已有目标目录且未指定时，该输入报错 |

未使用推理服务或下载模型。源图不修改。逐图失败不中断其他地图，但退出码为 1，总报告记录失败原因。没有输入或参数无效时直接失败。使用 `--overwrite` 只重写已知产物，不会清空整个输出目录；本次未处理的旧地图仍可能保留，以本次 build_report 为准。

## 六类协议

| ID | element_type | can_destroy | 默认物理行为 |
| --- | --- | --- | --- |
| 0 | default | false | 可通行 |
| 1 | river | false | 碰撞阻挡 |
| 2 | mountain | false | 碰撞阻挡 |
| 3 | snow | false | 可通行；不自动减速 |
| 4 | buildings | true | 碰撞阻挡；生命耗尽后变为 default |
| 5 | buildings | false | 碰撞阻挡；忽略伤害 |

JSON 中兼容使用 `buildings_can_destroy`、`buildings_no_destroy` 两个平面标签；`element_types` 提供分组结构。类别与美术贴图独立；目前只用六种纯色调试瓦片。桥梁需要在 mask 中标成可通行地形，流水线不会推断桥梁、道路连通性、出生点或寻路数据。

## 提供可靠的标注

1. 保留原始图片不变，另建 `reviewed_masks` 目录。
2. 为每张图片生成同名 PNG，例如 `France_Paris.png`。
3. 标注 PNG 必须是单通道 **L 模式**，尺寸与对应源图完全一致；每像素是整数 **0、1、2、3、4、5**，而不是 0/51/102/...，也不是预览 RGB 色值。
4. 可使用标注工具导出分割 ID；若导出的是 RGB 颜色图，需要先显式映射至 ID。不得直接灰度化预览颜色代替标签。
5. 执行：

```text
python aseets/assets_generation_pipeline/build_maps.py --masks aseets/assets_generation_pipeline/reviewed_masks --overwrite
```

ID 图缩放使用最近邻，不创造新 ID。场景栅格对语义图按区域投票，票数相同优先较小 ID；细河流、窄建筑会在粗栅格中丢失，需检查 `grid_preview.png`，必要时提高 `--grid-size` 或在标注中调整游戏尺度。单个场景最多 256×256 格，避免直接为百万像素创建物理瓦片。

## 生成目录

```text
generated_maps/
  semantic_map.gd
  build_report.json
  France_Paris/
    map.tscn
    map.json
    semantic_mask.png
    preview.png
    grid_preview.png
```

- `map.tscn`：包含场景栅格 ID 的原生 Godot 文本场景，可以实例化。依赖上一级 `semantic_map.gd`。
- `semantic_map.gd`：运行时构建 TileSet、TileMapLayer、六种瓦片及碰撞。颜色图集在内存中生成，不依赖 PNG 导入或原图。
- `semantic_mask.png`：中间单通道 ID 数据。不要以 JPEG 保存，也不要线性插值。
- `preview.png`：中间分类彩色预览。
- `grid_preview.png`：实际场景格子彩色预览，每格放大为 4×4 像素。
- `map.json`：schema v2、源文件 SHA-256、源图/语义图尺寸、六类统计、场景尺寸、世界尺寸、分类方式与警告。
- `build_report.json`：本次所有成功条目、失败条目与处理数量。后一次执行会覆盖报告。

世界尺寸 = 格数 × cell_size；这不是地图原始比例尺对应的真实米制单位。栅格按行存储，左上为起点，X 向右、Y 向下。

## Godot 使用

有两种消费方式，互不影响：

### A. 直接在游戏里玩生成地图（推荐）

游戏的战场已经改成数据驱动，会把 `semantic_mask.png` 直接变成可玩关卡：

1. 启动游戏（`Scenes/UI/MainMenu.tscn`）。
2. 在“选择作战地图”下拉框里选战场：
   * **内置演示条带** → `Scenes/Game/MainGame.tscn`（手写条带，不用掩膜）
   * **★ 波兰战役：但泽 ➔ 波兹南 ➔ 华沙（三城拼接）** → `Scenes/Game/MainGameCampaign.tscn`（默认选项）
   * **单城: <城市名>** → `Scenes/Game/MainGameMask.tscn`（单张城市图）
3. 选好后点开始。

### A1. 单城战场（`MaskBattlefieldMap`）

会从六类标签自动完成：

- **地形贴图**：整张掩膜一次性生成 `ImageTexture`，单次绘制调用。
- **碰撞**：山地用贪心矩形合并成**一个** StaticBody2D 的多个 shape，不是一个格子一个物体；河流**没有碰撞**（可通行但有减速惩罚）。
- **建筑**：`BuildingsCanDestroy` / `BuildingsNoDestroy` 合并成 `SemanticBuilding`（实现 `IDamageable`）。可摧毁建筑被打掉后，会把自身占地面积从 `ObstacleRectangles` 里移除，AI 寻路立刻不再绕行。
- **据点**：沿 +X 进攻轴把可通行范围切成 `SectorCount` 段，每段落在建筑最密集的可通行格上，依据是建筑积分图。
- **集结区 / 后方基地**：自动取可通行范围的左右两端。

关键导出参数（挂在 `BattlefieldMap` 节点上）：

| 参数 | 默认 | 作用 |
| --- | --- | --- |
| `MaskPath` | 空 | 指定掩膜；留空则用菜单选择，再不齐则用找到的第一张 |
| `CellSize` | 32 | 每格世界像素 |
| `TargetGridSize` | 160 | 掩膜降采样后的最长边格数 |
| `SectorCount` | 3 | 自动布设的据点数量 |
| `BuildingHealth` | 250 | 可摧毁建筑生命值 |
| `MaxBuildingCellsPerBlock` | 6 | 单个建筑块最大边长（格），防止贪心合并拉成长墙 |
| `DrawBuildings` | true | 关掉则建筑只保留碰撞，画面用底图 |

#### 建筑：可被全部武器打爆

可摧毁建筑（class 4）必须能被**子弹、火箭弹、手榴弹、坦克主炮**打爆。所有武器最终都做同一件事：

| 武器 | 查询方式 | 代码 |
| --- | --- | --- |
| 步枪/机枪 | 射线 | `Projectile.cs` → `IntersectRay` |
| 火箭弹 | 圆形范围 | `RocketProjectile.cs` → `IntersectShape` |
| 手榴弹 | 圆形范围 | `FragGrenade.cs` → `IntersectShape` |
| 坦克主炮 | 圆形范围 | `TankShellProjectile.cs` → `IntersectShape` |

这些查询都会拿到 `collider`，然后判断 `collider is IDamageable`。所以**建筑本身必须是那个物理体**。

> ⚠️ **容易踩的坑**：如果建一个 `Node2D` 再挂一个子 `StaticBody2D` 装碰撞形状，物理查询返回的 `collider` 是**子节点**，它不实现 `IDamageable`，所有武器都会走到“墙/不可伤害障碍”分支——表现为建筑特别“坚硬”，怎么打都不掉血。正确做法（也是 `CoverObject` / `SemanticBuilding` 的做法）是让节点**自己继承 `StaticBody2D`**，碰撞形状作为直接子节点。

伤害数值参考（默认 `BuildingHealth = 250`）：

* M4A1 单发 26 → 约 10 发打爆
* 手榴弹 120 → 约 3 颗
* 火箭弹直击 150 / 溅射 130 → 约 2 发
* 坦克主炮直击 150 → 约 2 发

不可摧毁建筑（class 5）完全免疫（包含火箭弹与主炮），并且会忽略负数 / NaN 等非法伤害值。

#### 建筑块尺寸上限

贪心矩形合并会把一整排城市纹理合成**一个**矩形，实测最长的达到 `3808px`。那意味着一次扫射就能抹掉整条街区。所以建筑合并默认加上 `MaxBuildingCellsPerBlock = 6`（即单块最长 192px）：

| 上限 | 建筑块数（三城拼接） | 最大单块 |
| --- | --- | --- |
| 无限 | 651 | 4320 px |
| **6 格（默认）** | **824** | **192 px** |
| 4 格 | 924 | 128 px |

注意：河流 / 山地的合并**不设上限**，因为它们越少越好（碰撞形状少）。

### A2. 三城拼接战役（`CampaignBattlefieldMap`）

把多张城市图沿进攻轴横向拼成一条连续战线，**每座城市只对应 1 个据点**：

- 先把整张画布填满平原（Default），再把各城市掩膜按顺序贴入；
- 城市之间的左右缝隙、以及上下因城市高度不同而多出的区域，**全部是平原**，因此读起来是开阔乡野而不是硬接缝；
- 每座城市的据点落在**该城自己的**建筑最密格上（在各自列区间内做密度搜索）；
- **占领半径按该城自己的城区包围盒计算**：`clamp(0.12 × min(城区宽, 城区高) × CellSize, min, max)`。占地大的城市目标圈就大，占地小的就小。
- 左右两端自动分别作为进攻方集结区 / 守方后方基地。

关键导出参数：

| 参数 | 默认 | 作用 |
| --- | --- | --- |
| `CityMaskPaths` | 但泽 / 波兹南 / 华沙 | 按战役顺序排列的城市掩膜数组 |
| `GapCells` | 40 | 两城之间的平原走廊宽度（格） |
| `CampaignCaptureRadiusMin` / `Max` | 240 / 700 | 单城占领半径的上下限（px） |
| `CellSize` / `TargetGridSize` | 32 / 160 | 同单城战场 |

默认三城拼接实测：总栅格 `550x160`，世界 `17600x5120`，建筑块 651，据点 A/B/C 半径 `415 / 445 / 526 px`。

### A3. 地形通行规则

| 地形 | 步兵 | 坦克 |
| --- | --- | --- |
| default / snow | 全速 | 全速 |
| river | **0.60x 减速** | **0.60x 减速** |
| mountain | **0.50x 减速** | **无法通过**（碰撞阻挡） |
| buildings (4/5) | 阻挡 | 阻挡 |

实现方式：

- 减速：`BattlefieldBase.SpeedMultiplierFor()` 是纯函数，步兵在 `Soldier.MoveInDirection()`、坦克在底盘移动处乘以该系数。
- 坦克不能过山地：山地碰撞体被放在一个**车辆专用物理层**（`BattlefieldBase.MountainCollisionLayer = 4`），坦克的 `CollisionMask` 包含该层，步兵不含。所以同一道山脊，步兵能爬（减速），坦克过不去。
- 河流故意零碰撞，只靠减速表达。


**为什么必须降采样**：`semantic_mask.png` 是给机器看的 1024px 数据栅格。直接按 32px/格渲染会得到 30000px 宽的世界和 8000+ 个建筑块，完全不可玩。所以默认先按多数投票降到 160 格（平票取较小 ID，和流水线一致），世界尺寸落到约 4700×5100。想要更精细就调大 `TargetGridSize` 并相应调小 `CellSize`。

#### 与旧战场的衔接

`BattlefieldBase` 定义了统一契约（尺寸、据点、阻挡矩形、双方集结位置），`BattlefieldMap`（手写条带）和 `MaskBattlefieldMap`（掩膜生成）都实现它。`ObjectiveManager` 的扇区边界不再硬编码，而是由已注册据点位置 + 集结位置推导；出生点、小地图、停战隔离墙也都改成跟随实际地图尺寸。因此换地图不需要改游戏逻辑。

### B. 单独预览流水线场景

每条地图另外自带一个独立瓦片场景，用于检查分类效果：

1. 在 Godot 打开 `aseets/assets_generation_pipeline/generated_maps/France_Paris/map.tscn`。
2. 按 F6 运行；自动创建适配视窗的 Camera2D。
3. 鼠标左键点击 ID=4 格子可测试摧毁；ID=5 不变。
4. 复制到其他项目时同时带上 `semantic_map.gd`。

该预览用 `TileMapLayer`，会用掉上万个瓦片单元，与大战场面无关。

## 测试

Python 合成输入验证六类保真、参数策略、非法标注、投票规则与场景写出：

```text
python -m unittest discover -s test -p test_map_pipeline.py -v
```

实际输入全量构建：

```text
python aseets/assets_generation_pipeline/build_maps.py --overwrite
```

Godot 实际加载所有生成场景并验证六类瓦片、碰撞定义、坐标换算、建筑伤害与不可摧毁行为：

```text
Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . --script test/test_generated_maps.gd
```

测试结果见 `MAP_PIPELINE_TEST_RESULTS.md`。正式使用前还必须人工审查分类预览与通行路线。引擎测试通过不证明地形识别正确。
