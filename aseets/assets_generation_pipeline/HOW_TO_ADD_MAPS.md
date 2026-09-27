# KingWar 地图资产添加与规范化指南 (How to Add Maps)

本文档为 `KingWar` 资产生成管线（`assets_generation_pipeline`）提供标准规范，说明如何在后续开发中添加符合统一格式的**第二次世界大战重点城市军用地形图与战术规划图**。

---

## 目录
1. [选型与搜集标准](#1-选型与搜集标准)
2. [图像技术规范](#2-图像技术规范)
3. [命名规范与存储路径](#3-命名规范与存储路径)
4. [元数据注册规范 (manifest.json)](#4-元数据注册规范-manifestjson)
5. [标准添加流程（自动化与手动）](#5-标准添加流程)
6. [质量自检脚本](#6-质量自检脚本)

---

## 1. 选型与搜集标准

为了保障游戏战役推演、2D突破模式关卡生成、小地图战术标注及风格渲染的一致性，新增地图必须严格遵循以下原则：

### 1.1 时代背景范围
* 必须为**第二次世界大战期间（1939年9月 — 1945年9月）**编制、发行或战场实用的军用地图。
* 严禁使用战后现代卫星图、现代道路交通图或二战前的中世纪/近代古地图。

### 1.2 权威制图机构谱系
优先选用以下公版二战同盟国/轴心国正规军事测绘图系：
1. **美国陆军制图局（U.S. Army Map Service, AMS）**：
   * 城市详图（City Plans / Town Plans，比例尺通常为 1:10,000 ~ 1:25,000）
   * 战术附图（AMS Verso "City and Vicinity"，比例尺通常为 1:35,000 ~ 1:50,000）
   * 战术地形图（Series M501/M508/N501/N504 等 1:250,000 图幅）
2. **英国陆军总参谋部测绘局（War Office GSGS）**：
   * GSGS 4470（西欧城市图）、GSGS 4040（法国/比利时 1:50,000）、GSGS 4363（北欧/北极地区）等。
3. **德国陆军总参谋部（OKH / Deutsche Heereskarte）**：
   * 二战德军东线/西线战术城市规划图（Stadtplan）及 1:25,000 / 1:100,000 地形图。
4. **苏联红军总参谋部（РККА / Genshtab）**：
   * 1930s-1940s 苏军大比例尺战术工兵地形图。

### 1.3 核心公开检索渠道推荐
* **德克萨斯大学奥斯汀分校 PCL 军本地图库**：
  * 主页：`https://maps.lib.utexas.edu/maps/ams/`
  * 涵盖西欧、中欧、东欧、高加索、西伯利亚与巴尔干的完整 AMS 军规图册。
* **维基共享资源（Wikimedia Commons）**：
  * 分类：`Category:Maps by the United States Army Map Service`
  * 分类：`Category:Maps of World War II by country`
* **美国国家档案馆（NARA）与美国国会图书馆（Library of Congress）**：
  * 检索关键词格式：`"[City Name]" Army Map Service 1944 filetype:bitmap`

### 1.4 地图内容要素
* **中心定位**：必须以目标城市核心区（市区、主干道交汇点、行政核心或主要桥梁）为中心。
* **战术纵深**：画面需涵盖该城市外围 5~25 公里的战术纵深（河流防线、主要出城铁路线、火车站编组站、制高点与森林掩体）。
* **地理要素**：清晰呈现等高线/山体阴影、河流湖泊、街区建筑轮廓及等级道路网。

---

## 2. 图像技术规范

| 属性 | 严格规范指标 | 说明 |
| :--- | :--- | :--- |
| **文件容器** | `.png` | 标准无损便携式网络图形，禁止使用有损 `.jpg` 或未烘焙的 `.tif` |
| **色彩空间** | **RGB（24-bit True Color）** | 8位/通道（sRGB）。**严禁**使用 CMYK、灰度单通道或 8-bit Indexed 调色板模式 |
| **最低解析度** | **短边 ≥ 1500 px** | 推荐尺寸在 **2500×2500 至 7000×5500 px** 之间，保障放大后街道网格清晰 |
| **宽高比** | 自由矩形（适应原图构图） | 保持原图纵横比，严禁拉伸变形 |
| **图幅裁切** | 紧凑战术视窗 | 裁去多余的大面积纸张白边与扫描仪黑边，保留内图框（Neatline）与有效地理区域 |

---

## 3. 命名规范与存储路径

### 3.1 物理存储路径
所有标准化地图文件统一存放在：
```text
E:\Games\KingWar\aseets\assets_generation_pipeline\source\maps\
```

### 3.2 文件命名格式
```text
{Country}_{City}.png
```
* **字符规则**：全英文半角字符，首字母大写，使用下划线 `_` 连接，严禁使用中文、空格及特殊符号。
* **国家与城市名称**：使用通用国际英文标准名称。
* **特殊战役节点扩展**：若某图代表特定防线或关键战役，可添加第三段标签：`{Country}_{City}_{Context}.png`。
* **示例**：
  * 罗马：`Italy_Rome.png`
  * 华沙：`Poland_Warsaw.png`
  * 伦敦：`United_Kingdom_London.png`
  * 色当马其诺防线：`France_Sedan_Maginot.png`

---

## 4. 元数据注册规范 (manifest.json)

新添加的地图必须在同目录下的 `manifest.json` 文件中追加对应的 JSON 记录条目，确保自动化管线能够索引并解析历史属性。

### 4.1 JSON 字段定义模版
```json
{
  "id": "italy_rome",
  "country_zh": "意大利",
  "country_en": "Italy",
  "city_zh": "罗马",
  "city_en": "Rome",
  "filename": "Italy_Rome.png",
  "url": "https://maps.lib.utexas.edu/maps/ams/italy_city_plans/txu-pclmaps-oclc-6584080-rome.jpg",
  "crop": null,
  "series": "AMS Italy City Plans",
  "scale": "1:25,000",
  "year": 1944,
  "publisher": "U.S. Army Map Service",
  "historical_note": "1944年6月4日卡西诺战役后美军第5集团军进入并宣布为不设防城市解放。",
  "dimensions": [
    5420,
    4280
  ],
  "file_size_kb": 24500
}
```

### 4.2 字段说明
* `id`：全局唯一小写标识符，格式为 `{国家代码}_{城市拼写}`。
* `crop`：若从大张总图中裁切，填写 `[x1, y1, x2, y2]` 像素矩形；若为整张独立城市图则填写 `null`。
* `dimensions`：图像实际像素尺寸 `[宽, 高]`。
* `historical_note`：一句话概述该城市在二战中的核心战役与战术地位。

---

## 5. 标准添加流程

### 方式 A：通过管线脚本自动化添加（强烈推荐）

管线已内置自动化采集与标准化脚本 `download_wwii_maps.py`。该脚本支持增量断点执行、安全边界裁剪、色彩空间自动纠偏为 RGB、无损 PNG 转换，并会自动同步刷新 `manifest.json` 与 `README.md`。

#### 执行步骤：
1. 打开脚本文件：
   `E:\Games\KingWar\aseets\assets_generation_pipeline\download_wwii_maps.py`
2. 在 `CITIES` 列表末尾追加需要添加的城市配置项（参照上方模板）。
3. 运行终端执行命令：
   ```bash
   python E:/Games/KingWar/aseets/assets_generation_pipeline/download_wwii_maps.py
   ```
4. 脚本将自动：
   * 校验已存在的 29 张地图并快速跳过；
   * 下载新增城市的原始大图；
   * 自动按设定参数完成战术视窗裁切；
   * 自动转换为标准 24 位 RGB PNG 格式；
   * 自动更新 `manifest.json` 与 `README.md` 表格。

---

### 方式 B：手动处理离线导入

若使用的是本地扫描图、私有档案馆底片或无法直接通过 URL 抓取的图源，可采取手动处理流程：

1. **图像处理（Photoshop / GIMP / ImageMagick）**：
   * 打开源图，将图像旋转校正为“上北下南”标准方位。
   * 裁剪掉边缘多余的扫描空白台纸，将视窗调整为以该城市及近郊战术区为中心。
   * 检查色彩模式：选择 **图像 -> 模式 -> RGB 颜色（8位/通道）**。
   * 导出为 **PNG** 格式（级别压缩率选择高或默认，开启隔行扫描关闭，保留全部像素精度）。
2. **放置与命名**：
   * 保存至 `E:\Games\KingWar\aseets\assets_generation_pipeline\source\maps/`。
   * 命名为 `{Country}_{City}.png`。
3. **补充元数据**：
   * 在 `manifest.json` 末尾追加一条合法记录。
   * 在 `README.md` 的 Markdown 表格中追加一行中英文对照。

---

## 6. 质量自检脚本

在完成地图添加后，可在项目根目录下运行以下单行 Python 命令，对 `source/maps` 目录下的所有地图进行自动质量合规性检查：

```bash
python -c "
import os, json
from PIL import Image

dirpath = r'E:\Games\KingWar\aseets\assets_generation_pipeline\source\maps'
manifest_path = os.path.join(dirpath, 'manifest.json')

with open(manifest_path, 'r', encoding='utf-8') as f:
    manifest = json.load(f)

manifest_files = {item['filename'] for item in manifest}
png_files = [f for f in os.listdir(dirpath) if f.endswith('.png')]

print(f'=== KingWar Map Pipeline 自动化质量合规体检 ===')
print(f'检测到 PNG 文件总数: {len(png_files)} | Manifest 记录总数: {len(manifest)}')

errors = 0
for f in sorted(png_files):
    fp = os.path.join(dirpath, f)
    # 1. 检查命名规范
    if '_' not in f or f.count('_') > 2:
        print(f'[警告] 文件名不符合 {Country}_{City}.png 命名规范: {f}')
        errors += 1
    # 2. 检查图像完整性与格式
    try:
        with Image.open(fp) as im:
            w, h = im.size
            if im.format != 'PNG':
                print(f'[错误] 非真实 PNG 格式: {f} (实际: {im.format})')
                errors += 1
            if im.mode != 'RGB':
                print(f'[错误] 非标准 RGB 色彩模式: {f} (当前: {im.mode})')
                errors += 1
            if min(w, h) < 1000:
                print(f'[警告] 图像分辨率偏低: {f} ({w}x{h})')
                errors += 1
    except Exception as e:
        print(f'[致命错误] 图像损坏无法读取: {f} - {e}')
        errors += 1
    # 3. 检查 Manifest 登记
    if f not in manifest_files:
        print(f'[警告] 文件未在 manifest.json 中登记: {f}')
        errors += 1

if errors == 0:
    print('>> [全部通过] 所有地图文件格式标准、色彩模式一致、元数据登记完整！')
else:
    print(f'>> [检查结束] 发现 {errors} 项待优化问题，请对照指南修复。')
"
```

运行该命令若提示 `>> [全部通过]`，即证明新添加的地图与现有资产管线 100% 兼容。
